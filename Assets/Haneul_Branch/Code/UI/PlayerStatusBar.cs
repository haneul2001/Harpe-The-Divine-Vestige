using UnityEngine;
using UnityEngine.UI;

// 화면 왼쪽 아래에 붙는 플레이어 체력·소울 바.
// 적 체력바와 같은 프레임(2D Pixel Quest의 Dynamic Bars)을 써서 화면 전체가 한 벌로 보이게 한다.
//
// 씬에 미리 만들어 둘 것이 없다. 재생하면 알아서 하나 생기고, 플레이어는 태그로 찾는다.
public class PlayerStatusBar : MonoBehaviour
{
    public static PlayerStatusBar Instance { get; private set; }

    [Header("프레임 그림")]
    [SerializeField] private Sprite hpFrame;
    [SerializeField] private Sprite soulFrame;
    [Tooltip("원본이 160x32 픽셀아트라 정수 배로만 키운다")]
    [Range(1, 6)]
    [SerializeField] private int frameScale = 3;

    [Header("배치")]
    [SerializeField] private float barWidth = 620f;
    [Tooltip("소울 바는 체력보다 짧게 둔다 — 시선이 체력에 먼저 가야 한다")]
    [SerializeField] private float soulWidthRatio = 0.78f;
    [SerializeField] private Vector2 margin = new Vector2(28f, 26f);
    [SerializeField] private float gap = 6f;

    [Header("색")]
    [SerializeField] private Color hpColor = new Color(0.78f, 0.16f, 0.16f);
    [SerializeField] private Color soulColor = new Color(0.42f, 0.34f, 0.86f);
    [SerializeField] private Color trailColor = new Color(0.98f, 0.88f, 0.55f, 0.9f);
    [SerializeField] private Color fallbackTrackColor = new Color(0.14f, 0.13f, 0.15f, 1f);
    [SerializeField] private Font font;

    [Header("연출")]
    [Tooltip("맞은 뒤 잔상이 따라 내려오기 전 멈춰 있는 시간(초)")]
    [SerializeField] private float trailDelay = 0.35f;
    [SerializeField] private float trailSpeed = 0.55f;

    [Header("기존 UI")]
    [Tooltip("씬에 남아 있는 예전 슬라이더 바를 재생 중에만 숨긴다. "
           + "씬 파일은 건드리지 않으므로 끄면 그대로 다시 보인다.")]
    [SerializeField] private bool hideLegacyBars = true;

    [Tooltip("이름으로 찾아서 숨길 예전 UI. 체력바는 PlayerHpUI 컴포넌트로 찾으므로 여기 없어도 된다")]
    [SerializeField] private string[] legacyBarNames = { "Soul bar" };

    private PlayerStatus status;
    private SpriteRenderer playerSprite;
    private PixelBar hpBar;
    private PixelBar soulBar;
    private float hpTrail = 1f;
    private float trailHoldUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        if (FindObjectOfType<PlayerStatusBar>() != null) return;

        GameObject prefab = Resources.Load<GameObject>("UI/PlayerStatusBar");
        if (prefab != null) Instantiate(prefab).name = "PlayerStatusBar";
        else new GameObject("PlayerStatusBar").AddComponent<PlayerStatusBar>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // 플레이어는 Start에서 찾는다. Awake 시점엔 아직 씬의 다른 오브젝트가 준비되지 않았을 수 있다.
    private void Start()
    {
        ResolvePlayer();
        if (hideLegacyBars) HideLegacyBars();
        Refresh(true);
    }

    private void ResolvePlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;

