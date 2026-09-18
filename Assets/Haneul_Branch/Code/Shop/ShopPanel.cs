using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 상점 NPC 한 마리가 통째로 담당한다: 범위 감지 → 상호작용 → 패널 UI → 가챠 뽑기.
//
// 능력은 목록에서 골라 사는 게 아니라 소울을 내고 무작위로 뽑는다(가챠).
// 카드 프레임은 AbilityCardView/CardFrameBuilder를 그대로 재사용한다 —
// 능력 패널과 뽑기 결과가 다른 그림으로 보이면 "같은 카드"라는 게 안 읽힌다.
public class ShopPanel : MonoBehaviour
{
    [Header("상호작용")]
    [SerializeField] private float interactRange = 1.8f;
    [SerializeField] private KeyCode interactKey = KeyCode.V;
    [Tooltip("상인 발밑에서 V 표시까지의 높이 (월드 단위 — 상인 스케일과 무관)")]
    [SerializeField] private float promptHeight = 1.5f;
    [Tooltip("V 표시 키캡 한 변 크기 (월드 단위)")]
    [SerializeField] private float promptSize = 0.5f;

    [Header("뽑기 풀 (비우면 프로젝트의 모든 AbilityCard를 자동으로 채운다)")]
    [SerializeField] private List<AbilityCard> pool = new List<AbilityCard>();

    [Header("뽑기 비용 · 확률")]
    [Tooltip("1회 뽑기 비용(소울). CharacterStats.TrySpendSoul을 타므로 INT 할인이 그대로 적용된다")]
    [SerializeField] private int pullCost = 50;
    [SerializeField] private float commonWeight = 60f;
    [SerializeField] private float rareWeight = 27f;
    [SerializeField] private float epicWeight = 10f;
    [SerializeField] private float legendaryWeight = 3f;

    [Header("배치")]
    [Tooltip("카드 원본(100x155)의 정수배로 둔다 — 비정수배면 픽셀이 뭉개진다")]
    [SerializeField] private Vector2 cardSize = new Vector2(200f, 310f);

    [Header("픽셀 UI (2D Pixel Quest Vol.3)")]
    [Tooltip("그림 1픽셀을 UI 몇 칸으로 그릴지. 카드(2배)와 맞춰야 테두리 두께가 한 세트로 보인다")]
    [SerializeField] private float pixelScale = 2f;
    [Tooltip("뽑기 버튼 그림. 비우면 예전처럼 단색 사각형으로 그려진다")]
    [SerializeField] private Sprite pullButtonSprite;
    [Tooltip("상단 제목 띠")]
    [SerializeField] private Sprite titlePlateSprite;
    [Tooltip("소울 표시 받침")]
    [SerializeField] private Sprite badgePlateSprite;
    [SerializeField] private Sprite soulIcon;
    [Tooltip("뽑기 전 자리에 엎어 두는 카드 뒷면")]
    [SerializeField] private Sprite cardBackSprite;
    [Tooltip("머리 위 V 표시 뒤에 까는 키캡 그림")]
    [SerializeField] private Sprite promptKeySprite;

    [Header("색 (사신 톤 — AbilityCheatPanel과 같은 값)")]
    [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.8f);
    [SerializeField] private Color panelFill = new Color(0.07f, 0.07f, 0.10f, 0.98f);
    [SerializeField] private Color panelBorder = new Color(0.66f, 0.58f, 0.39f, 0.9f);
    [SerializeField] private Color slotFill = new Color(0.11f, 0.11f, 0.14f, 1f);
    [SerializeField] private Color goldText = new Color(0.93f, 0.83f, 0.56f);
    [SerializeField] private Color dimText = new Color(0.49f, 0.51f, 0.55f);
    [SerializeField] private Color soulColor = new Color(0.56f, 0.48f, 0.94f);

    [SerializeField] private Font font;

    [Tooltip("비워 두면 등급색 단색 폴백으로 그려진다 (AbilityCardView와 동일)")]
    [SerializeField] private AbilityUISkin skin;

