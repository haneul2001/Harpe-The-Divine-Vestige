using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 떠돌이 상인. 골드로 사고파는 곳이다.
//
//  · 체력 물약 — 언제든 다시 살 수 있다
//  · 능력치 카드 — 한 번 사면 품절
//  · 특성 카드 — 가끔만 들어온다. 성장을 통째로 사는 것이라 값이 비싸다
//  · 도박 — 판돈을 걸고 주사위. 대부분 잃지만 특성 카드가 나오기도 한다
//
// 진열은 층마다 고정이다 — 같은 층에서 몇 번을 드나들어도 같은 물건이고,
// 다음 층으로 내려가면 새로 뽑는다 (닫을 때마다 바뀌면 원하는 게 나올 때까지 여닫게 된다).
//
// 소울은 이제 경험치(TraitLevelUp)라 여기서는 쓰지 않는다.
public class ShopPanel : MonoBehaviour
{
    public enum StatKind { Attack, CritRate, MoveSpeed, MaxHp }

    [System.Serializable]
    public class StatOffer
    {
        public StatKind kind = StatKind.Attack;
        [Tooltip("올려 주는 양. 치명타는 %p, 이동속도는 %, 나머지는 수치 그대로")]
        public float amount = 5f;
        public int price = 40;
        public Sprite icon;
    }

    [Header("상호작용")]
    [SerializeField] private float interactRange = 1.8f;
    [SerializeField] private KeyCode interactKey = KeyCode.V;
    [Tooltip("상인 발밑에서 V 표시까지의 높이 (월드 단위 — 상인 스케일과 무관)")]
    [SerializeField] private float promptHeight = 1.5f;
    [Tooltip("V 표시 키캡 한 변 크기 (월드 단위)")]
    [SerializeField] private float promptSize = 0.5f;

    [Header("체력 물약")]
    [SerializeField] private int potionPrice = 20;
    [Tooltip("최대 체력의 몇 %를 회복할지")]
    [Range(5, 100)] [SerializeField] private int potionHealPercent = 35;
    [SerializeField] private Sprite potionIcon;

    [Header("능력치 카드")]
    [Tooltip("이 중에서 골라 진열한다")]
    [SerializeField]
    private List<StatOffer> statPool = new List<StatOffer>
    {
        new StatOffer { kind = StatKind.Attack,    amount = 5f,  price = 40 },
        new StatOffer { kind = StatKind.CritRate,  amount = 2f,  price = 45 },
        new StatOffer { kind = StatKind.MoveSpeed, amount = 5f,  price = 35 },
        new StatOffer { kind = StatKind.MaxHp,     amount = 10f, price = 30 },
    };
    [Tooltip("한 번에 진열할 능력치 카드 수")]
    [Range(1, 4)] [SerializeField] private int statSlots = 2;

    [Header("특성 카드 (가끔 들어온다)")]
    [Tooltip("진열에 특성 카드가 낄 확률")]
    [Range(0f, 1f)] [SerializeField] private float traitOfferChance = 0.35f;
    [Tooltip("등급별 값 (일반 · 희귀 · 에픽 · 전설). 성장을 통째로 사는 것이라 비싸다")]
    [SerializeField] private int[] traitPrices = { 50, 100, 150, 200 };
    [Tooltip("어떤 등급이 들어올지 (일반 · 희귀 · 에픽 · 전설)")]
    [SerializeField] private float[] traitRarityWeights = { 55f, 28f, 12f, 5f };

    [Header("도박")]
    [SerializeField] private int gambleStake = 30;
    [Tooltip("실패 / 1.5배 / 2배 / 특성카드 확률(%) — 합이 100이 되게")]
    [SerializeField] private float failChance = 60f;
    [SerializeField] private float x15Chance = 20f;
    [SerializeField] private float x2Chance = 10f;
    [SerializeField] private float traitChance = 10f;
    [SerializeField] private Sprite gambleIcon;

