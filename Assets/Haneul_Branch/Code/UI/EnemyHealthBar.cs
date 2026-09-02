using UnityEngine;
using UnityEngine.UI;

// 화면 상단에 뜨는 적 체력바. 로스트아크·다크소울류의 보스 바와 같은 자리.
//
//   · 보스   — 방에 들어가면 뜨고, 죽을 때까지 안 내려간다
//   · 정예/일반 — 때린 순간 떴다가 잠시 뒤 사라진다
//
// UI는 미니맵·능력 패널과 같은 방식으로 런타임에 코드로 조립한다. 씬에 미리 만들어 둘 것이 없다.
// 어느 씬에서든 알아서 하나 생기므로 씬마다 배치할 필요도 없다.
public class EnemyHealthBar : MonoBehaviour
{
    public static EnemyHealthBar Instance { get; private set; }

    [Header("배치")]
    [Tooltip("화면 위쪽에서 띄우는 거리(px)")]
    [SerializeField] private float topMargin = 76f;
    [Tooltip("체력바 줄의 높이(px). 등급별 가로 길이는 EnemyGrade에서 정한다")]
    [SerializeField] private float barHeight = 26f;
    [Tooltip("초상화 한 변(px). 0이면 초상화를 만들지 않는다")]
    [SerializeField] private float portraitSize = 66f;

    [Tooltip("체력바가 차지할 수 있는 화면 폭의 최대 비율. 등급별 기본 폭이 화면보다 넓으면 "
           + "이 비율까지 줄인다. 좁은 화면비에서 바가 화면 밖으로 나가는 걸 막는다.")]
    [Range(0.3f, 1f)]
    [SerializeField] private float maxWidthRatio = 0.94f;

    [Header("표시 시간")]
    [Tooltip("일반·정예를 때린 뒤 이 시간 동안 더 떠 있는다(초). 보스는 무시된다")]
    [SerializeField] private float normalHoldTime = 3.5f;
    [Tooltip("대상이 죽은 뒤 바가 남아 있는 시간(초). 0까지 내려가는 걸 보여 주기 위한 여유")]
    [SerializeField] private float deathHoldTime = 1.2f;
    [Tooltip("나타나고 사라지는 데 걸리는 시간(초)")]
    [SerializeField] private float fadeDuration = 0.18f;

    [Header("연출")]
    [Tooltip("깎인 만큼을 천천히 따라 내려오는 잔상 바. 한 방에 얼마나 들어갔는지 눈에 보인다")]
    [SerializeField] private Color trailColor = new Color(0.96f, 0.86f, 0.55f, 0.9f);
    [Tooltip("잔상이 따라붙기 전 멈춰 있는 시간(초)")]
    [SerializeField] private float trailDelay = 0.35f;
    [Tooltip("잔상이 내려오는 속도(초당 비율)")]
    [SerializeField] private float trailSpeed = 0.55f;

    [Header("색")]
    [SerializeField] private Color backdropColor = new Color(0.04f, 0.04f, 0.06f, 0.82f);
    [SerializeField] private Color trackColor = new Color(0.14f, 0.13f, 0.15f, 1f);
    [SerializeField] private Color nameColor = new Color(0.96f, 0.96f, 0.93f);
    [SerializeField] private Font font;

    // ── 상태 ────────────────────────────────────────────
    private Enemy target;
    private EnemyGrade shownGrade = EnemyGrade.Normal;
    private float hideAtTime = -1f;   // 이 시각이 지나면 사라진다. 보스는 -1로 두어 안 사라짐
    private float alpha;
    private float trailRatio = 1f;
    private float trailHoldUntil;

    // ── 조립된 조각들 ───────────────────────────────────
    private CanvasGroup group;
    private RectTransform root;
    private RectTransform frame;
    private Image portrait;
    private RectTransform portraitBox;
    private Image portraitBorder;
    private Text gradeLabel;
    private Image gradeChip;
    private Text nameLabel;
    private Text percentLabel;
    private Text hpLabel;
    private RectTransform barRow;
    private RectTransform fillRect;
    private RectTransform trailRect;
    private Image fillImage;
    private Image trailImage;
    private RectTransform segmentsRoot;
    private RectTransform content;
    private Image accentBar;

