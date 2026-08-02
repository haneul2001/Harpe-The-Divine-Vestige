using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 아이작식 미니맵. 방문한 방과 그 이웃만 표시한다.
// Canvas 아래 빈 오브젝트에 붙이고 RectTransform 위치를 잡으면 끝 —
// 칸은 런타임에 코드로 생성하므로 미리 만들어 둘 UI가 없다.
[RequireComponent(typeof(RectTransform))]
public class DungeonMinimap : MonoBehaviour
{
    [Header("칸 크기")]
    [SerializeField] private float cellSize = 14f;
    [SerializeField] private float cellGap = 2f;

    [Header("색")]
    [SerializeField] private Color currentColor  = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Color clearedColor  = new Color(0.55f, 0.58f, 0.68f, 0.8f);
    [SerializeField] private Color visitedColor  = new Color(0.35f, 0.37f, 0.45f, 0.8f);
    [SerializeField] private Color unknownColor  = new Color(0.18f, 0.18f, 0.22f, 0.55f);
    [SerializeField] private Color bossColor     = new Color(0.78f, 0.25f, 0.28f, 0.9f);
    [SerializeField] private Color treasureColor = new Color(0.92f, 0.76f, 0.30f, 0.9f);

    private RoomManager manager;
    private RectTransform rect;
    private readonly Dictionary<Room, Image> cells = new Dictionary<Room, Image>();
    private Vector2Int origin;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
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

    private void Build()
    {
        foreach (Transform child in rect) Destroy(child.gameObject);
        cells.Clear();

        IReadOnlyList<Room> rooms = manager.AllRooms;
        if (rooms == null || rooms.Count == 0) return;

        // 시작 방을 (0,0)으로 두고 상대 좌표로 그린다
        origin = manager.Current != null ? manager.Current.GridPos : rooms[0].GridPos;

        float step = cellSize + cellGap;

        for (int i = 0; i < rooms.Count; i++)
        {
            Room r = rooms[i];
            if (r == null) continue;

            GameObject go = new GameObject("Cell_" + r.GridPos.x + "_" + r.GridPos.y, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(rect, false);
            rt.sizeDelta = new Vector2(cellSize, cellSize);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(
                (r.GridPos.x - origin.x) * step,
                (r.GridPos.y - origin.y) * step);

            Image img = go.AddComponent<Image>();
            img.raycastTarget = false;

            cells[r] = img;
        }

        Refresh();
    }

    private void Refresh()
    {
        foreach (KeyValuePair<Room, Image> kv in cells)
        {
            Room r = kv.Key;
            Image img = kv.Value;
            if (r == null || img == null) continue;

            bool known = r.Visited || IsNextToVisited(r);
            img.enabled = known;
            if (!known) continue;

            if (r == manager.Current)          img.color = currentColor;
            else if (!r.Visited)               img.color = unknownColor;
            else if (r.type == RoomType.Boss)  img.color = bossColor;
            else if (r.type == RoomType.Treasure) img.color = treasureColor;
            else if (r.IsCleared)              img.color = clearedColor;
            else                               img.color = visitedColor;
        }
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
        // 방 클리어는 폴링으로 판정되므로 색도 가볍게 따라 갱신
        if (manager != null && cells.Count > 0) Refresh();
    }
}