    [Header("픽셀 UI (2D Pixel Quest Vol.3)")]
    [Tooltip("그림 1픽셀을 UI 몇 칸으로 그릴지")]
    [SerializeField] private float pixelScale = 2f;
    [Tooltip("상단 제목 띠")]
    [SerializeField] private Sprite titlePlateSprite;
    [Tooltip("골드 표시 받침")]
    [SerializeField] private Sprite badgePlateSprite;
    [SerializeField] private Sprite goldIcon;
    [Tooltip("머리 위 V 표시 뒤에 까는 키캡 그림")]
    [SerializeField] private Sprite promptKeySprite;

    [Header("색 (사신 톤)")]
    [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.8f);
    [SerializeField] private Color panelFill = new Color(0.07f, 0.07f, 0.10f, 0.98f);
    [SerializeField] private Color panelBorder = new Color(0.66f, 0.58f, 0.39f, 0.9f);
    [SerializeField] private Color slotFill = new Color(0.11f, 0.11f, 0.14f, 1f);
    [SerializeField] private Color goldText = new Color(0.93f, 0.83f, 0.56f);
    [SerializeField] private Color dimText = new Color(0.49f, 0.51f, 0.55f);

    [SerializeField] private Font font;

    [Tooltip("비워 두면 단색 폴백으로 그려진다")]
    [SerializeField] private AbilityUISkin skin;

    public bool IsOpen { get; private set; }

    private Transform player;
    private PlayerStatus playerStatus;
    private bool inRange;
    private GameObject prompt;

    private GameObject rootGo;
    private RectTransform frameRect;
    private RectTransform row;
    private Text goldLabel;
    private Text resultLabel;
    private TooltipView tooltip;

    private float previousTimeScale = 1f;
    private readonly List<Slot> slots = new List<Slot>();

    // 상인 한 명이 파는 물건 한 칸
    private class Slot
    {
        public RectTransform root;
        public Image window;      // 아이콘이 앉는 아치 창
        public Text name;
        public Text price;
        public Image dim;         // 품절/못 삼 표시
        public int cost;
        public bool soldOut;
        public System.Func<bool> buy;   // 샀으면 true
        public string tipTitle, tipBody;

        // 주인이 죽으면 이 중 하나가 바닥에 떨어진다. 도박·물약 칸은 비어 있다
        public AbilityCard traitCard;
        public StatOffer statOffer;
        public Sprite icon;
    }

    // 카드 그림(100x155)의 아치 창·이름칸 위치
    private static readonly Vector2 WindowMin = new Vector2(0.22f, 0.38f);
    private static readonly Vector2 WindowMax = new Vector2(0.78f, 0.81f);
    private static readonly Vector2 NameMin = new Vector2(0.14f, 0.08f);
    private static readonly Vector2 NameMax = new Vector2(0.86f, 0.29f);
    private static readonly Vector2 CardSize = new Vector2(200f, 310f);
    private const float CardGap = 28f;

    private Enemy owner;

    private void Start()
    {
        // 상인이 맞을 수 있게 Enemy가 붙어 있으면 죽음을 지켜본다
        owner = GetComponent<Enemy>();
        if (owner != null) owner.Died += OnOwnerDied;

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerStatus = p.GetComponent<PlayerStatus>();
        }

