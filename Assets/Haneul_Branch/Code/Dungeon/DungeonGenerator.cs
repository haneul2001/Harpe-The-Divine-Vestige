using System.Collections.Generic;
using UnityEngine;

// 아이작식 던전 생성기.
// 그리드 위에 방 좌표를 랜덤하게 뻗어 나가며 정한 뒤, 좌표마다 방 프리팹을 배치하고
// 맞닿은 면의 문끼리 연결한다.
//
// 씬에 빈 오브젝트 하나 만들어서 RoomManager와 함께 붙이면 끝.
[RequireComponent(typeof(RoomManager))]
public class DungeonGenerator : MonoBehaviour
{
    [Header("생성 규모")]
    [Tooltip("만들 방 개수 (시작 방 포함)")]
    [SerializeField] private int roomCount = 10;
    [Tooltip("방이 퍼질 수 있는 최대 그리드 범위")]
    [SerializeField] private Vector2Int gridSize = new Vector2Int(9, 8);
    [Tooltip("세로 방향 문을 쓸지. 끄면 좌우로만 이어지는 횡스크롤 구조가 됨")]
    [SerializeField] private bool allowVerticalDoors = true;

    [Header("방 크기 (유닛)")]
    [Tooltip("방 프리팹에서 크기를 자동으로 읽는다. 켜 두면 아래 roomSize는 무시된다. "
           + "생성기와 프리팹의 숫자가 어긋나 방이 겹치는 사고를 막는다.")]
    [SerializeField] private bool autoRoomSize = true;

    [Tooltip("autoRoomSize를 끈 경우에만 쓰는 값. 방 프리팹의 실제 크기와 일치시킬 것")]
    [SerializeField] private Vector2 roomSize = new Vector2(32f, 18f);

    // 미니맵이 방 안에서의 플레이어 상대 위치를 계산할 때 쓴다.
    public Vector2 RoomSize { get { return roomSize; } }

    [Header("랜덤 시드")]
    [SerializeField] private bool useRandomSeed = true;
    [Tooltip("useRandomSeed를 끄면 이 값으로 항상 같은 던전이 나옴 (디버깅용)")]
    [SerializeField] private int seed = 12345;

    [Header("방 프리팹")]
    [SerializeField] private Room startRoomPrefab;
    [SerializeField] private Room bossRoomPrefab;
    [SerializeField] private Room treasureRoomPrefab;
    [SerializeField] private Room shopRoomPrefab;
    [Tooltip("일반 방 후보들. 필요한 문 방향을 가진 프리팹 중에서 랜덤으로 뽑는다")]
    [SerializeField] private List<Room> normalRoomPrefabs = new List<Room>();

    [Header("일반 방 자동 수집")]
    [Tooltip("Resources 아래 폴더에서 일반 방 프리팹을 전부 불러온다. "
           + "켜 두면 위 normalRoomPrefabs 목록을 손대지 않아도 방을 추가할 수 있다 — "
           + "폴더에 프리팹을 넣기만 하면 된다.")]
    [SerializeField] private bool loadRoomsFromResources = true;

    [Tooltip("Resources 기준 경로. 예: \"Rooms\" → Assets/*/Resources/Rooms")]
    [SerializeField] private string roomsResourcePath = "Rooms";

    [Header("동작")]
    [SerializeField] private bool generateOnStart = true;

    private RoomManager roomManager;
    private readonly Dictionary<Vector2Int, RoomType> layout = new Dictionary<Vector2Int, RoomType>();
    private readonly Dictionary<Vector2Int, Room> placed = new Dictionary<Vector2Int, Room>();
    private Vector2Int startCell;

    private void Awake()
    {
        roomManager = GetComponent<RoomManager>();
    }

    private void Start()
    {
        if (generateOnStart) Generate();
    }

    // ─────────────────────────────────────────────
    // 진입점
    // ─────────────────────────────────────────────

