using UnityEngine;
using UnityEngine.UI;

// 화면 상단에 뜨는 적 체력바. 로스트아크·다크소울류의 보스 바와 같은 자리.
//
//   · 보스   — 방에 들어가면 뜨고, 죽을 때까지 안 내려간다
//   · 정예/일반 — 때린 순간 떴다가 잠시 뒤 사라진다
//
// 막대 자체의 조립은 PixelBar가 맡는다. 여기는 "누구를 언제 보여줄지"만 정한다.
// 씬에 미리 만들어 둘 것이 없고, 어느 씬에서든 재생하면 알아서 하나 생긴다.
public class EnemyHealthBar : MonoBehaviour
{
    public static EnemyHealthBar Instance { get; private set; }

    [Header("프레임 그림")]
    [Tooltip("보스용 프레임. 비우면 단색 막대로 대체된다")]
    [SerializeField] private Sprite bossFrame;
    [SerializeField] private Sprite eliteFrame;
    [SerializeField] private Sprite normalFrame;
    [Tooltip("원본이 160x32 픽셀아트라 정수 배로만 키운다. 3이면 높이 96px")]
    [Range(1, 6)]
    [SerializeField] private int frameScale = 3;

    [Header("배치")]
    [Tooltip("화면 위쪽에서 띄우는 거리(px)")]
    [SerializeField] private float topMargin = 76f;
    [Tooltip("이름·등급이 들어가는 윗줄 높이(px)")]
    [SerializeField] private float nameRowHeight = 34f;
    [Tooltip("체력바가 차지할 수 있는 화면 폭의 최대 비율. 좁은 화면비에서 밖으로 나가는 걸 막는다")]
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
    [Tooltip("깎인 만큼을 천천히 따라 내려오는 잔상. 한 방에 얼마나 들어갔는지 눈에 보인다")]
    [SerializeField] private Color trailColor = new Color(0.98f, 0.88f, 0.55f, 0.9f);
    [SerializeField] private float trailDelay = 0.35f;
    [SerializeField] private float trailSpeed = 0.55f;

    [Header("색")]
    [Tooltip("프레임 그림이 없을 때만 쓰는 배경색")]
    [SerializeField] private Color fallbackTrackColor = new Color(0.14f, 0.13f, 0.15f, 1f);
    [SerializeField] private Color nameColor = new Color(0.97f, 0.94f, 0.86f);
    [SerializeField] private Font font;

    // ── 상태 ────────────────────────────────────────────
    private Enemy target;
    private EnemyGrade shownGrade = EnemyGrade.Normal;
    private float hideAtTime = -1f;   // 이 시각이 지나면 사라진다. 보스는 -1이라 안 사라짐
    private float alpha;
    private float trailRatio = 1f;
    private float trailHoldUntil;

    // ── 조립된 조각들 ───────────────────────────────────
    private CanvasGroup group;
    private RectTransform root;
    private RectTransform nameRow;
    private PixelBar bar;
    private Text gradeLabel;
    private Image gradeChip;
    private Text nameLabel;

