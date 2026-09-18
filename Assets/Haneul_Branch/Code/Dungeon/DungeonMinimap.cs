using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 아이작식 미니맵. 방문한 방과 그 이웃만 표시한다.
//
// Canvas 아래 빈 오브젝트에 붙이고 RectTransform 위치만 잡으면 끝 —
// 배경 패널·칸·문 연결선·플레이어 마커까지 전부 런타임에 코드로 만든다.
//
// Tab을 누르면 화면 중앙으로 나와 크게 펼쳐지고, 뒤로 어두운 막이 깔린다.
[RequireComponent(typeof(RectTransform))]
public class DungeonMinimap : MonoBehaviour
{
    [Header("칸 크기")]
    [SerializeField] private float cellSize = 17f;
    [SerializeField] private float cellGap = 5f;
    [Tooltip("방과 방을 잇는 문 표시의 두께")]
    [SerializeField] private float linkThickness = 5f;

    [Header("배경 패널")]
    [Tooltip("칸 바깥으로 남기는 여백")]
    [SerializeField] private float panelPadding = 10f;
    [SerializeField] private Color panelColor = new Color(0.05f, 0.05f, 0.08f, 0.82f);
    [SerializeField] private Color panelBorderColor = new Color(0.62f, 0.58f, 0.50f, 0.85f);
    [SerializeField] private float panelBorderWidth = 2f;

    [Header("픽셀 스킨 (2D Pixel Quest Vol.3) — 비우면 아래 색 사각형으로 그린다")]
    [SerializeField] private Sprite panelSprite;
    [Tooltip("일반 방 칸")]
    [SerializeField] private Sprite cellSprite;
    [Tooltip("지금 있는 방 칸")]
    [SerializeField] private Sprite currentCellSprite;
    [Tooltip("특수 방 아이콘은 방문 전이라도 옆방까지 오면 바로 보인다 (아이작 방식)")]
    [SerializeField] private Sprite bossIcon;
    [SerializeField] private Sprite shopIcon;
    [SerializeField] private Sprite treasureIcon;

    [Header("표시")]
    [Tooltip("접힌 상태의 표시 배율. 2면 칸·테두리 픽셀이 다른 UI(2배)와 같은 굵기가 된다")]
    [SerializeField] private float displayScale = 2f;
    [Tooltip("접힌 상태의 불투명도 (펼치면 1로 올라간다)")]
    [Range(0f, 1f)]
    [SerializeField] private float opacity = 0.7f;
    [Tooltip("화면 우상단 모서리와 패널 사이 여백 (UI 단위, 0이면 완전히 붙는다)")]
    [SerializeField] private float cornerMargin = 8f;

    [Header("펼치기 (Tab)")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [Tooltip("펼쳤을 때의 표시 배율 (절대값 — displayScale에 곱하지 않는다)")]
    [SerializeField] private float expandedScale = 4f;
    [Tooltip("펼치고 접히는 데 걸리는 시간(초)")]
    [SerializeField] private float expandDuration = 0.16f;
    [Tooltip("펼쳤을 때 뒤에 깔리는 어두운 막의 진하기. 0이면 막 없음")]
    [SerializeField] private float backdropAlpha = 0.72f;

    [Header("색")]
    [SerializeField] private Color currentColor  = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color clearedColor  = new Color(0.42f, 0.46f, 0.56f, 0.95f);
    [SerializeField] private Color visitedColor  = new Color(0.66f, 0.70f, 0.80f, 0.95f);
    [SerializeField] private Color unknownColor  = new Color(0.20f, 0.21f, 0.26f, 0.9f);
    [SerializeField] private Color bossColor     = new Color(0.85f, 0.22f, 0.25f, 1f);
    [Tooltip("보물방 표시색. 상점과 겹치지 않도록 노란색 계열에서 살짝 비켜 둔다")]
    [SerializeField] private Color treasureColor = new Color(0.45f, 0.85f, 0.62f, 1f);
    [Tooltip("상점 표시색 — 노란색")]
    [SerializeField] private Color shopColor     = new Color(0.96f, 0.78f, 0.28f, 1f);
    [SerializeField] private Color linkColor     = new Color(0.45f, 0.47f, 0.55f, 0.9f);

    [Header("플레이어 마커")]
    [SerializeField] private Color playerColor = new Color(0.35f, 1f, 0.95f, 1f);
    [SerializeField] private float playerMarkerSize = 6f;
    [Tooltip("마커가 깜빡이는 속도. 0이면 고정")]
    [SerializeField] private float playerPulseSpeed = 5f;

    private RoomManager manager;
    private RectTransform rect;

    private readonly Dictionary<Room, Image> cells = new Dictionary<Room, Image>();

    // 문 연결선. 양쪽 방이 모두 보일 때만 그리므로 방 참조를 같이 들고 있는다.
    private class Link
    {
        public Image image;
        public Room a;
        public Room b;
    }
    private readonly List<Link> links = new List<Link>();
    private RectTransform panel;
    private RectTransform marker;
    private RectTransform backdrop;
    private Vector2Int origin;

    // 접힌 상태의 원래 자리 — 펼쳤다 돌아올 목적지
    private Vector2 collapsedPos;
    private Vector3 collapsedScale;
    private Vector2 expandedPos;

    private bool expanded;
    private float expandT;   // 0=접힘, 1=펼침

    private CanvasGroup group;
    private Vector2 contentShift;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();

        // 화면 우상단 모서리에 붙인다
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        collapsedPos = new Vector2(-cornerMargin, -cornerMargin);
        rect.anchoredPosition = collapsedPos;
        collapsedScale = Vector3.one * displayScale;
        rect.localScale = collapsedScale;

        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = opacity;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void Start()
    {
        manager = RoomManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[DungeonMinimap] RoomManager를 찾을 수 없음");
            enabled = false;
            return;
        }

        CreateBackdrop();

        // 던전은 이미 생성된 뒤일 수 있으므로 즉시 한 번 만들고, 이후 이벤트로 갱신
        Build();
        manager.OnDungeonReady += Build;
        manager.OnRoomChanged += OnRoomChanged;
    }