    public void Generate()
    {
        LoadRoomsFromResources();

        if (!ValidatePrefabs()) return;

        // 방 크기는 프리팹이 정답이다. 생성기 인스펙터 값이 낡아 있으면 방이 겹치거나 벌어진다.
        if (autoRoomSize && startRoomPrefab != null)
        {
            Vector2 fromPrefab = startRoomPrefab.Size;
            if (fromPrefab.x > 0.1f && fromPrefab.y > 0.1f) roomSize = fromPrefab;
        }

        Random.InitState(useRandomSeed ? System.Environment.TickCount : seed);

        ClearExisting();
        BuildLayout();
        AssignSpecialRooms();
        InstantiateRooms();
        LinkDoors();

        Room startRoom = placed.TryGetValue(startCell, out Room r) ? r : null;
        roomManager.Initialize(new List<Room>(placed.Values), startRoom);

        Debug.Log($"[DungeonGenerator] 방 {placed.Count}개 생성 완료");
    }

    // 폴더에 프리팹을 넣는 것만으로 방이 늘어나게 한다.
    // 씬의 인스펙터 목록을 매번 갱신하는 것보다 방을 대량으로 만들 때 훨씬 낫다.
    private void LoadRoomsFromResources()
    {
        if (!loadRoomsFromResources || string.IsNullOrEmpty(roomsResourcePath)) return;

        Room[] found = Resources.LoadAll<Room>(roomsResourcePath);
        if (found == null || found.Length == 0)
        {
            Debug.LogWarning($"[DungeonGenerator] Resources/{roomsResourcePath} 에서 방을 찾지 못함. 인스펙터 목록을 그대로 쓴다");
            return;
        }

        normalRoomPrefabs = new List<Room>(found);
        Debug.Log($"[DungeonGenerator] Resources/{roomsResourcePath} 에서 일반 방 {found.Length}개 로드");
    }

    private bool ValidatePrefabs()
    {
        if (startRoomPrefab == null)
        {
            Debug.LogError("[DungeonGenerator] startRoomPrefab이 비어 있음");
            return false;
        }
        if (normalRoomPrefabs == null || normalRoomPrefabs.Count == 0)
        {
            Debug.LogError("[DungeonGenerator] normalRoomPrefabs가 비어 있음");
            return false;
        }
        return true;
    }

    private void ClearExisting()
    {
        foreach (Room r in placed.Values)
        {
            if (r != null) Destroy(r.gameObject);
        }
        placed.Clear();
        layout.Clear();
    }

    // ─────────────────────────────────────────────
    // 1단계 — 그리드 위에서 방 좌표 결정
    // ─────────────────────────────────────────────

    private void BuildLayout()
    {
        startCell = new Vector2Int(gridSize.x / 2, gridSize.y / 2);
        layout[startCell] = RoomType.Start;

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        frontier.Enqueue(startCell);

        int target = Mathf.Max(1, roomCount);
        int guard = 0;   // 무한 루프 방지

        while (layout.Count < target && guard++ < 10000)
        {
            if (frontier.Count == 0)
            {
                // 더 뻗을 곳이 없으면 기존 방들을 다시 후보로 넣고 재시도
                foreach (Vector2Int c in layout.Keys) frontier.Enqueue(c);
                if (frontier.Count == 0) break;
            }

            Vector2Int cell = frontier.Dequeue();

            foreach (Dir dir in Shuffled(DirUtil.All))
            {
                if (layout.Count >= target) break;
                if (!allowVerticalDoors && dir.IsVertical()) continue;

                Vector2Int next = cell + dir.Offset();

                if (!InBounds(next)) continue;
                if (layout.ContainsKey(next)) continue;

                // 이웃이 2개 이상이면 건너뛴다 → 고리(loop) 없는 나무 구조가 되어
                // 문 연결이 단순해지고 아이작 특유의 가지 뻗은 맵 모양이 나온다.
                if (NeighborCount(next) > 1) continue;

                // 50% 확률로 스킵 → 매번 다른 모양
                if (Random.value < 0.5f) continue;

                layout[next] = RoomType.Normal;
                frontier.Enqueue(next);
            }
        }
    }

