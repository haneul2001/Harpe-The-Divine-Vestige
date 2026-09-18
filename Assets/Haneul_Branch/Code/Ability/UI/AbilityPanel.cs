using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 능력 카드 패널(인벤토리). I로 열고 닫는다.
//
// 역할은 "조립과 입력"까지만이다.
//  · 무슨 카드를 갖고 있는지  → AbilityInventory
//  · 세트가 몇 단계 열렸는지  → AbilitySetCalculator
//  · 카드/아이콘을 어떻게 그릴지 → AbilityCardView / AbilitySetIconView
//  · 툴팁                    → TooltipView
// 여기는 그것들을 화면에 배치하고 열고 닫기만 한다.
//
// UI는 런타임에 코드로 만든다 (미니맵과 같은 방식). 씬에 미리 만들어 둘 것이 없다.
public class AbilityPanel : MonoBehaviour
{
    [Header("입력")]
    [Tooltip("ESC는 일시정지 메뉴가 쓰므로 능력 패널은 I를 쓴다")]
    [SerializeField] private KeyCode toggleKey = KeyCode.I;
    [Tooltip("열려 있는 동안 게임을 멈춘다")]
    [SerializeField] private bool pauseWhileOpen = true;

    [Header("배치")]
    [Tooltip("한 줄에 놓을 카드 수")]
    [Min(1)]
    [SerializeField] private int columns = 4;
    [Tooltip("항상 깔아 두는 최소 칸 수. 카드는 첫 칸부터 얻은 순서대로 채워진다")]
    [Min(1)]
    [SerializeField] private int minSlots = 8;
    [Tooltip("카드 크기. 픽셀 카드 그림(100x155)의 정수배여야 픽셀이 안 뭉개진다")]
    [SerializeField] private Vector2 cardSize = new Vector2(200f, 310f);
    [SerializeField] private Vector2 cardSpacing = new Vector2(20f, 24f);
    [Tooltip("마우스를 올렸을 때 카드가 떠오르는 높이(px)")]
    [SerializeField] private float cardHoverLift = 10f;
    [Tooltip("왼쪽 세트 칸의 너비(px)")]
    [SerializeField] private float setColumnWidth = 150f;
    [Tooltip("세트 슬롯 크기. 세트 아이콘(64px)이 1배로 들어가는 크기")]
    [SerializeField] private float setIconSize = 80f;

    [Header("색")]
    [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.78f);
    [SerializeField] private Color panelFill = new Color(0.07f, 0.07f, 0.10f, 0.98f);
    [SerializeField] private Color panelBorder = new Color(0.62f, 0.55f, 0.40f, 0.9f);
    [SerializeField] private Color slotFill = new Color(0.12f, 0.12f, 0.16f, 1f);
    [SerializeField] private Color headerColor = new Color(0.92f, 0.86f, 0.70f);
    [SerializeField] private Color dimText = new Color(0.55f, 0.56f, 0.62f);

    [Header("그림 (비우면 전부 단색으로 그림)")]
    [Tooltip("카드 프레임·패널 배경 등을 그림으로 갈아끼우는 묶음.\n" +
             "비워 두면 지금처럼 단색 사각형으로 그려지고, 채우는 만큼만 바뀐다.")]
    [SerializeField] private AbilityUISkin skin;

    [Header("폰트 (비우면 씬에서 가져옴)")]
    [SerializeField] private Font font;

    public bool IsOpen { get; private set; }

    // 픽셀 패널 테두리(8px × 2배) 안쪽 여백
    private const float Pad = 32f;
    private const float HeaderHeight = 92f;

    private Canvas canvas;
    private RectTransform canvasRect;
    private GameObject rootGo;
    private RectTransform cardContent;
    private RectTransform setContent;
    private TooltipView tooltip;
    private Text countText;
    private Text noSetText;

    private readonly List<AbilityCardView> cardViews = new List<AbilityCardView>();
    private bool dirty = true;
    private float previousTimeScale = 1f;