    // 씬이 열릴 때마다 챙긴다. 재시작(씬 재로드) 후에도 다시 생기게 하기 위해서다 —
    // RuntimeInitializeOnLoadMethod 하나만으로는 실행 시작에 한 번밖에 안 돈다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        RuntimeSingletons.EnsureEachScene(Spawn);
    }

    private static void Spawn()
    {
        if (Instance != null) return;
        if (FindObjectOfType<EnemyHealthBar>() != null) return;
        // 플레이어가 없는 씬(타이틀·컷신)에는 보여 줄 적이 없다
        if (FindObjectOfType<PlayerStatus>() == null) return;

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

        if (enemy == target) { RefreshHold(); return; }   // 같은 대상이면 시간만 늘린다

        Bind(enemy);
    }

    private void Bind(Enemy enemy)
    {
        target = enemy;
        shownGrade = enemy.Grade;

        Layout(shownGrade);

        nameLabel.text  = enemy.DisplayName;
        gradeLabel.text = shownGrade.Label();
        gradeChip.color = shownGrade.Accent();
        nameLabel.color = shownGrade == EnemyGrade.Normal ? nameColor : shownGrade.Accent();
        bar.Fill.color  = shownGrade.Fill();
        bar.Trail.color = trailColor;
        bar.SetPortrait(enemy.Portrait);
        // 보스는 등급별 기본 칸수 대신 실제 페이즈 경계를 긋는다.
        // 선이 "여기서 뭔가 바뀐다"는 예고 역할을 하게 된다.
        BossEnemy boss = enemy as BossEnemy;
        float[] phases = boss != null ? boss.PhaseBoundaries : null;

        if (phases != null) bar.BuildSegmentsAt(phases);
        else bar.BuildSegments(shownGrade.Segments());

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
    // 갱신 — 보스 처치 연출이 시간을 늦추므로 전부 실제 시간으로 돈다
    // ─────────────────────────────────────────────

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
        bar.SetRatio(ratio);

        // 잔상: 잠깐 멈췄다가 따라 내려온다
        if (ratio > trailRatio) trailRatio = ratio;
        else if (ratio < trailRatio)
        {
            if (trailHoldUntil <= 0f) trailHoldUntil = Time.unscaledTime + trailDelay;
            if (Time.unscaledTime >= trailHoldUntil)
            {
                trailRatio = Mathf.MoveTowards(trailRatio, ratio, trailSpeed * Time.unscaledDeltaTime);
                if (Mathf.Approximately(trailRatio, ratio)) trailHoldUntil = 0f;
            }
        }
        bar.SetTrail(trailRatio);

        bar.Label.text = PixelBar.Format(target.hp, target.maxHp);
    }

    private void SetAlpha(float value)
    {
        alpha = Mathf.Clamp01(value);
        group.alpha = alpha;
    }

    // ─────────────────────────────────────────────
    // 조립
    // ─────────────────────────────────────────────

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

        root = UIFactory.Empty("Root", canvasGo.transform);
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);

        // 이름 줄
        nameRow = UIFactory.Empty("NameRow", root);
        nameRow.anchorMin = new Vector2(0f, 1f);
        nameRow.anchorMax = new Vector2(1f, 1f);
        nameRow.pivot = new Vector2(0.5f, 1f);
        nameRow.anchoredPosition = Vector2.zero;

        gradeChip = UIFactory.Panel("GradeChip", nameRow, Color.white, false);
        RectTransform chip = gradeChip.rectTransform;
        chip.anchorMin = new Vector2(0f, 0.5f);
        chip.anchorMax = new Vector2(0f, 0.5f);
        chip.pivot = new Vector2(0f, 0.5f);
        chip.sizeDelta = new Vector2(60f, 26f);

        gradeLabel = UIFactory.Label("GradeText", chip, font, 16, new Color(0.07f, 0.06f, 0.05f),
            TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(gradeLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        nameLabel = UIFactory.Label("Name", nameRow, font, 28, nameColor,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(nameLabel.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(70f, 0f), new Vector2(-70f, 0f));
        PixelBar.AddOutline(nameLabel.gameObject);

        // 막대
        bar = PixelBar.Build("Frame", root, font, 22, true);
        bar.Root.anchorMin = new Vector2(0f, 0f);
        bar.Root.anchorMax = new Vector2(1f, 0f);
        bar.Root.pivot = new Vector2(0.5f, 0f);
        bar.Root.anchoredPosition = Vector2.zero;
    }

    private void Layout(EnemyGrade grade)
    {
        float width = grade.BarWidth();
        RectTransform canvasRect = root.parent as RectTransform;
        if (canvasRect != null && canvasRect.rect.width > 0f)
            width = Mathf.Min(width, canvasRect.rect.width * maxWidthRatio);

        int s = Mathf.Max(1, frameScale);
        root.sizeDelta = new Vector2(width, PixelBar.SrcHeight * s + nameRowHeight);
        root.anchoredPosition = new Vector2(0f, -topMargin);
        nameRow.sizeDelta = new Vector2(0f, nameRowHeight);

        bar.Layout(width, s, FrameFor(grade), fallbackTrackColor);
        // 폭은 부모를 따라가게 두고(좌우 앵커), 높이만 프레임에서 정한다
        bar.Root.sizeDelta = new Vector2(0f, PixelBar.SrcHeight * s);
    }

    private Sprite FrameFor(EnemyGrade grade)
    {
        switch (grade)
        {
            case EnemyGrade.Boss:  return bossFrame;
            case EnemyGrade.Elite: return eliteFrame != null ? eliteFrame : bossFrame;
            default:               return normalFrame != null ? normalFrame : bossFrame;
        }
    }
}