    private bool InBounds(Vector2Int c)
    {
        return c.x >= 0 && c.x < gridSize.x && c.y >= 0 && c.y < gridSize.y;
    }

    private int NeighborCount(Vector2Int c)
    {
        int n = 0;
        foreach (Dir d in DirUtil.All)
        {
            if (!allowVerticalDoors && d.IsVertical()) continue;
            if (layout.ContainsKey(c + d.Offset())) n++;
        }
        return n;
    }

    // ─────────────────────────────────────────────
    // 2단계 — 막다른 방에 보스/보상/상점 배치
    // ─────────────────────────────────────────────

    private void AssignSpecialRooms()
    {
        // 시작 방에서의 거리 계산 (BFS)
        Dictionary<Vector2Int, int> dist = BfsDistances(startCell);

        // 막다른 방 = 이웃이 1개뿐인 방 (시작 방 제외)
        List<Vector2Int> deadEnds = new List<Vector2Int>();
        foreach (Vector2Int c in layout.Keys)
        {
            if (c == startCell) continue;
            if (NeighborCount(c) == 1) deadEnds.Add(c);
        }

        // 먼 곳부터 정렬 → 가장 먼 막다른 방이 보스
        deadEnds.Sort((a, b) =>
        {
            int da = dist.TryGetValue(a, out int va) ? va : 0;
            int db = dist.TryGetValue(b, out int vb) ? vb : 0;
            return db.CompareTo(da);
        });

        int idx = 0;

        if (bossRoomPrefab != null && idx < deadEnds.Count)
            layout[deadEnds[idx++]] = RoomType.Boss;

        if (treasureRoomPrefab != null && idx < deadEnds.Count)
            layout[deadEnds[idx++]] = RoomType.Treasure;

        if (shopRoomPrefab != null && idx < deadEnds.Count)
            layout[deadEnds[idx++]] = RoomType.Shop;

        if (bossRoomPrefab != null && deadEnds.Count == 0)
            Debug.LogWarning("[DungeonGenerator] 막다른 방이 없어 보스 방을 배치하지 못함. roomCount를 늘려보세요");
    }

    private Dictionary<Vector2Int, int> BfsDistances(Vector2Int from)
    {
        Dictionary<Vector2Int, int> dist = new Dictionary<Vector2Int, int> { [from] = 0 };
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(from);

        while (q.Count > 0)
        {
            Vector2Int c = q.Dequeue();
            foreach (Dir d in DirUtil.All)
            {
                Vector2Int n = c + d.Offset();
                if (!layout.ContainsKey(n) || dist.ContainsKey(n)) continue;
                dist[n] = dist[c] + 1;
                q.Enqueue(n);
            }
        }
        return dist;
    }

    // ─────────────────────────────────────────────
    // 3단계 — 실제 배치
    // ─────────────────────────────────────────────

    private void InstantiateRooms()
    {
        foreach (KeyValuePair<Vector2Int, RoomType> kv in layout)
        {
            Vector2Int cell = kv.Key;
            RoomType type = kv.Value;

            List<Dir> needed = RequiredDirs(cell);
            Room prefab = PickPrefab(type, needed);

            if (prefab == null)
            {
                Debug.LogError($"[DungeonGenerator] {cell} ({type}) 에 맞는 프리팹이 없음");
                continue;
            }

            Vector3 pos = CellToWorld(cell);
            Room room = Instantiate(prefab, pos, Quaternion.identity, transform);
            room.name = $"Room_{type}_{cell.x}_{cell.y}";
            room.Configure(cell);

            placed[cell] = room;
        }
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        Vector2Int rel = cell - startCell;
        return new Vector3(rel.x * roomSize.x, rel.y * roomSize.y, 0f);
    }

    private List<Dir> RequiredDirs(Vector2Int cell)
    {
        List<Dir> dirs = new List<Dir>();
        foreach (Dir d in DirUtil.All)
        {
            if (layout.ContainsKey(cell + d.Offset())) dirs.Add(d);
        }
        return dirs;
    }

