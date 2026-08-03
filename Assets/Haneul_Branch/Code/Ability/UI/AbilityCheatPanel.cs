using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 개발용 치트 패널. 프로젝트의 모든 능력 카드를 나열하고 클릭으로 켜고 끈다.
//
// 인벤토리를 직접 조작하므로 결과는 즉시 반영된다 —
// AbilityInventory.Changed → AbilityEffectRunner가 효과 재적용,
//                          → AbilityPanel이 목록 갱신.
// 이 클래스는 그 연쇄를 전혀 모르고 Add/Remove만 부른다.
//
// 카드 목록은 에디터에서 프로젝트 전체를 훑어 자동으로 채운다.
// 빌드에서도 쓰려면 cards 목록을 인스펙터에 직접 채우면 된다.
public class AbilityCheatPanel : MonoBehaviour
{
    [Header("입력")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;

    [Header("카드 목록 (비우면 에디터에서 자동 수집)")]
    [SerializeField] private List<AbilityCard> cards = new List<AbilityCard>();

    [Header("배치")]
    [Min(1)]
    [SerializeField] private int columns = 5;
    [SerializeField] private Vector2 cardSize = new Vector2(150f, 200f);
    [SerializeField] private Vector2 cardSpacing = new Vector2(16f, 34f);

    [Header("색")]
    [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.85f);
    [SerializeField] private Color panelFill = new Color(0.06f, 0.07f, 0.06f, 0.98f);
    [SerializeField] private Color panelBorder = new Color(0.45f, 0.72f, 0.45f, 0.9f);
    [SerializeField] private Color slotFill = new Color(0.12f, 0.13f, 0.12f, 1f);
    [SerializeField] private Color onColor = new Color(0.42f, 0.86f, 0.48f);
    [SerializeField] private Color offColor = new Color(0.45f, 0.46f, 0.50f);
    [Tooltip("보유하지 않은 카드를 덮는 어두운 막")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.62f);

    [SerializeField] private AbilityUISkin skin;
    [SerializeField] private Font font;

    public bool IsOpen { get; private set; }

    private GameObject rootGo;
    private RectTransform content;
    private TooltipView tooltip;
    private Text counterText;

    // 카드별 표시 요소 — 토글할 때 이것만 갱신하면 되므로 전체 재생성이 필요 없다
    private class Entry
    {
        public AbilityCard card;
        public Image dim;
        public Text state;
    }
    private readonly List<Entry> entries = new List<Entry>();

    private float previousTimeScale = 1f;

    private void Start()
    {
        CollectCardsIfEmpty();
        BuildUI();
        Close();
    }

    private void OnDestroy()
    {
        if (AbilityInventory.Instance != null)
            AbilityInventory.Instance.Changed -= RefreshStates;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (IsOpen) Close();
            else Open();
        }
    }