    private void Start()
    {
        BuildUI();
        Close();

        if (AbilityInventory.Instance != null)
            AbilityInventory.Instance.Changed += MarkDirty;
    }

    private void OnDestroy()
    {
        if (AbilityInventory.Instance != null)
            AbilityInventory.Instance.Changed -= MarkDirty;
    }

    private void MarkDirty()
    {
        dirty = true;
        if (IsOpen) Refresh();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (IsOpen) Close();
            else Open();
        }
    }

    // ─────────────────────────────────────────────
    // 열기 / 닫기
    // ─────────────────────────────────────────────

    public void Open()
    {
        // 켜고 나서 채운다. 꺼진 오브젝트는 레이아웃 계산이 돌지 않아
        // 그리드가 자리를 못 잡고 카드가 한 점에 뭉친다.
        rootGo.SetActive(true);

        if (dirty) Refresh();

        IsOpen = true;

        if (pauseWhileOpen)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        SetPlayerInputLocked(true);
    }

    public void Close()
    {
        if (rootGo != null) rootGo.SetActive(false);
        IsOpen = false;

        if (tooltip != null) tooltip.Hide();

        if (pauseWhileOpen)
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
    // 조립 (한 번만)
    // ─────────────────────────────────────────────

    private void BuildUI()
    {
        // 미니맵(100)보다 위에 떠야 한다
        GameObject canvasGo = new GameObject("AbilityCanvas", typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRect = canvasGo.GetComponent<RectTransform>();

        rootGo = UIFactory.Stretch("Root", canvasGo.transform).gameObject;

        // 배경 — 뒤쪽 클릭을 먹어 게임 화면과 분리한다
        UIFactory.Panel("Backdrop", rootGo.transform, backdropColor).rectTransform
            .CopyStretchFrom(rootGo.GetComponent<RectTransform>());

        // 본체 창 — 픽셀 패널 그림이 있으면 한 장으로, 없으면 테두리+본체 단색
        RectTransform frame;
        RectTransform inner;
        Sprite panelSprite = skin.PanelBackground();
        if (panelSprite != null)
        {
            frame = UIFactory.PixelImage("Frame", rootGo.transform, panelSprite, skin.PixelScale(), true).rectTransform;
            inner = frame;
        }
        else
        {
            Image frameFill;
            frame = UIFactory.BorderedPanel("Frame", rootGo.transform, panelFill, panelBorder, 3f, out frameFill);
            inner = (RectTransform)frame.GetChild(0);
        }
        UIFactory.SetAnchoredBox(frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // 카드 두 줄이 스크롤 없이 보이는 높이
        float gridWidth = columns * cardSize.x + (columns - 1) * cardSpacing.x;
        float gridHeight = 2f * cardSize.y + cardSpacing.y + GridPadTop + GridPadBottom;
        frame.sizeDelta = new Vector2(
            Pad + setColumnWidth + ColumnGap + gridWidth + Pad,
            HeaderHeight + gridHeight + Pad);

        BuildHeader(inner);
        BuildSetColumn(inner);
        BuildCardArea(inner);

        // 툴팁은 항상 맨 위
        GameObject tipGo = new GameObject("TooltipView");
        tipGo.transform.SetParent(rootGo.transform, false);
        tooltip = tipGo.AddComponent<TooltipView>();
        tooltip.Build(canvasRect, font, 420f,
            new Color(0.05f, 0.05f, 0.08f, 0.97f), panelBorder, new Vector2(20f, -14f), skin);
        tipGo.transform.SetAsLastSibling();
    }

    private const float ColumnGap = 20f;
    private const float GridPadTop = 14f;     // 호버로 떠오른 첫 줄이 잘리지 않게
    private const float GridPadBottom = 6f;

    private void BuildHeader(RectTransform parent)
    {
        // 제목 — 픽셀 제목 띠를 윗변에 걸친다
        RectTransform titleBox;
        if (skin.TitlePlate() != null)
        {
            titleBox = UIFactory.PixelImage("TitlePlate", parent, skin.TitlePlate(), skin.PixelScale()).rectTransform;
            UIFactory.SetAnchoredBox(titleBox, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-130f, -26f), new Vector2(130f, 22f));
        }
        else
        {
            titleBox = UIFactory.Empty("TitlePlate", parent);
            UIFactory.SetAnchoredBox(titleBox, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-130f, -56f), new Vector2(130f, -16f));
        }

        Text title = UIFactory.Label("Header", titleBox, font, 28, headerColor,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 2f), Vector2.zero);
        title.text = "능력";
        AddShadow(title);

        // 왼쪽: 보유 장수 / 오른쪽: 조작 안내 — 제목 띠와 안 겹치게 아래 줄에 둔다
        countText = UIFactory.Label("Count", parent, font, 18, dimText, TextAnchor.MiddleLeft);
        UIFactory.SetAnchoredBox(countText.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f),
            new Vector2(Pad, -HeaderHeight + 12f), new Vector2(0f, -HeaderHeight + 44f));

        Text hint = UIFactory.Label("Hint", parent, font, 18, dimText, TextAnchor.MiddleRight);
        UIFactory.SetAnchoredBox(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -HeaderHeight + 12f), new Vector2(-Pad, -HeaderHeight + 44f));
        hint.text = toggleKey + " 닫기  ·  휠 스크롤";
    }

    private static void AddShadow(Text t)
    {
        var sh = t.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
        sh.effectDistance = new Vector2(2f, -2f);
    }

    private void BuildSetColumn(RectTransform parent)
    {
        RectTransform column;
        if (skin.SetColumnBackground() != null)
        {
            column = UIFactory.PixelImage("SetColumn", parent, skin.SetColumnBackground(), skin.PixelScale()).rectTransform;
        }
        else
        {
            column = UIFactory.Empty("SetColumn", parent);
            Image bg = column.gameObject.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.035f);
            bg.raycastTarget = false;
        }
        UIFactory.SetAnchoredBox(column, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(Pad, Pad), new Vector2(Pad + setColumnWidth, -HeaderHeight));

        Text label = UIFactory.Label("SetHeader", column, font, 18, headerColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(16f, -50f), new Vector2(-16f, -18f));   // 기둥 테두리(8px×2) 안쪽
        label.text = "세트 효과";

        setContent = UIFactory.Stretch("Content", column);
        setContent.offsetMin = new Vector2(0f, 16f);
        setContent.offsetMax = new Vector2(0f, -56f);

        var layout = setContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        noSetText = UIFactory.Label("NoSet", column, font, 15, dimText, TextAnchor.UpperCenter);
        UIFactory.SetAnchoredBox(noSetText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(10f, -110f), new Vector2(-10f, -60f));
        noSetText.text = "없음";
    }

    private void BuildCardArea(RectTransform parent)
    {
        // 뷰포트 — 여기를 벗어난 카드는 잘린다
        RectTransform viewport = UIFactory.Empty("Viewport", parent);
        UIFactory.SetAnchoredBox(viewport, new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(Pad + setColumnWidth + ColumnGap, Pad), new Vector2(-Pad, -HeaderHeight));

        viewport.gameObject.AddComponent<RectMask2D>();
        // ScrollRect가 드래그/휠을 받으려면 레이캐스트 대상이 필요하다 (투명해도 됨)
        Image blocker = viewport.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0f);

        cardContent = UIFactory.Empty("Content", viewport);
        cardContent.anchorMin = new Vector2(0f, 1f);
        cardContent.anchorMax = new Vector2(1f, 1f);
        cardContent.pivot = new Vector2(0.5f, 1f);
        cardContent.offsetMin = new Vector2(0f, 0f);
        cardContent.offsetMax = new Vector2(0f, 0f);

        var grid = cardContent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = cardSize;
        grid.spacing = cardSpacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        // 왼쪽 위부터 채운다 — 가운데 정렬이면 카드가 1~3장일 때 첫 칸이 아니라 가운데에 뜬다
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.padding = new RectOffset(0, 0, (int)GridPadTop, (int)GridPadBottom);

        var fitter = cardContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = cardContent;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 42f;
        scroll.inertia = false;   // 픽셀아트 UI에서 관성은 미끄러워 보인다
    }

    // ─────────────────────────────────────────────
    // 내용 채우기
    // ─────────────────────────────────────────────

    private void Refresh()
    {
        dirty = false;

        AbilityInventory inv = AbilityInventory.Instance;
        IReadOnlyList<AbilityCard> owned = inv != null ? inv.Owned : new List<AbilityCard>();

        RebuildCards(owned);
        RebuildSets(owned);
    }

    private void RebuildCards(IReadOnlyList<AbilityCard> owned)
    {
        for (int i = cardContent.childCount - 1; i >= 0; i--)
        {
            // Destroy는 프레임 끝에 지워지므로, 그 전에 레이아웃에서 먼저 빼 둔다
            GameObject old = cardContent.GetChild(i).gameObject;
            old.transform.SetParent(null, false);
            Destroy(old);
        }
        cardViews.Clear();

        int cardCount = 0;
        for (int i = 0; i < owned.Count; i++) if (owned[i] != null) cardCount++;

        // 칸 수: 최소 칸 수 이상, 줄 단위로 맞춘다. 얻은 순서(AbilityInventory 목록 순서) 그대로 첫 칸부터.
        int rows = Mathf.CeilToInt(Mathf.Max(cardCount, minSlots) / (float)columns);
        int slotCount = rows * columns;

        int filled = 0;
        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i] == null) continue;

            RectTransform slot = UIFactory.Empty("Slot_" + filled, cardContent);
            AbilityCardView view = slot.gameObject.AddComponent<AbilityCardView>();
            view.Build(owned[i], tooltip, font, slotFill, cardHoverLift, skin);
            cardViews.Add(view);
            filled++;
        }

        for (int i = filled; i < slotCount; i++)
            BuildEmptySlot(i);

        countText.text = "보유 " + cardCount + "장";

        // 카드가 늘어난 만큼 스크롤 영역 높이를 즉시 반영한다
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardContent);
    }

    private void BuildEmptySlot(int index)
    {
        RectTransform slot = UIFactory.Empty("Slot_" + index, cardContent);

        Image img;
        if (skin.EmptyCardSlot() != null)
        {
            img = UIFactory.PixelImage("Empty", slot, skin.EmptyCardSlot(), skin.PixelScale());
            img.color = new Color(1f, 1f, 1f, 0.55f);
        }
        else
        {
            img = UIFactory.Panel("Empty", slot, new Color(1f, 1f, 1f, 0.04f), false);
        }
        UIFactory.SetAnchoredBox(img.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private void RebuildSets(IReadOnlyList<AbilityCard> owned)
    {
        for (int i = setContent.childCount - 1; i >= 0; i--)
            Destroy(setContent.GetChild(i).gameObject);

        List<AbilitySetProgress> sets = AbilitySetCalculator.Collect(owned);
        noSetText.gameObject.SetActive(sets.Count == 0);

        for (int i = 0; i < sets.Count; i++)
        {
            RectTransform slot = UIFactory.Empty("Set_" + i, setContent);
            slot.sizeDelta = new Vector2(setIconSize, setIconSize);

            var le = slot.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = setIconSize;
            le.preferredHeight = setIconSize;

            AbilitySetIconView view = slot.gameObject.AddComponent<AbilitySetIconView>();
            view.Build(sets[i], tooltip, font, slotFill, skin);
        }
    }
}

// 배경처럼 부모를 그대로 덮어야 하는 경우가 반복돼 확장 메서드로 뺐다
public static class RectTransformStretchExtensions
{
    public static void CopyStretchFrom(this RectTransform rt, RectTransform source)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