    // 필요한 문 방향을 모두 가진 프리팹 중에서 랜덤으로 고른다.
    // 정확히 그 조합만 가진 방이 있으면 그쪽을 먼저 쓴다 — 아래 PickNormal 참고.
    private Room PickPrefab(RoomType type, List<Dir> needed)
    {
        switch (type)
        {
            case RoomType.Start:    return startRoomPrefab;
            case RoomType.Boss:     return bossRoomPrefab != null ? bossRoomPrefab : PickNormal(needed);
            case RoomType.Treasure: return treasureRoomPrefab != null ? treasureRoomPrefab : PickNormal(needed);
            case RoomType.Shop:     return shopRoomPrefab != null ? shopRoomPrefab : PickNormal(needed);
            default:                return PickNormal(needed);
        }
    }

    private Room PickNormal(List<Dir> needed)
    {
        // exact  : 필요한 문만 정확히 가진 방 (벽이 처음부터 제대로 그려져 있다)
        // superset: 필요한 문을 포함하되 남는 문은 벽으로 막게 되는 방
        List<Room> exact = new List<Room>();
        List<Room> superset = new List<Room>();

        for (int i = 0; i < normalRoomPrefabs.Count; i++)
        {
            Room p = normalRoomPrefabs[i];
            if (p == null) continue;

            bool hasAll = true;
            for (int d = 0; d < needed.Count; d++)
            {
                if (p.GetDoor(needed[d]) == null) { hasAll = false; break; }
            }
            if (!hasAll) continue;

            // 프리팹이 가진 문 개수를 세어 정확히 일치하는지 본다
            int owned = 0;
            foreach (Dir dd in DirUtil.All)
                if (p.GetDoor(dd) != null) owned++;

            if (owned == needed.Count) exact.Add(p);
            else superset.Add(p);
        }

        if (exact.Count > 0)
            return exact[Random.Range(0, exact.Count)];

        if (superset.Count > 0)
            return superset[Random.Range(0, superset.Count)];

        // 맞는 게 없으면 아무거나 — 문이 없는 방향은 벽으로 막혀 길이 끊긴다.
        Debug.LogWarning($"[DungeonGenerator] 문 방향 {string.Join(",", needed)} 을 모두 가진 일반 방 프리팹이 없음. " +
                         "4방향 문을 다 가진 방 프리팹을 하나 만들어 두면 안전합니다");
        return normalRoomPrefabs[Random.Range(0, normalRoomPrefabs.Count)];
    }

    // ─────────────────────────────────────────────
    // 4단계 — 문 연결
    // ─────────────────────────────────────────────

    private void LinkDoors()
    {
        foreach (KeyValuePair<Vector2Int, Room> kv in placed)
        {
            Vector2Int cell = kv.Key;
            Room room = kv.Value;

            foreach (Dir d in DirUtil.All)
            {
                Door door = room.GetDoor(d);
                if (door == null) continue;

                Vector2Int nCell = cell + d.Offset();

                if (!placed.TryGetValue(nCell, out Room neighbor))
                {
                    door.DisableAsWall();
                    continue;
                }

                Door other = neighbor.GetDoor(d.Opposite());
                if (other == null)
                {
                    // 이웃 방에 반대편 문이 없으면 통로가 성립하지 않는다
                    door.DisableAsWall();
                    continue;
                }

                door.Link(other);
                door.Open();
            }
        }
    }

    // ─────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────

    private static Dir[] Shuffled(Dir[] src)
    {
        Dir[] copy = (Dir[])src.Clone();
        for (int i = copy.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }
        return copy;
    }

    // 씬 뷰에서 그리드 배치 확인용
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.4f);
        Vector2Int center = new Vector2Int(gridSize.x / 2, gridSize.y / 2);

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                Vector2Int rel = new Vector2Int(x, y) - center;
                Vector3 p = transform.position + new Vector3(rel.x * roomSize.x, rel.y * roomSize.y, 0f);
                Gizmos.DrawWireCube(p, new Vector3(roomSize.x, roomSize.y, 0f));
            }
        }
    }
}