    public bool IsOpen { get; private set; }

    private Transform player;
    private PlayerStatus playerStatus;
    private bool inRange;
    private GameObject prompt;

    private GameObject rootGo;
    private RectTransform revealHolder;
    private Text soulText;
    private Text pullButtonLabel;
    private Image pullButtonFill;
    private ClickRelay pullButton;
    private TooltipView tooltip;

    private float previousTimeScale = 1f;

    private void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerStatus = p.GetComponent<PlayerStatus>();
        }

        CollectPoolIfEmpty();
        BuildPrompt();
        BuildUI();
        Close();
    }

    // 프로젝트의 모든 AbilityCard를 긁어온다 — 카드가 늘 때마다 목록을 손으로 관리하면 반드시 빠뜨린다.
    private void CollectPoolIfEmpty()
    {
        if (pool.Count > 0) return;

#if UNITY_EDITOR
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:AbilityCard"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            AbilityCard c = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityCard>(path);
            if (c != null) pool.Add(c);
        }
#else
        Debug.LogWarning("[ShopPanel] 빌드에서는 pool 목록을 인스펙터에 채워야 한다", this);
#endif
    }

    private void Update()
    {
        if (player == null) return;

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
        rootGo.SetActive(true);
        IsOpen = true;

        if (prompt != null) prompt.SetActive(false);

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        SetPlayerInputLocked(true);
        RefreshSoul();
    }

    public void Close()
    {
        if (rootGo != null) rootGo.SetActive(false);
        IsOpen = false;

        if (tooltip != null) tooltip.Hide();

        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        SetPlayerInputLocked(false);
    }

    private void SetPlayerInputLocked(bool locked)
    {
        if (player == null) return;
        PlayerMove move = player.GetComponent<PlayerMove>();
        if (move != null) move.inputLocked = locked;
    }

    // ─────────────────────────────────────────────
    // 상호작용 프롬프트 (범위 안에 들어오면 머리 위에 뜨는 표시)
    // ─────────────────────────────────────────────

    private void BuildPrompt()
    {
        // 상인 프리팹은 3배 스케일이라 자식으로 그냥 붙이면 표시도 3배로 커진다.
        // 부모 스케일을 되돌려 놓고 크기·높이는 월드 단위로 잡는다.
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
        // 글자 높이를 키캡의 절반 정도로 — TextMesh 한 줄 높이는 대략 fontSize × characterSize × 0.1
        tm.characterSize = promptSize * 0.55f / (tm.fontSize * 0.1f);
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = goldText;

        var mr = label.GetComponent<MeshRenderer>();
        // UI와 같은 갈무리 굵은체. TextMesh는 폰트 재질까지 같이 바꿔 줘야 글자가 보인다
        Font keyFont = UIFactory.ResolveBoldFont();
        if (keyFont != null)
        {
            tm.font = keyFont;
            mr.sharedMaterial = keyFont.material;
            tm.fontSize = 48;   // 12의 배수
        }
        else
        {
            tm.fontStyle = FontStyle.Bold;
        }
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

        // 픽셀 패널 그림(2D Pixel Quest Vol.3)이 있으면 그걸로, 없으면 예전처럼 단색 폴백
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
        // 위: 제목·소울 줄(104) / 가운데: 카드 / 아래: 확률·버튼·안내(128)
        frame.sizeDelta = new Vector2(Mathf.Max(560f, cardSize.x + 280f), HeaderHeight + cardSize.y + FooterHeight);

        BuildHeader(inner);
        BuildBody(inner);

        GameObject tipGo = new GameObject("TooltipView");
        tipGo.transform.SetParent(rootGo.transform, false);
        tooltip = tipGo.AddComponent<TooltipView>();
        tooltip.Build(canvasRect, font, 420f, new Color(0.05f, 0.05f, 0.06f, 0.97f), panelBorder, new Vector2(20f, -14f), skin);
        tipGo.transform.SetAsLastSibling();
    }

    private const float HeaderHeight = 104f;
    private const float FooterHeight = 136f;

    private void BuildHeader(RectTransform parent)
    {
        // 제목 띠 — 패널 윗변에 반쯤 걸쳐 올린다 (픽셀 RPG 창 제목 방식)
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

        // 대사(왼쪽) · 소울 잔액(오른쪽) 한 줄
        Text line = UIFactory.Label("Line", parent, font, 12, dimText, TextAnchor.MiddleLeft);
        UIFactory.SetAnchoredBox(line.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(32f, -84f), new Vector2(-190f, -44f));
        line.text = "\"운이 따르면 귀한 것도 나오지.\"";

        RectTransform badge;
        if (badgePlateSprite != null)
            badge = PixelImage("SoulBadge", parent, badgePlateSprite).rectTransform;
        else
        {
            Image fill;
            badge = UIFactory.BorderedPanel("SoulBadge", parent, slotFill,
                new Color(soulColor.r, soulColor.g, soulColor.b, 0.55f), 2f, out fill);
        }
        UIFactory.SetAnchoredBox(badge, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-176f, -84f), new Vector2(-28f, -44f));

        if (soulIcon != null)
        {
            Image icon = UIFactory.Panel("SoulIcon", badge, Color.white, false);
            icon.sprite = soulIcon;
            UIFactory.SetAnchoredBox(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(4f, -22f), new Vector2(48f, 22f));   // 22px 아이콘 × 2
        }

        soulText = UIFactory.Label("SoulText", badge, font, 20, soulColor, TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.SetAnchoredBox(soulText.rectTransform, Vector2.zero, Vector2.one, new Vector2(48f, 0f), new Vector2(-14f, 0f));
        AddShadow(soulText);
    }

    private void BuildBody(RectTransform parent)
    {
        // 뽑기 결과가 나오는 자리 — 카드 한 장 크기
        revealHolder = UIFactory.Empty("RevealHolder", parent);
        UIFactory.SetAnchoredBox(revealHolder, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-cardSize.x * 0.5f, -(HeaderHeight + cardSize.y)), new Vector2(cardSize.x * 0.5f, -HeaderHeight));
        ShowCardBack();

        // 확률 표기 — 뭘 뽑을지 미리 알 수 있게
        Text odds = UIFactory.Label("Odds", parent, font, 15, dimText, TextAnchor.MiddleCenter);
        UIFactory.SetAnchoredBox(odds.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(32f, 100f), new Vector2(-32f, 124f));
        odds.text = FormatOdds();

        // 뽑기 버튼 — 픽셀 버튼 그림이 있으면 그걸로, 없으면 예전처럼 단색 폴백
        RectTransform btn;
        RectTransform btnContent;
        if (pullButtonSprite != null)
        {
            Image bg = PixelImage("PullButton", parent, pullButtonSprite, true);
            btn = bg.rectTransform;
            btnContent = btn;
            pullButtonFill = bg;
        }
        else
        {
            Image btnFill;
            btn = UIFactory.BorderedPanel("PullButton", parent, panelBorder, goldText, 2f, out btnFill);
            btnContent = (RectTransform)btn.GetChild(0);
            pullButtonFill = btnFill;
        }
        UIFactory.SetAnchoredBox(btn, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-120f, 44f), new Vector2(120f, 96f));

        pullButtonLabel = UIFactory.Label("Label", btnContent, font, 22, goldText, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(pullButtonLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 2f), Vector2.zero);
        AddShadow(pullButtonLabel);

        pullButton = btn.gameObject.AddComponent<ClickRelay>();
        pullButton.onClick = Pull;

        // 안내 문구는 반드시 패널 안쪽에 — 테두리(8px × pixelScale) 위로 올린다
        Text hint = UIFactory.Label("Hint", parent, font, 14, dimText, TextAnchor.MiddleCenter);
        UIFactory.SetAnchoredBox(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(32f, 20f), new Vector2(-32f, 40f));
        hint.text = "클릭해서 뽑기  ·  ESC 닫기";
    }

    // 아직 아무것도 안 뽑았을 때 가운데가 휑하지 않게 카드 뒷면을 엎어 둔다
    private void ShowCardBack()
    {
        if (cardBackSprite == null) return;

        Image back = UIFactory.Panel("CardBack", revealHolder, Color.white, false);
        back.sprite = cardBackSprite;
        UIFactory.SetAnchoredBox(back.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private string FormatOdds()
    {
        return "일반 " + commonWeight + "%  ·  레어 " + rareWeight + "%  ·  에픽 " + epicWeight + "%  ·  전설 " + legendaryWeight + "%";
    }

    // ─────────────────────────────────────────────
    // 뽑기
    // ─────────────────────────────────────────────

    private void Pull()
    {
        if (playerStatus == null) return;

        if (!playerStatus.Stats.TrySpendSoul(pullCost))
        {
            ToastManager.Show("소울이 부족하다");
            return;
        }

        AbilityCard drawn = RollCard();
        RefreshSoul();

        if (drawn == null)
        {
            Debug.LogWarning("[ShopPanel] 뽑을 카드가 없다 — pool이 비어 있는지 확인할 것", this);
            return;
        }

        AbilityInventory.Instance?.Add(drawn);
        ShowReveal(drawn);
    }

    private AbilityCard RollCard()
    {
        var byRarity = new Dictionary<AbilityRarity, List<AbilityCard>>();
        for (int i = 0; i < pool.Count; i++)
        {
            AbilityCard c = pool[i];
            if (c == null) continue;

            List<AbilityCard> list;
            if (!byRarity.TryGetValue(c.Rarity, out list))
            {
                list = new List<AbilityCard>();
                byRarity[c.Rarity] = list;
            }
            list.Add(c);
        }

        var tiers = new[]
        {
            new { rarity = AbilityRarity.Common, weight = commonWeight },
            new { rarity = AbilityRarity.Rare, weight = rareWeight },
            new { rarity = AbilityRarity.Epic, weight = epicWeight },
            new { rarity = AbilityRarity.Legendary, weight = legendaryWeight },
        };

        float total = 0f;
        foreach (var t in tiers)
            if (byRarity.ContainsKey(t.rarity)) total += t.weight;

        if (total <= 0f) return null;

        float roll = Random.value * total;
        foreach (var t in tiers)
        {
            if (!byRarity.ContainsKey(t.rarity)) continue;
            roll -= t.weight;
            if (roll <= 0f)
            {
                var list = byRarity[t.rarity];
                return list[Random.Range(0, list.Count)];
            }
        }

        // 부동소수점 오차로 못 골랐을 때의 마지막 안전장치
        foreach (var t in tiers)
        {
            if (byRarity.ContainsKey(t.rarity))
            {
                var list = byRarity[t.rarity];
                return list[Random.Range(0, list.Count)];
            }
        }
        return null;
    }

    private void ShowReveal(AbilityCard card)
    {
        if (revealHolder.childCount > 0)
            Destroy(revealHolder.GetChild(0).gameObject);

        RectTransform slot = UIFactory.Empty("RevealCard", revealHolder);
        UIFactory.SetAnchoredBox(slot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        AbilityCardView view = slot.gameObject.AddComponent<AbilityCardView>();
        view.Build(card, tooltip, font, slotFill, 6f, skin);
    }

    private void RefreshSoul()
    {
        if (playerStatus == null) return;

        if (soulText != null) soulText.text = playerStatus.CurrentSoul.ToString();

        bool canAfford = playerStatus.CurrentSoul >= pullCost;
        if (pullButtonLabel != null) pullButtonLabel.text = "뽑기 (" + pullCost + ")";
        // 그림 버튼은 흰색이 원래 색이다 (금색을 곱하면 칙칙하게 탁해진다)
        Color normal = pullButtonSprite != null ? Color.white : goldText;
        if (pullButtonFill != null) pullButtonFill.color = canAfford ? normal : new Color(0.45f, 0.45f, 0.45f, 1f);
        if (pullButtonLabel != null) pullButtonLabel.color = canAfford ? goldText : dimText;
    }
}