    // 프로젝트의 모든 AbilityCard를 긁어온다.
    // 카드를 추가할 때마다 목록을 손으로 관리하면 반드시 빠뜨린다.
    private void CollectCardsIfEmpty()
    {
        if (cards.Count > 0) return;

#if UNITY_EDITOR
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:AbilityCard"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            AbilityCard c = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityCard>(path);
            if (c != null) cards.Add(c);
        }
        cards.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
#else
        Debug.LogWarning("[AbilityCheatPanel] 빌드에서는 cards 목록을 인스펙터에 채워야 한다", this);
#endif
    }

    // ─────────────────────────────────────────────
    // 열기 / 닫기
    // ─────────────────────────────────────────────

    public void Open()
    {
        rootGo.SetActive(true);
        IsOpen = true;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        SetPlayerInputLocked(true);
        RefreshStates();
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
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;

        PlayerMove move = p.GetComponent<PlayerMove>();
        if (move != null) move.inputLocked = locked;
    }

    // ─────────────────────────────────────────────
    // 조립
    // ─────────────────────────────────────────────

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("CheatCanvas", typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;   // 능력 패널(200)보다 위

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        rootGo = UIFactory.Stretch("Root", canvasGo.transform).gameObject;

        Image backdrop = UIFactory.Panel("Backdrop", rootGo.transform, backdropColor);
        backdrop.rectTransform.anchorMin = Vector2.zero;
        backdrop.rectTransform.anchorMax = Vector2.one;
        backdrop.rectTransform.offsetMin = Vector2.zero;
        backdrop.rectTransform.offsetMax = Vector2.zero;

        Image frameFill;
        RectTransform frame = UIFactory.BorderedPanel("Frame", rootGo.transform,
            panelFill, panelBorder, 3f, out frameFill);
        UIFactory.SetAnchoredBox(frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        frame.sizeDelta = new Vector2(
            columns * cardSize.x + (columns - 1) * cardSpacing.x + 80f, 880f);

        RectTransform inner = (RectTransform)frame.GetChild(0);
        BuildHeader(inner);
        BuildGrid(inner);

        GameObject tipGo = new GameObject("TooltipView");
        tipGo.transform.SetParent(rootGo.transform, false);
        tooltip = tipGo.AddComponent<TooltipView>();
        tooltip.Build(canvasRect, font, 420f,
            new Color(0.04f, 0.06f, 0.04f, 0.97f), panelBorder, new Vector2(20f, -14f), skin);
        tipGo.transform.SetAsLastSibling();

        if (AbilityInventory.Instance != null)
            AbilityInventory.Instance.Changed += RefreshStates;
    }

    private void BuildHeader(RectTransform parent)
    {
        Text title = UIFactory.Label("Header", parent, font, 34, onColor,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.SetAnchoredBox(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(26f, -64f), new Vector2(-26f, -18f));
        title.text = "치트 · 능력 카드";

        counterText = UIFactory.Label("Counter", parent, font, 20,
            new Color(0.6f, 0.62f, 0.6f), TextAnchor.MiddleRight);
        UIFactory.SetAnchoredBox(counterText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(26f, -64f), new Vector2(-26f, -18f));

        Image line = UIFactory.Panel("Divider", parent, new Color(1f, 1f, 1f, 0.10f), false);
        UIFactory.SetAnchoredBox(line.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(22f, -68f), new Vector2(-22f, -66f));

        Text hint = UIFactory.Label("Hint", parent, font, 18,
            new Color(0.5f, 0.52f, 0.5f), TextAnchor.MiddleCenter);
        UIFactory.SetAnchoredBox(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(22f, 6f), new Vector2(-22f, 30f));
        hint.text = "카드를 클릭해 켜고 끈다 · F1 닫기";
    }

    private void BuildGrid(RectTransform parent)
    {
        RectTransform viewport = UIFactory.Empty("Viewport", parent);
        UIFactory.SetAnchoredBox(viewport, Vector2.zero, Vector2.one,
            new Vector2(22f, 34f), new Vector2(-22f, -76f));
        viewport.gameObject.AddComponent<RectMask2D>();
        Image blocker = viewport.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0f);

        content = UIFactory.Empty("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = cardSize;
        grid.spacing = cardSpacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.padding = new RectOffset(0, 0, 6, 10);

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 42f;
        scroll.inertia = false;

        for (int i = 0; i < cards.Count; i++)
            BuildEntry(cards[i], i);

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private void BuildEntry(AbilityCard card, int index)
    {
        if (card == null) return;

        RectTransform slot = UIFactory.Empty("Cheat_" + index, content);

        // 능력 패널과 같은 카드 뷰를 그대로 재사용한다
        AbilityCardView view = slot.gameObject.AddComponent<AbilityCardView>();
        view.Build(card, tooltip, font, slotFill, 6f, skin);

        // 보유하지 않은 카드를 덮는 막 (카드 위에 얹혀야 하므로 마지막에 추가)
        Image dim = UIFactory.Panel("Dim", slot, dimColor, false);
        dim.rectTransform.anchorMin = Vector2.zero;
        dim.rectTransform.anchorMax = Vector2.one;
        dim.rectTransform.offsetMin = Vector2.zero;
        dim.rectTransform.offsetMax = Vector2.zero;

        // ON/OFF 표시 — 카드 아래에 붙인다
        Text state = UIFactory.Label("State", slot, font, 20, offColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(state.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, -28f), new Vector2(0f, -4f));

        var relay = slot.gameObject.AddComponent<ClickRelay>();
        AbilityCard captured = card;
        relay.onClick = () => Toggle(captured);

        entries.Add(new Entry { card = card, dim = dim, state = state });
    }

    // ─────────────────────────────────────────────
    // 토글 / 상태 반영
    // ─────────────────────────────────────────────

    private void Toggle(AbilityCard card)
    {
        AbilityInventory inv = AbilityInventory.Instance;
        if (inv == null)
        {
            Debug.LogWarning("[AbilityCheatPanel] AbilityInventory를 찾을 수 없다", this);
            return;
        }

        // Add/Remove가 Changed를 쏘고, 그걸 구독한 러너와 능력 패널이 알아서 따라온다
        if (Owns(inv, card)) inv.Remove(card);
        else inv.Add(card);
    }

    private static bool Owns(AbilityInventory inv, AbilityCard card)
    {
        IReadOnlyList<AbilityCard> owned = inv.Owned;
        for (int i = 0; i < owned.Count; i++)
            if (owned[i] == card) return true;
        return false;
    }

    private void RefreshStates()
    {
        AbilityInventory inv = AbilityInventory.Instance;
        int on = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            bool owns = inv != null && Owns(inv, e.card);
            if (owns) on++;

            e.dim.enabled = !owns;
            e.state.text = owns ? "ON" : "OFF";
            e.state.color = owns ? onColor : offColor;
        }

        if (counterText != null)
            counterText.text = on + " / " + entries.Count + " 보유";
    }
}