    private void OnDestroy()
    {
        if (manager == null) return;
        manager.OnDungeonReady -= Build;
        manager.OnRoomChanged -= OnRoomChanged;
    }

    private void OnRoomChanged(Room room)
    {
        Refresh();
    }

    // ─────────────────────────────────────────────
    // 만들기
    // ─────────────────────────────────────────────

    // 펼쳤을 때 화면을 덮는 어두운 막.
    // 미니맵의 자식으로 두면 같이 확대돼 버리므로 캔버스 바로 아래 형제로 만들고 맨 뒤로 보낸다.
    private void CreateBackdrop()
    {
        if (backdropAlpha <= 0f) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        GameObject go = new GameObject("MinimapBackdrop", typeof(RectTransform));
        backdrop = go.GetComponent<RectTransform>();
        backdrop.SetParent(canvas.transform, false);
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.offsetMin = Vector2.zero;
        backdrop.offsetMax = Vector2.zero;
        backdrop.SetAsFirstSibling();   // 미니맵보다 뒤에

        Image img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = false;
    }

    private Image NewImage(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private void Build()
    {
        foreach (Transform child in rect) Destroy(child.gameObject);
        cells.Clear();
        links.Clear();
        panel = null;
        marker = null;

        IReadOnlyList<Room> rooms = manager.AllRooms;
        if (rooms == null || rooms.Count == 0) return;

        // 방 전체의 한가운데를 기준점으로 잡는다.
        // 현재 방을 기준으로 하면 방을 옮길 때마다 지도 전체가 흔들린다.
        Vector2Int min = rooms[0].GridPos;
        Vector2Int max = rooms[0].GridPos;
        for (int i = 1; i < rooms.Count; i++)
        {
            if (rooms[i] == null) continue;
            min = Vector2Int.Min(min, rooms[i].GridPos);
            max = Vector2Int.Max(max, rooms[i].GridPos);
        }
        origin = new Vector2Int((min.x + max.x) / 2, (min.y + max.y) / 2);

        // 배경 패널을 먼저 (맨 뒤에 깔려야 하므로)
        float step = cellSize + cellGap;
        Vector2 spanCells = new Vector2(max.x - min.x + 1, max.y - min.y + 1);
        Vector2 content = new Vector2(spanCells.x * step - cellGap, spanCells.y * step - cellGap);
        Vector2 panelSize = content + Vector2.one * (panelPadding * 2f);

        // 기준점이 정수 반올림이라 실제 내용 중심과 반 칸까지 어긋날 수 있다. 그만큼 패널을 밀어 준다.
        Vector2 contentCenter = new Vector2(
            ((min.x + max.x) * 0.5f - origin.x) * step,
            ((min.y + max.y) * 0.5f - origin.y) * step);

        // 내용 중심을 이 RectTransform의 중심으로 옮기고, 사각형 크기를 패널에 딱 맞춘다.
        // 그래야 우상단 피벗 기준으로 놓았을 때 패널 모서리가 화면 모서리에 정확히 붙는다
        // (던전 모양마다 패널 크기가 달라서 고정 크기 사각형으로는 여백이 들쭉날쭉해진다).
        contentShift = -contentCenter;
        Vector2 outerSize = panelSprite != null ? panelSize : panelSize + Vector2.one * (panelBorderWidth * 2f);
        rect.sizeDelta = outerSize;
        contentCenter = Vector2.zero;

        if (panelSprite != null)
        {
            // 픽셀 패널은 그림에 테두리가 들어 있으므로 한 장으로 끝난다
            Image bg = NewImage("Panel", rect, panelSize, contentCenter, Color.white);
            bg.sprite = panelSprite;
            bg.type = panelSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            panel = bg.rectTransform;
        }
        else
        {
            Image border = NewImage("PanelBorder", rect,
                panelSize + Vector2.one * (panelBorderWidth * 2f), contentCenter, panelBorderColor);
            panel = NewImage("Panel", rect, panelSize, contentCenter, panelColor).rectTransform;
            border.transform.SetAsFirstSibling();
        }

        // 문 연결선 — 칸보다 뒤에 깔려야 깔끔하다
        BuildLinks(rooms, step);

        // 방 칸
        for (int i = 0; i < rooms.Count; i++)
        {
            Room r = rooms[i];
            if (r == null) continue;

            Image img = NewImage("Cell_" + r.GridPos.x + "_" + r.GridPos.y, rect,
                Vector2.one * cellSize, CellPos(r.GridPos, step), Color.white);
            cells[r] = img;
        }

        // 플레이어 마커는 맨 위
        marker = NewImage("PlayerMarker", rect,
            Vector2.one * playerMarkerSize, Vector2.zero, playerColor).rectTransform;

        Refresh();
    }

    private void BuildLinks(IReadOnlyList<Room> rooms, float step)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            Room r = rooms[i];
            if (r == null) continue;

            IReadOnlyList<Door> doors = r.Doors;
            for (int d = 0; d < doors.Count; d++)
            {
                Door door = doors[d];
                if (door == null || !door.IsConnected) continue;

                // 오른쪽/위쪽만 그려 같은 연결을 두 번 그리지 않는다
                if (door.dir != Dir.Right && door.dir != Dir.Up) continue;

                Room nb = door.Linked.OwnerRoom;
                if (nb == null) continue;

                Vector2 a = CellPos(r.GridPos, step);
                Vector2 b = CellPos(nb.GridPos, step);
                Vector2 mid = (a + b) * 0.5f;

                Vector2 size = door.dir == Dir.Right
                    ? new Vector2(cellGap + 2f, linkThickness)
                    : new Vector2(linkThickness, cellGap + 2f);

                Image img = NewImage("Link", rect, size, mid, linkColor);
                links.Add(new Link { image = img, a = r, b = nb });
            }
        }
    }

    private Vector2 CellPos(Vector2Int grid, float step)
    {
        return new Vector2((grid.x - origin.x) * step, (grid.y - origin.y) * step) + contentShift;
    }

    // ─────────────────────────────────────────────
    // 갱신
    // ─────────────────────────────────────────────

    private void Refresh()
    {
        foreach (KeyValuePair<Room, Image> kv in cells)
        {
            Room r = kv.Key;
            Image img = kv.Value;
            if (r == null || img == null) continue;

            // 보스방은 못 가봤어도 랜드마크로 미리 보여준다 (탐색 여부와 무관하게)
            bool known = r.Visited || IsNextToVisited(r) || r.type == RoomType.Boss;
            img.enabled = known;
            if (!known) continue;

            if (cellSprite != null)
            {
                ApplySkinnedCell(r, img);
                continue;
            }

            if (r == manager.Current)             img.color = currentColor;
            else if (r.type == RoomType.Boss)     img.color = bossColor;
            else if (!r.Visited)                  img.color = unknownColor;
            else if (r.type == RoomType.Treasure) img.color = treasureColor;
            else if (r.type == RoomType.Shop)     img.color = shopColor;
            else if (r.IsCleared)                 img.color = clearedColor;
            else                                  img.color = visitedColor;
        }

        // 연결선은 양쪽 방이 모두 보일 때만
        for (int i = 0; i < links.Count; i++)
        {
            Link link = links[i];
            if (link.image == null) continue;

            link.image.enabled = IsCellVisible(link.a) && IsCellVisible(link.b);
        }
    }

    // 픽셀 스킨: 특수 방은 아이콘 그림 자체가 칸이 되고(방문 전에도 보임), 일반 방은 슬롯 그림에 명암만 준다
    private void ApplySkinnedCell(Room r, Image img)
    {
        Sprite icon = IconFor(r.type);
        bool isCurrent = r == manager.Current;

        Sprite want;
        Color tint;
        if (icon != null)
        {
            want = icon;
            // 가 본 적 없는 특수 방은 살짝 어둡게 — 위치는 알지만 아직 안 가봤다는 표시
            tint = r.Visited || isCurrent ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
        }
        else if (isCurrent)
        {
            want = currentCellSprite != null ? currentCellSprite : cellSprite;
            tint = Color.white;
        }
        else if (!r.Visited)
        {
            want = cellSprite;
            tint = new Color(0.42f, 0.42f, 0.48f, 0.85f);
        }
        else
        {
            want = cellSprite;
            // 들어가 봤지만 아직 못 깬 방은 붉은 기
            tint = r.IsCleared ? Color.white : new Color(1f, 0.62f, 0.58f, 1f);
        }

        if (img.sprite != want)
        {
            img.sprite = want;
            img.type = want.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }
        img.color = tint;
    }

    private Sprite IconFor(RoomType type)
    {
        switch (type)
        {
            case RoomType.Boss:     return bossIcon;
            case RoomType.Shop:     return shopIcon;
            case RoomType.Treasure: return treasureIcon;
            default:                return null;
        }
    }

    private bool IsCellVisible(Room r)
    {
        Image img;
        return r != null && cells.TryGetValue(r, out img) && img != null && img.enabled;
    }

    // 방문한 방과 문으로 이어져 있으면 "존재는 아는" 방으로 취급 (아이작과 같은 규칙)
    private bool IsNextToVisited(Room r)
    {
        IReadOnlyList<Door> doors = r.Doors;
        for (int i = 0; i < doors.Count; i++)
        {
            Door d = doors[i];
            if (d == null || !d.IsConnected) continue;
            Room nb = d.Linked.OwnerRoom;
            if (nb != null && nb.Visited) return true;
        }
        return false;
    }

    private void Update()
    {
        if (manager == null) return;

        if (Input.GetKeyDown(toggleKey))
            expanded = !expanded;

        UpdateExpand();

        // 방 클리어는 폴링으로 판정되므로 색도 가볍게 따라 갱신
        if (cells.Count > 0) Refresh();

        UpdatePlayerMarker();
    }

    private void UpdateExpand()
    {
        // 화면 한가운데로 나오려면, 지금 앵커 기준에서 캔버스 중심이 어디인지 되짚어야 한다.
        RectTransform parentRect = rect.parent as RectTransform;
        if (parentRect != null)
        {
            Vector2 anchorCenter = (rect.anchorMin + rect.anchorMax) * 0.5f;
            Vector2 anchorInParent = new Vector2(
                (anchorCenter.x - parentRect.pivot.x) * parentRect.rect.width,
                (anchorCenter.y - parentRect.pivot.y) * parentRect.rect.height);

            // 피벗이 가운데가 아니면 그만큼 보정 (우상단 피벗이면 오른쪽 위로 치우친다)
            Vector2 pivotFix = new Vector2(
                (rect.pivot.x - 0.5f) * rect.rect.width * expandedScale,
                (rect.pivot.y - 0.5f) * rect.rect.height * expandedScale);

            expandedPos = -anchorInParent + pivotFix;
        }

        float target = expanded ? 1f : 0f;
        if (!Mathf.Approximately(expandT, target))
        {
            float speed = expandDuration > 0f ? Time.unscaledDeltaTime / expandDuration : 1f;
            expandT = Mathf.MoveTowards(expandT, target, speed);
        }

        float e = Mathf.SmoothStep(0f, 1f, expandT);

        rect.anchoredPosition = Vector2.Lerp(collapsedPos, expandedPos, e);
        rect.localScale = Vector3.Lerp(collapsedScale, Vector3.one * expandedScale, e);
        if (group != null) group.alpha = Mathf.Lerp(opacity, 1f, e);

        if (backdrop != null)
        {
            Image img = backdrop.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, backdropAlpha * e);
            img.enabled = e > 0.001f;
        }
    }

    private void UpdatePlayerMarker()
    {
        if (marker == null) return;

        Room cur = manager.Current;
        if (cur == null || !cells.ContainsKey(cur))
        {
            marker.gameObject.SetActive(false);
            return;
        }

        marker.gameObject.SetActive(true);

        // 방 안 세부 위치는 따라가지 않고, 지금 있는 방 칸의 한가운데에만 찍는다
        float step = cellSize + cellGap;
        marker.anchoredPosition = CellPos(cur.GridPos, step);

        // 흰 칸 위의 흰 점은 안 보이므로 깜빡여서 눈에 띄게 한다
        if (playerPulseSpeed > 0f)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * playerPulseSpeed);
            marker.localScale = Vector3.one * pulse;
        }
    }
}