    // 씬마다 배치하지 않아도 되도록 재생 시작 시 스스로 하나 만든다.
    // Resources에 프리팹을 두면 그걸 쓰고(인스펙터로 조절 가능), 없으면 기본값으로 생성한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        if (FindObjectOfType<EnemyHealthBar>() != null) return;

        GameObject prefab = Resources.Load<GameObject>("UI/EnemyHealthBar");
        if (prefab != null) Instantiate(prefab).name = "EnemyHealthBar";
        else new GameObject("EnemyHealthBar").AddComponent<EnemyHealthBar>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BuildUI();
        SetAlpha(0f);
    }

    private void OnEnable()  { Enemy.AnyDamaged += OnAnyDamaged; }
    private void OnDisable() { Enemy.AnyDamaged -= OnAnyDamaged; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    // ─────────────────────────────────────────────
    // 외부 진입점
    // ─────────────────────────────────────────────

    // 보스 방에 들어가는 순간 Room이 호출한다. 때리지 않아도 바로 뜬다.
    public static void ShowBoss(Enemy enemy)
    {
        if (Instance == null || enemy == null) return;
        enemy.Grade = EnemyGrade.Boss;
        Instance.Bind(enemy);
    }

    // 맞은 적을 띄운다. 보스가 살아 있는 동안에는 잡몹이 자리를 뺏지 못한다.
    private void OnAnyDamaged(Enemy enemy)
    {
        if (enemy == null) return;

        bool bossHeld = target != null && !target.isDead && target.Grade == EnemyGrade.Boss;
        if (bossHeld && enemy.Grade != EnemyGrade.Boss) return;

        // 같은 대상이면 다시 조립하지 않고 표시 시간만 늘린다
        if (enemy == target) { RefreshHold(); return; }

        Bind(enemy);
    }

    private void Bind(Enemy enemy)
    {
        target = enemy;
        shownGrade = enemy.Grade;

        Layout(shownGrade);

        nameLabel.text = enemy.DisplayName;
        gradeLabel.text = shownGrade.Label();
        gradeChip.color = shownGrade.Accent();
        gradeLabel.color = new Color(0.06f, 0.06f, 0.07f);
        nameLabel.color = shownGrade == EnemyGrade.Normal ? nameColor : shownGrade.Accent();
        portraitBorder.color = shownGrade.Accent();
        fillImage.color = shownGrade.Fill();

        if (portrait != null)
        {
            Sprite s = enemy.spriteRenderer != null ? enemy.spriteRenderer.sprite : null;
            portrait.sprite = s;
            portrait.enabled = s != null;
        }

        BuildSegments(shownGrade.Segments());

        // 새 대상은 잔상도 현재 체력에서 시작한다 — 안 그러면 전 대상의 잔상이 흘러내린다
        trailRatio = Ratio();
        trailHoldUntil = 0f;

        RefreshHold();
    }

    private void RefreshHold()
    {
        hideAtTime = (shownGrade == EnemyGrade.Boss) ? -1f : Time.unscaledTime + normalHoldTime;
    }

    private float Ratio()
    {
        if (target == null || target.maxHp <= 0) return 0f;
        return Mathf.Clamp01((float)target.hp / target.maxHp);
    }

    // ─────────────────────────────────────────────
    // 갱신
    // ─────────────────────────────────────────────

    // 보스 처치 연출이 시간을 늦추므로 전부 실제 시간(unscaled)으로 돈다.
    // 스케일된 시간을 쓰면 슬로우 동안 체력바만 기어간다.
    private void Update()
    {
        bool visible = target != null;

        if (visible)
        {
            if (target.isDead && hideAtTime < 0f)
                hideAtTime = Time.unscaledTime + deathHoldTime;   // 보스도 죽으면 내려간다

            if (hideAtTime > 0f && Time.unscaledTime >= hideAtTime)
                visible = false;
        }

        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration);
        SetAlpha(Mathf.MoveTowards(alpha, visible ? 1f : 0f, step));

        if (alpha <= 0f)
        {
            if (target != null && !visible) target = null;   // 완전히 사라진 뒤에 놓아 준다
            return;
        }
        if (target == null) return;

        float ratio = Ratio();
        SetBarRatio(fillRect, ratio);

        // 잔상: 잠깐 멈췄다가 따라 내려온다
        if (ratio > trailRatio) trailRatio = ratio;                 // 회복하면 즉시 따라 올린다
        else if (ratio < trailRatio)
        {
            if (trailHoldUntil <= 0f) trailHoldUntil = Time.unscaledTime + trailDelay;
            if (Time.unscaledTime >= trailHoldUntil)
            {
                trailRatio = Mathf.MoveTowards(trailRatio, ratio, trailSpeed * Time.unscaledDeltaTime);
                if (Mathf.Approximately(trailRatio, ratio)) trailHoldUntil = 0f;
            }
        }
        SetBarRatio(trailRect, trailRatio);

        hpLabel.text = target.hp.ToString("N0") + " / " + target.maxHp.ToString("N0");
        percentLabel.text = Mathf.CeilToInt(ratio * 100f) + "%";
    }

    private static void SetBarRatio(RectTransform rt, float ratio)
    {
        Vector2 max = rt.anchorMax;
        max.x = Mathf.Clamp01(ratio);
        rt.anchorMax = max;
        rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
    }

    private void SetAlpha(float value)
    {
        alpha = Mathf.Clamp01(value);
        group.alpha = alpha;
    }

    // ─────────────────────────────────────────────
    // 조립 — 미니맵·능력 패널과 같이 런타임에 코드로 만든다
    // ─────────────────────────────────────────────

    private const float Padding = 10f;

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("EnemyHealthBarCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;   // 게임 HUD 위, 일시정지 메뉴(250) 아래

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasGo.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;   // 체력바가 클릭을 가로채면 안 된다

        // 화면 위쪽 가운데에 매단다
        root = UIFactory.Empty("Root", canvasGo.transform);
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);

        Image back = UIFactory.Panel("Backdrop", root, backdropColor, false);
        frame = back.rectTransform;
        UIFactory.SetAnchoredBox(frame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 등급 색 띠. 배경만으로는 등급이 안 읽혀서 위쪽에 얇게 하나 깐다.
        accentBar = UIFactory.Panel("Accent", root, Color.white, false);
        UIFactory.SetAnchoredBox(accentBar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -3f), Vector2.zero);

        BuildPortrait();

        content = UIFactory.Empty("Content", root);

        BuildTopRow();
        BuildBar();
    }

    private void BuildPortrait()
    {
        if (portraitSize <= 0f) return;

        portraitBorder = UIFactory.Panel("PortraitBorder", root, Color.white, false);
        portraitBox = portraitBorder.rectTransform;
        portraitBox.anchorMin = new Vector2(0f, 0.5f);
        portraitBox.anchorMax = new Vector2(0f, 0.5f);
        portraitBox.pivot = new Vector2(0f, 0.5f);
        portraitBox.sizeDelta = new Vector2(portraitSize, portraitSize);
        portraitBox.anchoredPosition = new Vector2(Padding, 0f);

        Image inner = UIFactory.Panel("PortraitFill", portraitBox, new Color(0.09f, 0.09f, 0.11f, 1f), false);
        UIFactory.SetAnchoredBox(inner.rectTransform, Vector2.zero, Vector2.one,
            Vector2.one * 2f, -Vector2.one * 2f);

        portrait = UIFactory.Panel("Portrait", inner.transform, Color.white, false);
        UIFactory.SetAnchoredBox(portrait.rectTransform, Vector2.zero, Vector2.one,
            Vector2.one * 4f, -Vector2.one * 4f);
        portrait.preserveAspect = true;
        portrait.enabled = false;
    }

    private void BuildTopRow()
    {
        RectTransform row = UIFactory.Empty("TopRow", content);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(0f, 26f);
        row.anchoredPosition = Vector2.zero;

        // 등급 칩 — 이름 앞에 붙는 작은 태그
        gradeChip = UIFactory.Panel("GradeChip", row, Color.white, false);
        RectTransform chip = gradeChip.rectTransform;
        chip.anchorMin = new Vector2(0f, 0.5f);
        chip.anchorMax = new Vector2(0f, 0.5f);
        chip.pivot = new Vector2(0f, 0.5f);
        chip.sizeDelta = new Vector2(52f, 22f);
        chip.anchoredPosition = Vector2.zero;

        gradeLabel = UIFactory.Label("GradeText", chip, font, 14, Color.black,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(gradeLabel.rectTransform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);

        nameLabel = UIFactory.Label("Name", row, font, 24, nameColor,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.SetAnchoredBox(nameLabel.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(62f, 0f), new Vector2(-64f, 0f));
        AddShadow(nameLabel.gameObject);

        percentLabel = UIFactory.Label("Percent", row, font, 16,
            new Color(0.72f, 0.74f, 0.78f), TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.SetAnchoredBox(percentLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-62f, 0f), Vector2.zero);
    }

    private void BuildBar()
    {
        barRow = UIFactory.Empty("BarRow", content);
        barRow.anchorMin = new Vector2(0f, 0f);
        barRow.anchorMax = new Vector2(1f, 0f);
        barRow.pivot = new Vector2(0.5f, 0f);
        barRow.sizeDelta = new Vector2(0f, barHeight);
        barRow.anchoredPosition = Vector2.zero;

        Image track = UIFactory.Panel("Track", barRow, trackColor, false);
        UIFactory.SetAnchoredBox(track.rectTransform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);

        // 잔상이 먼저(뒤에), 실제 체력이 나중에(앞에) 그려져야 겹쳐 보인다
        trailImage = UIFactory.Panel("Trail", track.transform, trailColor, false);
        trailRect = trailImage.rectTransform;
        UIFactory.SetAnchoredBox(trailRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        fillImage = UIFactory.Panel("Fill", track.transform, Color.white, false);
        fillRect = fillImage.rectTransform;
        UIFactory.SetAnchoredBox(fillRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 칸 나눔은 체력 위에 얹혀야 남은 칸이 보인다
        segmentsRoot = UIFactory.Empty("Segments", track.transform);
        UIFactory.SetAnchoredBox(segmentsRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        hpLabel = UIFactory.Label("Hp", track.transform, font, 15, Color.white,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(hpLabel.rectTransform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        AddShadow(hpLabel.gameObject);
    }

    // 글자가 체력바 색 위에서도 읽히도록
    private static void AddShadow(GameObject go)
    {
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    // 등급이 바뀌면 전체 크기가 바뀐다. 보스는 넓고 잡몹은 좁다.
    private void Layout(EnemyGrade grade)
    {
        float width = grade.BarWidth();

        // 캔버스가 기준 해상도보다 좁으면(4:3 창 등) 바가 화면 밖으로 나간다. 여기서 잘라 준다.
        RectTransform canvasRect = root.parent as RectTransform;
        if (canvasRect != null && canvasRect.rect.width > 0f)
            width = Mathf.Min(width, canvasRect.rect.width * maxWidthRatio);
        float height = Padding * 2f + 26f + 6f + barHeight;
        if (portraitSize > 0f) height = Mathf.Max(height, portraitSize + Padding * 2f);

        root.sizeDelta = new Vector2(width, height);
        root.anchoredPosition = new Vector2(0f, -topMargin);

        float left = portraitSize > 0f ? Padding + portraitSize + 12f : Padding;
        UIFactory.SetAnchoredBox(content, Vector2.zero, Vector2.one,
            new Vector2(left, Padding), new Vector2(-Padding, -Padding));

        if (accentBar != null) accentBar.color = grade.Accent();
    }

    // 보스 체력을 몇 칸으로 나눌지. 남은 양을 눈으로 세기 쉬워진다.
    private void BuildSegments(int count)
    {
        for (int i = segmentsRoot.childCount - 1; i >= 0; i--)
            Destroy(segmentsRoot.GetChild(i).gameObject);

        if (count <= 1) return;

        for (int i = 1; i < count; i++)
        {
            float t = (float)i / count;
            Image line = UIFactory.Panel("Seg" + i, segmentsRoot, new Color(0f, 0f, 0f, 0.55f), false);
            RectTransform rt = line.rectTransform;
            rt.anchorMin = new Vector2(t, 0f);
            rt.anchorMax = new Vector2(t, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(2f, 0f);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