        BuildPrompt();
        BuildUI();
        Close();
    }

    private void Update()
    {
        if (player == null) return;

        // 주인이 죽으면 더는 열리지 않는다 (열려 있었다면 닫는다)
        if (owner != null && owner.isDead)
        {
            if (IsOpen) Close();
            if (prompt != null && prompt.activeSelf) prompt.SetActive(false);
            return;
        }

        if (!IsOpen)
        {
            bool nowInRange = Vector2.Distance(transform.position, player.position) <= interactRange;
            if (nowInRange != inRange)
            {
                inRange = nowInRange;
                if (prompt != null) prompt.SetActive(inRange);
            }

            if (inRange && Input.GetKeyDown(interactKey)) Open();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    // ─────────────────────────────────────────────
    // 열기 / 닫기
    // ─────────────────────────────────────────────

    public void Open()
    {
        RestockIfFloorChanged();

        rootGo.SetActive(true);
        IsOpen = true;

        if (prompt != null) prompt.SetActive(false);

        previousTimeScale = Time.timeScale > 0f ? Mathf.Max(1f, Time.timeScale) : 1f;
        Time.timeScale = 0f;

        SetPlayerInputLocked(true);
        if (resultLabel != null) resultLabel.text = "";
        Refresh();
    }

    public void Close()
    {
        if (rootGo != null) rootGo.SetActive(false);

        // 이미 닫혀 있으면 시간에 손대지 않는다.
        // 도박으로 특성이 나오면 잠시 뒤 자동으로 닫히는데, 그 사이에 플레이어가 먼저 ESC로 닫으면
        // 예약된 닫기가 한 번 더 들어와 남의 멈춤(특성 선택 화면)을 풀어 버린다.
        if (!IsOpen) return;
        IsOpen = false;

        if (tooltip != null) tooltip.Hide();

        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        SetPlayerInputLocked(false);
    }

    // 층이 바뀌면 진열을 새로 뽑는다.
    // 보통은 층을 내려갈 때 던전이 통째로 다시 지어지면서 상인도 새로 생기지만,
    // 상인이 살아남는 경로가 생겨도 물건이 그대로 남지 않도록 여기서도 챙긴다.
    private int stockFloor = -1;

    private void RestockIfFloorChanged()
    {
        int floor = RunStats.Instance != null ? RunStats.Instance.Floor : 1;
        if (floor == stockFloor) return;

        BuildStock();
    }

    private void OnDestroy()
    {
        if (owner != null) owner.Died -= OnOwnerDied;
    }

    // 주인이 죽으면 진열대에서 한 장이 바닥에 떨어진다
    private void OnOwnerDied(Enemy enemy)
    {
        if (IsOpen) Close();
        DropOneCard();
    }

    private void DropOneCard()
    {
        // 물약·도박 칸은 카드가 아니다. 팔린 칸도 뺀다
        var candidates = new List<Slot>();
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            if (slot.soldOut) continue;
            if (slot.traitCard != null || slot.statOffer != null) candidates.Add(slot);
        }
        if (candidates.Count == 0) return;

        Slot drop = candidates[Random.Range(0, candidates.Count)];
        Vector3 at = transform.position + new Vector3(0f, 0.4f, 0f);
        Sprite panel = skin != null ? skin.TooltipBackground() : null;

        if (drop.traitCard != null)
        {
            AbilityCard card = drop.traitCard;
            CardDrop.Spawn(at, card.Icon, card.DisplayName,
                card.Rarity.Label() + " · " + card.Description, card.Rarity.Color(),
                promptKeySprite, font, panel,
                delegate { if (AbilityInventory.Instance != null) AbilityInventory.Instance.Add(card); });
            return;
        }

        StatOffer offer = drop.statOffer;
        CardDrop.Spawn(at, offer.icon, StatName(offer.kind), StatEffectText(offer), goldText,
            promptKeySprite, font, panel,
            delegate { ApplyStat(offer); });
    }

    private void SetPlayerInputLocked(bool locked)
    {
        if (player == null) return;
        PlayerMove move = player.GetComponent<PlayerMove>();
        if (move != null) move.inputLocked = locked;
    }

    // ─────────────────────────────────────────────
    // 상호작용 프롬프트
    // ─────────────────────────────────────────────

    private void BuildPrompt()
    {
        // 상인 프리팹은 3배 스케일이라 자식으로 그냥 붙이면 표시도 3배로 커진다.
        Vector3 s = transform.lossyScale;
        float inv = Mathf.Abs(s.x) > 0.0001f ? 1f / Mathf.Abs(s.x) : 1f;

        var go = new GameObject("InteractPrompt");
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * inv;
        go.transform.localPosition = new Vector3(0f, promptHeight * inv, 0f);

        if (promptKeySprite != null)
        {
            var cap = new GameObject("KeyCap");
            cap.transform.SetParent(go.transform, false);
            var sr = cap.AddComponent<SpriteRenderer>();
            sr.sprite = promptKeySprite;
            sr.sortingLayerName = "Skill";
            sr.sortingOrder = 50;
            float spriteSize = Mathf.Max(promptKeySprite.bounds.size.x, 0.0001f);
            cap.transform.localScale = Vector3.one * (promptSize / spriteSize);
        }

        var label = new GameObject("Key");
        label.transform.SetParent(go.transform, false);
        var tm = label.AddComponent<TextMesh>();
        tm.text = interactKey.ToString();
        tm.fontSize = 48;
        tm.characterSize = promptSize * 0.55f / (tm.fontSize * 0.1f);
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = goldText;

        var mr = label.GetComponent<MeshRenderer>();
        Font keyFont = UIFactory.ResolveBoldFont();
        if (keyFont != null)
        {
            tm.font = keyFont;
            mr.sharedMaterial = keyFont.material;
            tm.fontSize = 48;
        }
        else tm.fontStyle = FontStyle.Bold;

        mr.sortingLayerName = "Skill";
        mr.sortingOrder = 51;

        prompt = go;
        prompt.SetActive(false);
    }

    private Image PixelImage(string name, Transform parent, Sprite sprite, bool raycast = false)
    {
        return UIFactory.PixelImage(name, parent, sprite, pixelScale, raycast);
    }

    private static void AddShadow(Text t)
    {
        var sh = t.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
        sh.effectDistance = new Vector2(2f, -2f);
    }

    // ─────────────────────────────────────────────
    // UI 조립
    // ─────────────────────────────────────────────

    private const float HeaderHeight = 104f;
    private const float FooterHeight = 150f;

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("ShopCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        rootGo = UIFactory.Stretch("Root", canvasGo.transform).gameObject;

        Image backdrop = UIFactory.Panel("Backdrop", rootGo.transform, backdropColor);
        UIFactory.SetAnchoredBox(backdrop.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform frame;
        RectTransform inner;
        Sprite panelSprite = skin != null ? skin.PanelBackground() : null;
        if (panelSprite != null)
        {
            frame = PixelImage("Frame", rootGo.transform, panelSprite, true).rectTransform;
            inner = frame;
        }
        else
        {
            Image frameFill;
            frame = UIFactory.BorderedPanel("Frame", rootGo.transform, panelFill, panelBorder, 3f, out frameFill);
            inner = (RectTransform)frame.GetChild(0);
        }
        UIFactory.SetAnchoredBox(frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        frameRect = frame;
        ResizeFrame(2 + Mathf.Clamp(statSlots, 1, 4));   // 물약 + 능력치 + 도박

        BuildHeader(inner);

        row = UIFactory.Empty("Row", inner);
        UIFactory.SetAnchoredBox(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -(HeaderHeight + CardSize.y)), new Vector2(0f, -HeaderHeight));

        resultLabel = UIFactory.Label("Result", inner, font, 20, goldText, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(resultLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(32f, 78f), new Vector2(-32f, 116f));
        AddShadow(resultLabel);

        Text hint = UIFactory.Label("Hint", inner, font, 15, dimText, TextAnchor.MiddleCenter);
        UIFactory.SetAnchoredBox(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(32f, 34f), new Vector2(-32f, 62f));
        hint.text = "카드를 눌러 산다  ·  ESC 닫기";

        GameObject tipGo = new GameObject("TooltipView");
        tipGo.transform.SetParent(rootGo.transform, false);
        tooltip = tipGo.AddComponent<TooltipView>();
        tooltip.Build(canvasRect, font, 420f, new Color(0.05f, 0.05f, 0.06f, 0.97f), panelBorder, new Vector2(20f, -14f), skin);
        tipGo.transform.SetAsLastSibling();

        BuildStock();
    }

    private void BuildHeader(RectTransform parent)
    {
        RectTransform titleBox;
        if (titlePlateSprite != null)
        {
            titleBox = PixelImage("TitlePlate", parent, titlePlateSprite).rectTransform;
            UIFactory.SetAnchoredBox(titleBox, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-150f, -26f), new Vector2(150f, 22f));
        }
        else
        {
            titleBox = UIFactory.Empty("TitlePlate", parent);
            UIFactory.SetAnchoredBox(titleBox, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-150f, -52f), new Vector2(150f, -16f));
        }

        Text title = UIFactory.Label("Title", titleBox, font, 26, goldText, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 2f), Vector2.zero);
        title.text = "떠돌이 상인";
        AddShadow(title);

        Text line = UIFactory.Label("Line", parent, font, 12, dimText, TextAnchor.MiddleLeft);
        UIFactory.SetAnchoredBox(line.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(32f, -84f), new Vector2(-190f, -44f));
        line.text = "\"죽은 자의 주머니는 가볍지. 자네 건 무거워 보이는군.\"";

        RectTransform badge;
        if (badgePlateSprite != null)
            badge = PixelImage("GoldBadge", parent, badgePlateSprite).rectTransform;
        else
        {
            Image fill;
            badge = UIFactory.BorderedPanel("GoldBadge", parent, slotFill,
                new Color(goldText.r, goldText.g, goldText.b, 0.55f), 2f, out fill);
        }
        UIFactory.SetAnchoredBox(badge, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-176f, -84f), new Vector2(-28f, -44f));

        if (goldIcon != null)
        {
            Image icon = UIFactory.Panel("GoldIcon", badge, Color.white, false);
            icon.sprite = goldIcon;
            icon.preserveAspect = true;
            UIFactory.SetAnchoredBox(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(6f, -18f), new Vector2(42f, 18f));
        }

        goldLabel = UIFactory.Label("GoldText", badge, font, 20, goldText, TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.SetAnchoredBox(goldLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(48f, 0f), new Vector2(-14f, 0f));
        AddShadow(goldLabel);
    }

    // ─────────────────────────────────────────────
    // 진열
    // ─────────────────────────────────────────────

    // 상인이 이번에 파는 물건을 정한다. 능력치 카드는 풀에서 골라 진열한다.
    private void BuildStock()
    {
        stockFloor = RunStats.Instance != null ? RunStats.Instance.Floor : 1;

        for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
        slots.Clear();

        var offers = new List<StatOffer>();
        var bag = new List<StatOffer>(statPool);
        int want = Mathf.Clamp(statSlots, 1, Mathf.Max(1, bag.Count));
        for (int i = 0; i < want && bag.Count > 0; i++)
        {
            int k = Random.Range(0, bag.Count);
            offers.Add(bag[k]);
            bag.RemoveAt(k);
        }

        // 특성 카드는 가끔만 들어온다 — 늘 있으면 도박도 성장도 의미가 옅어진다
        AbilityCard trait = RollTraitOffer();

        int columns = 2 + offers.Count + (trait != null ? 1 : 0);
        float step = CardSize.x + CardGap;
        float left = -(columns - 1) * step * 0.5f;
        int col = 0;

        // 진열 수에 따라 창 너비도 같이 늘린다
        ResizeFrame(columns);

        // ① 체력 물약
        Slot potion = BuildSlot(left + step * col++, potionIcon, "체력 물약", potionPrice);
        potion.tipTitle = "체력 물약";
        potion.tipBody = "최대 체력의 <color=#8FD6A0>" + potionHealPercent + "%</color>를 회복한다.\n몇 번이든 다시 살 수 있다.";
        potion.buy = BuyPotion;
        slots.Add(potion);

        // ② 능력치 카드
        for (int i = 0; i < offers.Count; i++)
        {
            StatOffer offer = offers[i];
            Slot slot = BuildSlot(left + step * col++, offer.icon, StatName(offer.kind), offer.price);
            slot.tipTitle = StatName(offer.kind);
            slot.tipBody = "<color=#8FD6A0>" + StatEffectText(offer) + "</color>\n이번 판 내내 남는다. 한 장만 있다.";
            StatOffer captured = offer;
            Slot capturedSlot = slot;
            slot.statOffer = offer;
            slot.icon = offer.icon;
            slot.buy = delegate { return BuyStat(captured, capturedSlot); };
            slots.Add(slot);
        }

        // ③ 특성 카드 (있을 때만)
        if (trait != null)
        {
            Slot slot = BuildTraitSlot(left + step * col++, trait);
            slots.Add(slot);
        }

        // ④ 도박
        Slot gamble = BuildSlot(left + step * col, gambleIcon, "주사위", gambleStake);
        gamble.tipTitle = "주사위";
        gamble.tipBody = "판돈 " + gambleStake + " 골드를 건다.\n"
            + "<color=#9AA0AE>실패 " + Mathf.RoundToInt(failChance) + "%</color>  ·  "
            + "1.5배 " + Mathf.RoundToInt(x15Chance) + "%  ·  "
            + "2배 " + Mathf.RoundToInt(x2Chance) + "%  ·  "
            + "<color=#E7C46B>특성 카드 " + Mathf.RoundToInt(traitChance) + "%</color>";
        gamble.buy = Gamble;
        slots.Add(gamble);
    }

    // 이번 진열에 특성 카드가 낄지. 끼면 어떤 카드인지까지 정한다
    private AbilityCard RollTraitOffer()
    {
        if (Random.value > traitOfferChance) return null;

        AbilityCardLibrary library = AbilityCardLibrary.Load();
        if (library == null) return null;

        return library.Roll(traitRarityWeights);
    }

    private int TraitPrice(AbilityRarity rarity)
    {
        int i = (int)rarity;
        if (traitPrices != null && i >= 0 && i < traitPrices.Length) return traitPrices[i];
        return 100;
    }

    // 특성 카드 칸은 진짜 카드(AbilityCardView)를 그대로 세워 둔다 —
    // 상점에서 본 그림과 나중에 능력 목록에서 볼 그림이 달라지면 같은 카드로 안 읽힌다.
    private Slot BuildTraitSlot(float x, AbilityCard card)
    {
        var slot = new Slot { cost = TraitPrice(card.Rarity) };

        RectTransform holder = UIFactory.Empty("Slot_특성_" + card.DisplayName, row);
        holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
        holder.sizeDelta = CardSize;
        holder.anchoredPosition = new Vector2(x, 0f);
        slot.root = holder;

        RectTransform cardSlot = UIFactory.Empty("View", holder);
        UIFactory.SetAnchoredBox(cardSlot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 뷰가 알아서 등급 프레임·아이콘·이름을 그리고, 마우스를 올리면 설명 툴팁까지 띄운다
        var view = cardSlot.gameObject.AddComponent<AbilityCardView>();
        view.Build(card, tooltip, font, slotFill, 6f, skin);

        slot.price = UIFactory.Label("Price", holder, font, 20, goldText, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(slot.price.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, -40f), new Vector2(0f, -8f));
        slot.price.text = slot.cost + " G";
        AddShadow(slot.price);

        slot.dim = UIFactory.Panel("Dim", holder, new Color(0f, 0f, 0f, 0f), false);
        UIFactory.SetAnchoredBox(slot.dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        AbilityCard captured = card;
        Slot capturedSlot = slot;
        slot.traitCard = card;
        slot.icon = card.Icon;
        slot.buy = delegate { return BuyTrait(captured, capturedSlot); };

        var relay = view.gameObject.AddComponent<ClickRelay>();
        relay.onClick = delegate { TryBuy(capturedSlot); };

        return slot;
    }

    private bool BuyTrait(AbilityCard card, Slot slot)
    {
        if (!playerStatus.SpendGold(slot.cost)) return false;

        if (AbilityInventory.Instance != null) AbilityInventory.Instance.Add(card);

        slot.soldOut = true;
        ShowResult(card.DisplayName + " 획득", card.Rarity.Color());
        return true;
    }

    // 칸 수가 바뀌면 창도 같이 넓어져야 카드가 테두리를 밟지 않는다
    private void ResizeFrame(int columns)
    {
        if (frameRect == null) return;

        float width = columns * CardSize.x + (columns - 1) * CardGap + 120f;
        frameRect.sizeDelta = new Vector2(Mathf.Max(640f, width), HeaderHeight + CardSize.y + FooterHeight);
    }

    private Slot BuildSlot(float x, Sprite icon, string name, int price)
    {
        var slot = new Slot { cost = price };

        RectTransform holder = UIFactory.Empty("Slot_" + name, row);
        holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
        holder.sizeDelta = CardSize;
        holder.anchoredPosition = new Vector2(x, 0f);
        slot.root = holder;

        // 카드 그림의 아치 창은 뚫려 있다 — 프레임보다 뒤에 바탕을 깔아야 속이 안 비친다
        Sprite frameSprite = skin != null ? skin.Frame(AbilityRarity.Common) : null;
        slot.window = UIFactory.Panel("Window", holder, new Color(0.07f, 0.06f, 0.09f, 1f), false);
        UIFactory.SetAnchoredBox(slot.window.rectTransform, WindowMin, WindowMax, Vector2.zero, Vector2.zero);

        Image card = UIFactory.Panel("Card", holder, Color.white, true);
        if (frameSprite != null) card.sprite = frameSprite;
        else card.color = slotFill;
        UIFactory.SetAnchoredBox(card.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        if (icon != null)
        {
            Image img = UIFactory.Panel("Icon", holder, Color.white, false);
            img.sprite = icon;
            img.preserveAspect = true;
            Vector2 center = (WindowMin + WindowMax) * 0.5f;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = center;
            // 22px 아이콘을 창(약 88px) 안에 들어가는 가장 큰 정수배로
            float target = 88f;
            float size = Mathf.Max(icon.rect.width, icon.rect.height);
            float k = size <= target ? Mathf.Floor(target / size) : target / size;
            img.rectTransform.sizeDelta = new Vector2(icon.rect.width, icon.rect.height) * k;
            img.rectTransform.anchoredPosition = Vector2.zero;
        }

        slot.name = UIFactory.Label("Name", holder, font, 14,
            new Color(0.24f, 0.16f, 0.10f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(slot.name.rectTransform, NameMin, NameMax, Vector2.zero, Vector2.zero);
        slot.name.text = name;

        // 가격은 카드 밑에 (양피지 이름칸이 좁아 둘 다 넣으면 글자가 겹친다)
        slot.price = UIFactory.Label("Price", holder, font, 20, goldText, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(slot.price.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, -40f), new Vector2(0f, -8f));
        slot.price.text = price + " G";
        AddShadow(slot.price);

        // 못 사거나 품절일 때 덮는 막
        slot.dim = UIFactory.Panel("Dim", holder, new Color(0f, 0f, 0f, 0f), false);
        UIFactory.SetAnchoredBox(slot.dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var relay = card.gameObject.AddComponent<ClickRelay>();
        Slot captured = slot;
        relay.onClick = delegate { TryBuy(captured); };

        var hover = card.gameObject.AddComponent<HoverRelay>();
        hover.onEnter = delegate { if (tooltip != null) tooltip.Show(captured.tipTitle, goldText, captured.tipBody, ""); };
        hover.onExit = delegate { if (tooltip != null) tooltip.Hide(); };

        return slot;
    }

    // ─────────────────────────────────────────────
    // 사기
    // ─────────────────────────────────────────────

    private void TryBuy(Slot slot)
    {
        if (slot == null || slot.soldOut || playerStatus == null) return;

        if (playerStatus.CurrentGold < slot.cost)
        {
            ShowResult("골드가 부족하다", dimText);
            return;
        }

        if (slot.buy != null) slot.buy();
        Refresh();
    }

    private bool BuyPotion()
    {
        if (!playerStatus.SpendGold(potionPrice)) return false;

        int heal = Mathf.Max(1, Mathf.RoundToInt(playerStatus.MaxHp * potionHealPercent * 0.01f));
        int before = playerStatus.CurrentHp;
        playerStatus.Heal(heal);
        int gained = playerStatus.CurrentHp - before;

        ShowResult(gained > 0 ? "체력을 " + gained + " 회복했다" : "이미 가득 찼다", new Color(0.56f, 0.84f, 0.63f));
        return true;
    }

    private bool BuyStat(StatOffer offer, Slot slot)
    {
        if (!playerStatus.SpendGold(offer.price)) return false;

        ApplyStat(offer);
        slot.soldOut = true;
        ShowResult(StatEffectText(offer), new Color(0.56f, 0.84f, 0.63f));
        return true;
    }

    private void ApplyStat(StatOffer offer)
    {
        PlayerStatus status = playerStatus;
        if (status == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            status = p != null ? p.GetComponent<PlayerStatus>() : null;
        }
        if (status == null) return;

        CharacterStats stats = status.Stats;
        switch (offer.kind)
        {
            case StatKind.Attack:
                stats.baseAttackPower += Mathf.RoundToInt(offer.amount);
                break;
            case StatKind.CritRate:
                stats.bonusCritRate += offer.amount;
                break;
            case StatKind.MoveSpeed:
                // 카드에 적힌 5는 5%를 뜻한다 (이동속도 원본값이 5라 그대로 더하면 두 배가 된다)
                stats.bonusMoveSpeed += offer.amount * 0.01f;
                break;
            case StatKind.MaxHp:
                // 최대 체력이 오르면 PlayerStatus가 늘어난 만큼 현재 체력도 같이 올린다
                stats.baseHp += Mathf.RoundToInt(offer.amount);
                break;
        }
    }

    private bool Gamble()
    {
        if (!playerStatus.SpendGold(gambleStake)) return false;

        float total = Mathf.Max(0.0001f, failChance + x15Chance + x2Chance + traitChance);
        float roll = Random.value * total;

        if (roll < failChance)
        {
            ShowResult("주사위가 굴렀다 — 실패. " + gambleStake + " 골드를 잃었다", new Color(0.78f, 0.42f, 0.42f));
            return true;
        }
        roll -= failChance;

        if (roll < x15Chance)
        {
            int won = Mathf.RoundToInt(gambleStake * 1.5f);
            playerStatus.AddGold(won);
            ShowResult("1.5배! " + won + " 골드", goldText);
            return true;
        }
        roll -= x15Chance;

        if (roll < x2Chance)
        {
            int won = gambleStake * 2;
            playerStatus.AddGold(won);
            ShowResult("2배! " + won + " 골드", goldText);
            return true;
        }

        // 특성 카드 — 상점을 닫아야 선택 화면이 뜬다 (둘 다 시간을 멈추므로 겹치면 안 된다)
        if (TraitLevelUp.Instance != null)
        {
            TraitLevelUp.Instance.GrantPick();
            ShowResult("특성 카드다!", new Color(0.91f, 0.77f, 0.42f));
            StartCoroutine(CloseAfter(1.1f));
        }
        else
        {
            int won = gambleStake * 2;
            playerStatus.AddGold(won);
            ShowResult("2배! " + won + " 골드", goldText);
        }
        return true;
    }

    private IEnumerator CloseAfter(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end) yield return null;
        Close();
    }

    private void ShowResult(string text, Color color)
    {
        if (resultLabel == null) return;
        resultLabel.text = text;
        resultLabel.color = color;
    }

    private void Refresh()
    {
        if (playerStatus == null) return;

        int gold = playerStatus.CurrentGold;
        if (goldLabel != null) goldLabel.text = gold.ToString();

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            bool canAfford = gold >= slot.cost;

            if (slot.soldOut)
            {
                slot.dim.color = new Color(0f, 0f, 0f, 0.65f);
                slot.price.text = "품절";
                slot.price.color = dimText;
            }
            else
            {
                slot.dim.color = canAfford ? new Color(0f, 0f, 0f, 0f) : new Color(0f, 0f, 0f, 0.45f);
                slot.price.text = slot.cost + " G";
                slot.price.color = canAfford ? goldText : dimText;
            }
        }
    }

    private static string StatName(StatKind kind)
    {
        switch (kind)
        {
            case StatKind.Attack: return "공격력";
            case StatKind.CritRate: return "치명타";
            case StatKind.MoveSpeed: return "이동속도";
            default: return "체력";
        }
    }

    private static string StatEffectText(StatOffer offer)
    {
        switch (offer.kind)
        {
            case StatKind.Attack: return "공격력 +" + Mathf.RoundToInt(offer.amount);
            case StatKind.CritRate: return "치명타 확률 +" + offer.amount + "%";
            case StatKind.MoveSpeed: return "이동속도 +" + offer.amount + "%";
            default: return "최대 체력 +" + Mathf.RoundToInt(offer.amount);
        }
    }
}
