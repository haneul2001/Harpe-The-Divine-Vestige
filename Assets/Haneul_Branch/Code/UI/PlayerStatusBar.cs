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

    [Header("상태 아이콘 (체력바 위)")]
    [Tooltip("물약 소켓을 떠 온 26x26 칸 그림 (Art/UI/StatusSlot.png)")]
    [SerializeField] private Sprite statusSlot;
    [Tooltip("48x48 대시 아이콘 (Art/UI/StatusIcons)")]
    [SerializeField] private Sprite dashIcon;
    [Tooltip("48x48 대시 공격 아이콘")]
    [SerializeField] private Sprite dashAttackIcon;
    [Tooltip("48x48 처형 버프 아이콘. [0]=1단계 [1]=2단계 [2]=3단계. 한 장만 넣으면 전 단계 공용")]
    [SerializeField] private Sprite[] harvestBuffIcons = new Sprite[3];
    [SerializeField] private float statusIconGap = 6f;

    [Header("스킬 쿨타임 (화면 아래 가운데)")]
    [Header("골드")]
    [Tooltip("골드 표시용 동전 그림. 비우면 Resources/Loot/GoldCoin 프리팹의 첫 프레임을 쓴다")]
    [SerializeField] private Sprite goldIcon;
    [Tooltip("동전 그림 확대 배율 (원본 픽셀 정수 배)")]
    [Range(1, 6)] [SerializeField] private int goldIconScale = 2;
    [SerializeField] private Color goldColor = new Color(0.93f, 0.78f, 0.35f);

    [Tooltip("발동 키 표시용 14x14 키캡 (Art/UI/KeyCap.png, 9-slice)")]
    [SerializeField] private Sprite keyCapSprite;
    [Tooltip("키캡 확대 배율 (원본 픽셀 정수 배)")]
    [Range(1, 4)]
    [SerializeField] private int keyCapScale = 2;
    [Tooltip("화면 아래 끝에서 스킬 칸까지 거리")]
    [SerializeField] private float skillBarBottom = 24f;
    [SerializeField] private float skillSlotGap = 18f;
    [Tooltip("48x48 처형 스킬 아이콘")]
    [SerializeField] private Sprite harvestSkillIcon;
    [Tooltip("처형 가능할 때 아이콘 뒤에서 타오르는 불꽃 프레임 (Art/UI/StatusIcons/HarvestFlame_0~7)")]
    [SerializeField] private Sprite[] harvestReadyFlame = new Sprite[0];
    [SerializeField] private float harvestFlameFps = 12f;
    [Tooltip("불꽃 위치 (1080p 기준 픽셀, 아이콘 중심에서)")]
    [SerializeField] private Vector2 harvestFlameOffset = new Vector2(0f, 6f);

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
    private Text goldText;
    // 체력바 프레임 원본에서 물약 소켓 칸이 시작하는 x (StatusSlot.png를 떠 온 자리)
    private const float StatusSlotFrameX = 24f;

    private PlayerMove playerMove;
    private PlayerDashAttack dashAttack;
    private HarvestBuff harvestBuff;
    private StatusIconSlot dashSlot;
    private bool dashAttackKeyPending;
    private StatusIconSlot dashAttackSlot;
    private StatusIconSlot harvestSkillSlot;
    private PlayerCombat combat;
    private StatusIconSlot harvestSlot;
    private float hpTrail = 1f;
    private float trailHoldUntil;

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
        if (FindObjectOfType<PlayerStatusBar>() != null) return;
        // 플레이어가 없는 씬(타이틀·컷신)에는 표시할 것이 없다
        if (FindObjectOfType<PlayerStatus>() == null) return;

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

        Transform body = status != null ? status.transform : p.transform;
        playerMove = body.GetComponent<PlayerMove>();
        dashAttack = body.GetComponent<PlayerDashAttack>();
        harvestBuff = body.GetComponent<HarvestBuff>();
        combat = body.GetComponent<PlayerCombat>();

        if (dashAttackKeyPending && dashAttackSlot != null && dashAttack != null)
        {
            dashAttackSlot.SetKeyLabel(StatusIconSlot.KeyName(dashAttack.Key), keyCapSprite, font, keyCapScale);
            dashAttackKeyPending = false;
        }
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
        if (!EnsureBuilt()) return;
        if (status == null) { ResolvePlayer(); return; }
        Refresh(false);
    }

    // 에디터에서 플레이 중에 스크립트를 고치면 도메인이 다시 로드된다.
    // 그때 직렬화되지 않는 필드(막대 참조·Instance)는 날아가는데 Awake는 다시 돌지 않아
    // 상태바가 참조 없이 남아 매 프레임 터진다. 참조가 비어 있으면 다시 만든다.
    // (빌드에서는 도메인 리로드가 없어 한 번도 타지 않는 길이다)
    private bool EnsureBuilt()
    {
        if (hpBar != null && soulBar != null) return true;

        if (Instance == null) Instance = this;
        else if (Instance != this) return false;

        // 남아 있던 옛 캔버스는 치운다 (Destroy는 프레임 끝이라 한 프레임은 겹친다)
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        BuildUI();
        ResolvePlayer();
        Refresh(true);
        return false;   // 이번 프레임은 여기까지 — 다음 프레임부터 정상
    }

    private void Refresh(bool snap)
    {
        if (status == null || hpBar == null || soulBar == null) return;

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

        if (goldText != null) goldText.text = status.CurrentGold.ToString();

        if (playerSprite != null) hpBar.SetPortrait(playerSprite.sprite);

        RefreshStatusIcons();
    }

    // 쿨타임은 남은 비율만큼 위에서부터 가리고 남은 초를 적는다. 준비되면 깨끗한 아이콘만 남는다.
    // 처형 버프는 단계가 있을 때만 보이고, 흐른 시간만큼 가리며 오른쪽 아래에 단계 숫자를 단다.
    private void RefreshStatusIcons()
    {
        if (dashSlot != null)
        {
            float remain = playerMove != null ? playerMove.DashCooldownRemaining : 0f;
            float total = playerMove != null ? playerMove.DashCooldown : 1f;
            dashSlot.SetShade(total > 0f ? remain / total : 0f);
            dashSlot.SetTimer(remain);
        }

        if (dashAttackSlot != null)
        {
            dashAttackSlot.SetVisible(dashAttack != null);
            float remain = dashAttack != null ? dashAttack.CooldownRemaining : 0f;
            float total = dashAttack != null ? dashAttack.Cooldown : 1f;
            dashAttackSlot.SetShade(total > 0f ? remain / total : 0f);
            dashAttackSlot.SetTimer(remain);
        }

        // 처형 스킬: 쿨타임이 없고, 주변에 처형할 수 있는 몬스터가 있을 때만 뒤에서 불꽃이 타오른다
        if (harvestSkillSlot != null)
        {
            bool ready = combat != null && combat.CanHarvestNow;
            harvestSkillSlot.SetDimmed(!ready);
            harvestSkillSlot.SetBackFlame(ready, harvestReadyFlame, harvestFlameFps, harvestFlameOffset);
        }

        if (harvestSlot != null)
        {
            int stage = harvestBuff != null ? harvestBuff.Stage : 0;
            harvestSlot.SetVisible(stage > 0);
            if (stage > 0)
            {
                float remain = harvestBuff.Remaining;
                float total = harvestBuff.Duration;
                harvestSlot.SetIcon(HarvestIconFor(stage));
                harvestSlot.SetShade(total > 0f ? 1f - remain / total : 0f);
                harvestSlot.SetTimer(remain);
                harvestSlot.SetBadge(stage.ToString());
            }
        }
    }

    // 소울 바 오른쪽 빈자리에 동전과 숫자만. 바를 하나 더 늘리면 화면 아래가 답답해진다
    private void BuildGold(RectTransform root, float soulW, int s, float barH)
    {
        Sprite icon = goldIcon;
        if (icon == null)
        {
            GameObject coin = Resources.Load<GameObject>("Loot/GoldCoin");
            if (coin != null)
            {
                var sr = coin.GetComponentInChildren<SpriteRenderer>(true);
                if (sr != null) icon = sr.sprite;
            }
        }

        RectTransform box = UIFactory.Empty("Gold", root);
        box.anchorMin = box.anchorMax = Vector2.zero;
        box.pivot = new Vector2(0f, 0f);
        box.anchoredPosition = new Vector2(soulW + 14f * s, 0f);
        box.sizeDelta = new Vector2(barWidth - soulW, barH);

        float iconSize = 0f;
        if (icon != null)
        {
            Image img = UIFactory.Panel("Coin", box, Color.white, false);
            img.sprite = icon;
            img.preserveAspect = true;
            iconSize = Mathf.Max(icon.rect.width, icon.rect.height) * Mathf.Max(1, goldIconScale);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            img.rectTransform.pivot = new Vector2(0f, 0.5f);
            img.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            img.rectTransform.anchoredPosition = Vector2.zero;
        }

        goldText = UIFactory.Label("GoldText", box, font, 18, goldColor, TextAnchor.MiddleLeft, FontStyle.Bold);
        // 칸이 좁아 기본값(줄바꿈)이면 "120"이 "12/0"으로 잘린다
        goldText.horizontalOverflow = HorizontalWrapMode.Overflow;
        goldText.verticalOverflow = VerticalWrapMode.Overflow;
        UIFactory.SetAnchoredBox(goldText.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(iconSize + 4f * s, 0f), Vector2.zero);
        var sh = goldText.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
        sh.effectDistance = new Vector2(2f, -2f);
        goldText.text = "0";
    }

    private Sprite HarvestIconFor(int stage)
    {
        if (harvestBuffIcons == null || harvestBuffIcons.Length == 0) return null;
        int i = Mathf.Clamp(stage - 1, 0, harvestBuffIcons.Length - 1);
        return harvestBuffIcons[i] != null ? harvestBuffIcons[i] : harvestBuffIcons[0];
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
        soulBar = PixelBar.Build("SoulBar", root, font, 18, false);   // 1440p에서 18×1.33=24px — 갈무리 격자 2배라 선명
        soulBar.Root.anchorMin = Vector2.zero;
        soulBar.Root.anchorMax = Vector2.zero;
        soulBar.Root.pivot = Vector2.zero;
        soulBar.Root.anchoredPosition = Vector2.zero;
        soulBar.Layout(soulW, s, soulFrame, fallbackTrackColor);
        soulBar.Fill.color = soulColor;
        soulBar.Trail.color = new Color(soulColor.r, soulColor.g, soulColor.b, 0f);

        hpBar = PixelBar.Build("HpBar", root, font, 18, false);
        hpBar.Root.anchorMin = Vector2.zero;
        hpBar.Root.anchorMax = Vector2.zero;
        hpBar.Root.pivot = Vector2.zero;
        hpBar.Root.anchoredPosition = new Vector2(0f, barH + gap);
        BuildGold(root, soulW, s, barH);

        hpBar.Layout(barWidth, s, hpFrame, fallbackTrackColor);
        hpBar.Fill.color = hpColor;
        hpBar.Trail.color = trailColor;

        // 버프 줄: 체력바 바로 위. 첫 칸은 체력바 물약 소켓(프레임 x24~49)과 세로로 줄을 맞춘다
        float rowY = barH * 2f + gap + statusIconGap;
        float rowX = StatusSlotFrameX * s;
        harvestSlot = StatusIconSlot.Build("Status_HarvestBuff", root, statusSlot, HarvestIconFor(1), "처형", s, font);
        harvestSlot.Root.anchoredPosition = new Vector2(rowX, rowY);
        harvestSlot.SetVisible(false);

        // 스킬 쿨타임 줄: 화면 아래 가운데. 칸 위에 발동 키를 키캡으로 띄운다
        RectTransform skillRow = UIFactory.Empty("SkillRow", canvasGo.transform);
        skillRow.anchorMin = skillRow.anchorMax = new Vector2(0.5f, 0f);
        skillRow.pivot = new Vector2(0.5f, 0f);
        skillRow.anchoredPosition = new Vector2(0f, skillBarBottom);
        float slotW = StatusIconSlot.SlotPixels * s;
        skillRow.sizeDelta = new Vector2(slotW * 3f + skillSlotGap * 2f, slotW);

        dashSlot = StatusIconSlot.Build("Skill_Dash", skillRow, statusSlot, dashIcon, "대시", s, font);
        dashSlot.Root.anchoredPosition = Vector2.zero;
        dashSlot.SetKeyLabel(StatusIconSlot.KeyName(PlayerMove.DashKey), keyCapSprite, font, keyCapScale);

        dashAttackSlot = StatusIconSlot.Build("Skill_DashAttack", skillRow, statusSlot, dashAttackIcon, "돌진", s, font);
        dashAttackSlot.Root.anchoredPosition = new Vector2(slotW + skillSlotGap, 0f);
        // 대시 공격 키는 플레이어에 설정돼 있어 플레이어를 찾은 뒤(Start) 단다
        dashAttackKeyPending = true;

        harvestSkillSlot = StatusIconSlot.Build("Skill_Harvest", skillRow, statusSlot, harvestSkillIcon, "처형", s, font);
        harvestSkillSlot.Root.anchoredPosition = new Vector2((slotW + skillSlotGap) * 2f, 0f);
        harvestSkillSlot.SetKeyLabel(StatusIconSlot.KeyName(PlayerCombat.HarvestKey), keyCapSprite, font, keyCapScale);
    }
}