        status = p.GetComponentInParent<PlayerStatus>();
        if (status == null) status = p.GetComponentInChildren<PlayerStatus>();
        playerSprite = p.GetComponentInChildren<SpriteRenderer>();
    }

    // 씬에 있는 예전 체력바(슬라이더)를 끈다. 새 바와 겹쳐 두 개가 보이는 걸 막는다.
    // 씬을 수정하는 게 아니라 재생 중에만 꺼지므로 되돌릴 것이 없다.
    private void HideLegacyBars()
    {
        var legacy = FindObjectOfType<PlayerHpUI>();
        if (legacy != null) legacy.gameObject.SetActive(false);

        // 소울 바는 전용 스크립트가 없어 이름으로 찾는다.
        // 비활성 오브젝트까지 훑어야 하므로 슬라이더 전체를 돌며 이름을 맞춘다.
        if (legacyBarNames == null) return;
        foreach (var slider in FindObjectsOfType<Slider>(true))
        {
            for (int i = 0; i < legacyBarNames.Length; i++)
            {
                if (string.IsNullOrEmpty(legacyBarNames[i])) continue;
                if (slider.name != legacyBarNames[i]) continue;
                slider.gameObject.SetActive(false);
                break;
            }
        }
    }

    private void Update()
    {
        if (status == null) { ResolvePlayer(); return; }
        Refresh(false);
    }

    private void Refresh(bool snap)
    {
        if (status == null) return;

        float ratio = status.MaxHp > 0 ? Mathf.Clamp01((float)status.CurrentHp / status.MaxHp) : 0f;
        hpBar.SetRatio(ratio);
        hpBar.Label.text = PixelBar.Format(status.CurrentHp, status.MaxHp);

        // 잔상: 깎이면 잠깐 멈췄다가 따라 내려온다. 회복은 즉시 따라 올린다.
        if (snap || ratio > hpTrail) hpTrail = ratio;
        else if (ratio < hpTrail)
        {
            if (trailHoldUntil <= 0f) trailHoldUntil = Time.unscaledTime + trailDelay;
            if (Time.unscaledTime >= trailHoldUntil)
            {
                hpTrail = Mathf.MoveTowards(hpTrail, ratio, trailSpeed * Time.unscaledDeltaTime);
                if (Mathf.Approximately(hpTrail, ratio)) trailHoldUntil = 0f;
            }
        }
        hpBar.SetTrail(hpTrail);

        float soul = status.MaxSoul > 0 ? Mathf.Clamp01((float)status.CurrentSoul / status.MaxSoul) : 0f;
        soulBar.SetRatio(soul);
        soulBar.SetTrail(soul);   // 소울은 잔상 없이 즉시 따라간다 — 자원이라 타격감이 필요 없다
        soulBar.Label.text = PixelBar.Format(status.CurrentSoul, status.MaxSoul);

        if (playerSprite != null) hpBar.SetPortrait(playerSprite.sprite);
    }

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("PlayerStatusCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 140;   // 적 체력바(150)보다 아래

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var group = canvasGo.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        RectTransform root = UIFactory.Empty("Root", canvasGo.transform);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.anchoredPosition = margin;

        int s = Mathf.Max(1, frameScale);
        float barH = PixelBar.SrcHeight * s;
        float soulW = barWidth * Mathf.Clamp01(soulWidthRatio);

        root.sizeDelta = new Vector2(barWidth, barH * 2f + gap);

        // 소울이 아래, 체력이 위 — 체력이 눈높이에 가깝다
        soulBar = PixelBar.Build("SoulBar", root, font, 16, false);
        soulBar.Root.anchorMin = Vector2.zero;
        soulBar.Root.anchorMax = Vector2.zero;
        soulBar.Root.pivot = Vector2.zero;
        soulBar.Root.anchoredPosition = Vector2.zero;
        soulBar.Layout(soulW, s, soulFrame, fallbackTrackColor);
        soulBar.Fill.color = soulColor;
        soulBar.Trail.color = new Color(soulColor.r, soulColor.g, soulColor.b, 0f);

        hpBar = PixelBar.Build("HpBar", root, font, 20, false);
        hpBar.Root.anchorMin = Vector2.zero;
        hpBar.Root.anchorMax = Vector2.zero;
        hpBar.Root.pivot = Vector2.zero;
        hpBar.Root.anchoredPosition = new Vector2(0f, barH + gap);
        hpBar.Layout(barWidth, s, hpFrame, fallbackTrackColor);
        hpBar.Fill.color = hpColor;
        hpBar.Trail.color = trailColor;
    }
}
