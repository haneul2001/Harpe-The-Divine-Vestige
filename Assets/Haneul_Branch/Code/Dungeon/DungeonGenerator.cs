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
    [Header("층 데이터")]
    [Tooltip("층 순서 에셋. 비워 두면 Resources에서 아래 경로로 찾는다.\n"
           + "끝내 못 찾으면 이 인스펙터에 직접 물린 프리팹들로 한 층짜리 던전을 만든다.")]
    [SerializeField] private FloorSequence floors;

    [Tooltip("Resources 기준 경로. 예: \"Dungeon/FloorSequence\"")]
    [SerializeField] private string floorsResourcePath = "Dungeon/FloorSequence";

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
    [Tooltip("보스 방으로 올라가는 문에 끼우는 감옥 문 프리팹. 비우면 Resources/Dungeon/BossEntranceGate")]
    [SerializeField] private GameObject bossEntranceGate;
    private GameObject BossGate
    {
        get
        {
            if (bossEntranceGate == null) bossEntranceGate = Resources.Load<GameObject>("Dungeon/BossEntranceGate");
            return bossEntranceGate;
        }
    }

    [Header("일반 방 자동 수집")]
    [Tooltip("Resources 아래 폴더에서 일반 방 프리팹을 전부 불러온다. "
           + "켜 두면 위 normalRoomPrefabs 목록을 손대지 않아도 방을 추가할 수 있다 — "
           + "폴더에 프리팹을 넣기만 하면 된다.")]
    [SerializeField] private bool loadRoomsFromResources = true;

    [Tooltip("Resources 기준 경로. 예: \"Rooms\" → Assets/*/Resources/Rooms")]
    [SerializeField] private string roomsResourcePath = "Rooms";

    [Header("동작")]
    [SerializeField] private bool generateOnStart = true;

    [Tooltip("디버그: 0 이상이면 이 층부터 시작한다 (0 = 1층). 특정 층의 방을 바로 보고 싶을 때. 빌드 전엔 -1로")]
    [SerializeField] private int debugStartFloor = -1;

    // 지금 몇 번째 층인가. 0부터 센다(= 1층).
    public int CurrentFloorIndex { get; private set; }
    public FloorData CurrentFloor { get; private set; }

    // 층 에셋이 아예 없으면 인스펙터 설정으로 도는 한 층짜리 던전이다.
    public int FloorCount
    {
        get { return floors != null && floors.Count > 0 ? floors.Count : 1; }
    }

    public bool IsLastFloor
    {
        get { return CurrentFloorIndex >= FloorCount - 1; }
    }

    private RoomManager roomManager;
    private readonly Dictionary<Vector2Int, RoomType> layout = new Dictionary<Vector2Int, RoomType>();
    private readonly Dictionary<Vector2Int, Room> placed = new Dictionary<Vector2Int, Room>();
    private Vector2Int startCell;

    // 큰 방(여러 칸)은 "기준 칸"(왼쪽 아래) 하나에만 놓이고, 나머지 칸은 여기로 기준 칸을 가리킨다.
    // 한 칸 방은 자기 자신을 가리킨다.
    private readonly Dictionary<Vector2Int, Vector2Int> anchorOf = new Dictionary<Vector2Int, Vector2Int>();
    private readonly Dictionary<Vector2Int, Vector2Int> spanOf = new Dictionary<Vector2Int, Vector2Int>();

    // 막다른 특수 방의 유일한 입구: 기준 칸 → (입구가 있는 칸, 그 칸에서 바깥으로 나가는 방향)
    private readonly Dictionary<Vector2Int, (Vector2Int cell, Dir dir)> specialEntry = new Dictionary<Vector2Int, (Vector2Int cell, Dir dir)>();

    // cell 에서 d 방향 이웃 칸과 문으로 이어져야 하는가
    private bool Connects(Vector2Int cell, Dir d)
    {
        Vector2Int n = cell + d.Offset();
        if (!anchorOf.TryGetValue(cell, out Vector2Int a) || !anchorOf.TryGetValue(n, out Vector2Int b)) return false;
        if (a == b) return false;   // 같은 방 안

        // 어느 한쪽이 막다른 특수 방이면 기억해 둔 입구만 통한다
        if (specialEntry.TryGetValue(a, out var ea) && !(ea.cell == cell && ea.dir == d)) return false;
        if (specialEntry.TryGetValue(b, out var eb) && !(eb.cell == n && eb.dir == d.Opposite())) return false;
        return true;
    }

    [Header("큰 방")]
    [Tooltip("옆으로 나란한 일반 방 두 칸을 하나의 긴 방(2x1)으로 합칠 확률. 그 크기의 방 프리팹이 있을 때만 합친다")]
    [Range(0f, 1f)] [SerializeField] private float wideRoomChance = 0.35f;

    [Tooltip("위아래로 나란한 일반 방 두 칸을 하나의 높은 방(1x2)으로 합칠 확률")]
    [Range(0f, 1f)] [SerializeField] private float tallRoomChance = 0.35f;

    private void Awake()
    {
        roomManager = GetComponent<RoomManager>();
    }

    private void Start()
    {
        if (generateOnStart) GenerateFloor(debugStartFloor >= 0 ? debugStartFloor : 0);
    }

    // 층 하나를 짓는다. 층 에셋이 있으면 그 내용으로 자기 설정을 갈아끼운 뒤 평소대로 생성한다.
    // 생성 알고리즘은 층마다 다르지 않으므로 Generate() 아래쪽은 손대지 않는다.
    public void GenerateFloor(int index)
    {
        ResolveFloors();

        CurrentFloorIndex = Mathf.Max(0, index);
        CurrentFloor = floors != null ? floors.Get(CurrentFloorIndex) : null;

        if (CurrentFloor != null) ApplyFloor(CurrentFloor);
        else if (floors != null && floors.Count > 0)
            Debug.LogWarning("[DungeonGenerator] " + (CurrentFloorIndex + 1) + "층 데이터가 비어 있다. 인스펙터 설정으로 만든다");

        if (RunStats.Instance != null) RunStats.Instance.SetFloor(CurrentFloorIndex + 1);
        ApplyFloorTitle();

        Generate();
    }

    private void ResolveFloors()
    {
        if (floors != null || string.IsNullOrEmpty(floorsResourcePath)) return;

        floors = Resources.Load<FloorSequence>(floorsResourcePath);
        if (floors == null)
            Debug.LogWarning("[DungeonGenerator] Resources/" + floorsResourcePath + " 를 찾지 못함. 인스펙터 설정으로 한 층만 만든다");
    }

    // 에셋 값을 자기 필드로 옮긴다.
    // 생성 코드가 계속 자기 필드만 보게 두는 편이, 곳곳에서 CurrentFloor를 참조하며
    // null을 챙기는 것보다 갈래가 적다.
    private void ApplyFloor(FloorData d)
    {
        roomCount = d.roomCount;
        gridSize = d.gridSize;
        allowVerticalDoors = d.allowVerticalDoors;

        startRoomPrefab = d.startRoomPrefab;
        bossRoomPrefab = d.bossRoomPrefab;
        treasureRoomPrefab = d.treasureRoomPrefab;
        shopRoomPrefab = d.shopRoomPrefab;

        loadRoomsFromResources = d.loadRoomsFromResources;
        roomsResourcePath = d.roomsResourcePath;

        if (!d.loadRoomsFromResources && d.normalRoomPrefabs != null && d.normalRoomPrefabs.Count > 0)
            normalRoomPrefabs = new List<Room>(d.normalRoomPrefabs);
    }

    // 입장 연출에 이번 층 이름을 넘긴다. 연출은 던전이 다 만들어진 뒤에 글자를 읽으므로
    // 생성 전에 알려 두면 순서를 신경 쓸 필요가 없다.
    private void ApplyFloorTitle()
    {
        if (CurrentFloor == null) return;

        DungeonIntro intro = FindObjectOfType<DungeonIntro>();
        if (intro != null)
            intro.SetFloorInfo(CurrentFloor.displayName, CurrentFloor.ResolveSubtitle(CurrentFloorIndex + 1));
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
        MergeBigRooms();
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
        {
            // 보스 방은 아래 방에서 올라가는 자리여야 한다 — 입구 감옥 문이 아래 방의 위쪽 벽(앞면)에 서기 때문.
            // ① 아래에서 올라오는 막다른 칸이 있으면 거기, ② 없으면 가장 먼 막다른 칸 위에 보스 칸을 덧붙인다, ③ 그것도 안 되면 그냥 가장 먼 칸
            // 보스 프리팹이 여러 칸짜리면 그 자리에 실제로 들어가는지도 같이 본다 (안 맞으면 한 칸 자리에 겹쳐 놓이게 된다)
            Vector2Int bossSpan = bossRoomPrefab.CellSpan;
            int pick = deadEnds.FindIndex(c => EntryFromBelow(c) && (bossSpan == Vector2Int.one || TryReserveSpan(c, bossSpan, false)));
            if (pick >= 0)
            {
                layout[deadEnds[pick]] = RoomType.Boss;
                deadEnds.RemoveAt(pick);
            }
            else
            {
                int above = deadEnds.FindIndex(c =>
                {
                    Vector2Int q = c + Dir.Up.Offset();
                    return InBounds(q) && !layout.ContainsKey(q) && (bossSpan == Vector2Int.one || TryReserveSpan(q, bossSpan, false));
                });
                if (above >= 0)
                {
                    layout[deadEnds[above] + Dir.Up.Offset()] = RoomType.Boss;   // 그 막다른 칸은 보스로 가는 통로 방이 된다
                    deadEnds.RemoveAt(above);
                }
                else
                {
                    layout[deadEnds[0]] = RoomType.Boss;
                    deadEnds.RemoveAt(0);
                }
            }
        }

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

    // ─────────────────────────────────────────────
    // 2.5단계 — 이웃한 일반 방 두 칸을 큰 방 하나로 합친다
    // ─────────────────────────────────────────────
    //
    // 배치는 나무 구조라 이웃한 두 칸은 반드시 문으로 이어져 있다. 그 둘을 한 방으로 합쳐도
    // 바깥과의 연결은 그대로다 — 합쳐진 방의 어느 칸에서든 밖으로 나가는 문이 subCell로 구분될 뿐이다.
    private void MergeBigRooms()
    {
        anchorOf.Clear();
        spanOf.Clear();
        foreach (Vector2Int c in layout.Keys) { anchorOf[c] = c; spanOf[c] = Vector2Int.one; }

        // 보스·보물·상점은 막다른 방이다 — 들어오는 길은 배치 때 정해진 이웃 하나뿐이어야 한다.
        // 큰 방으로 커지면 다른 방들과도 맞닿게 되는데, 그쪽으로 문이 열리면 보스 방을 우회하거나
        // 보물 방이 통로가 된다. 그래서 입구(칸, 방향)를 기억해 두고 그 문만 잇는다.
        specialEntry.Clear();
        var handled = new HashSet<Vector2Int>();   // 옮겨진 특수 방을 두 번 처리하지 않게
        foreach (Vector2Int c0 in new List<Vector2Int>(layout.Keys))
        {
            if (handled.Contains(c0) || !layout.ContainsKey(c0)) continue;
            Vector2Int c = c0;
            RoomType t = layout[c];
            if (t == RoomType.Normal) continue;
            bool deadEndKind = t == RoomType.Boss || t == RoomType.Treasure || t == RoomType.Shop;

            Room prefab = null;
            switch (t)
            {
                case RoomType.Boss: prefab = bossRoomPrefab; break;
                case RoomType.Treasure: prefab = treasureRoomPrefab; break;
                case RoomType.Shop: prefab = shopRoomPrefab; break;
                case RoomType.Start: prefab = startRoomPrefab; break;
            }
            Vector2Int span = prefab != null ? prefab.CellSpan : Vector2Int.one;

            // 특수 방 프리팹이 여러 칸짜리면 그만큼 빈 칸을 함께 차지시킨다. 안 그러면 격자 한 칸 자리에
            // 40x22 방이 놓여 이웃과 겹친다. 지금 자리에 안 들어가면 들어가는 다른 막다른 칸으로 옮긴다.
            if (span != Vector2Int.one && !TryReserveSpan(c, span, false))
            {
                Vector2Int moved = c;
                // 보스는 아래에서 올라오는 막다른 칸을 먼저 찾고, 없으면 아무 막다른 칸
                for (int pass = (t == RoomType.Boss ? 0 : 1); pass < 2 && moved == c; pass++)
                    foreach (Vector2Int alt in layout.Keys)
                    {
                        if (layout[alt] != RoomType.Normal || NeighborCount(alt) != 1 || alt == startCell) continue;
                        if (pass == 0 && !EntryFromBelow(alt)) continue;
                        if (TryReserveSpan(alt, span, false)) { moved = alt; break; }
                    }
                if (moved != c)
                {
                    layout[moved] = t;
                    layout[c] = RoomType.Normal;
                    c = moved;
                }
                else Debug.LogWarning($"[DungeonGenerator] {c} {t} 방({span.x}x{span.y})을 놓을 빈 칸이 없어 한 칸 자리에 겹쳐 놓는다");
            }

            // 막다른 방의 유일한 입구는 배치 때 정해진 이웃 하나다 — 큰 방으로 커져 다른 방과 맞닿아도 그쪽은 벽이다
            Dir entryDir = Dir.Up; bool hasEntry = false;
            if (deadEndKind)
                foreach (Dir d in (t == RoomType.Boss ? BossEntryOrder : DirUtil.All))   // 보스는 아래쪽 입구 우선
                    if (layout.ContainsKey(c + d.Offset())) { entryDir = d; hasEntry = true; break; }

            Vector2Int anchor = c;
            if (span != Vector2Int.one && TryReserveSpan(c, span, true))
                anchor = anchorOf[c];

            if (hasEntry) specialEntry[anchor] = (c, entryDir);
            handled.Add(c); handled.Add(anchor);
        }

        bool hasWide = HasNormalPrefabOfSpan(new Vector2Int(2, 1));
        bool hasTall = HasNormalPrefabOfSpan(new Vector2Int(1, 2));
        if (!hasWide && !hasTall) return;

        List<Vector2Int> cells = new List<Vector2Int>(layout.Keys);
        cells.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

        foreach (Vector2Int c in cells)
        {
            if (layout[c] != RoomType.Normal || spanOf[c] != Vector2Int.one || anchorOf[c] != c) continue;

            // 가로 먼저, 안 되면 세로
            if (hasWide && Random.value < wideRoomChance && TryMerge(c, c + Vector2Int.right, new Vector2Int(2, 1))) continue;
            if (hasTall && Random.value < tallRoomChance) TryMerge(c, c + Vector2Int.up, new Vector2Int(1, 2));
        }
    }

    // 배치된 칸 cell을 포함하는 span 크기의 자리를 찾는다. cell이 방의 어느 구석이든 될 수 있으므로
    // 기준 칸(왼쪽 아래)을 옮겨 가며 나머지 칸이 전부 비어 있고 격자 안인 자리를 고른다.
    // 막다른 칸의 유일한 이웃이 바로 아래 칸인가
    private bool EntryFromBelow(Vector2Int c) => layout.ContainsKey(c + Dir.Down.Offset());
    private static readonly Dir[] BossEntryOrder = { Dir.Down, Dir.Up, Dir.Right, Dir.Left };

    private bool TryReserveSpan(Vector2Int cell, Vector2Int span, bool apply)
    {
        for (int oy = 0; oy < span.y; oy++)
            for (int ox = 0; ox < span.x; ox++)
            {
                Vector2Int anchor = cell - new Vector2Int(ox, oy);
                bool ok = true;
                for (int y = 0; y < span.y && ok; y++)
                    for (int x = 0; x < span.x && ok; x++)
                    {
                        Vector2Int q = anchor + new Vector2Int(x, y);
                        if (q == cell) continue;
                        if (!InBounds(q) || layout.ContainsKey(q) || anchorOf.ContainsKey(q)) ok = false;
                    }
                if (!ok) continue;
                if (!apply) return true;

                // cell 자체는 layout에 남기되(문·타입은 거기 있다) 방의 기준은 anchor다
                for (int y = 0; y < span.y; y++)
                    for (int x = 0; x < span.x; x++)
                    {
                        Vector2Int q = anchor + new Vector2Int(x, y);
                        anchorOf[q] = anchor;
                        spanOf[q] = span;
                    }
                if (anchor != cell)
                {
                    // 기준 칸이 layout에 없으면 InstantiateRooms가 못 본다 — 타입을 기준 칸으로 옮긴다
                    layout[anchor] = layout[cell];
                    layout.Remove(cell);
                }
                return true;
            }
        return false;
    }

    private bool TryMerge(Vector2Int a, Vector2Int b, Vector2Int span)
    {
        if (!layout.TryGetValue(b, out RoomType tb) || tb != RoomType.Normal) return false;
        if (spanOf[b] != Vector2Int.one || anchorOf[b] != b) return false;
        anchorOf[b] = a;
        spanOf[a] = span;
        spanOf[b] = span;
        return true;
    }

    private bool HasNormalPrefabOfSpan(Vector2Int span)
    {
        for (int i = 0; i < normalRoomPrefabs.Count; i++)
            if (normalRoomPrefabs[i] != null && normalRoomPrefabs[i].CellSpan == span) return true;
        return false;
    }

    // 방이 차지하는 칸들
    private IEnumerable<Vector2Int> CellsOf(Vector2Int anchor)
    {
        Vector2Int span = spanOf.TryGetValue(anchor, out Vector2Int s) ? s : Vector2Int.one;
        for (int y = 0; y < span.y; y++)
            for (int x = 0; x < span.x; x++)
                yield return anchor + new Vector2Int(x, y);
    }

    // 어떤 칸의 어떤 방향 문이 큰 방에서 몇 번째(subCell)인지
    private static int SubCellOf(Vector2Int anchor, Vector2Int cell, Dir dir)
    {
        return dir.IsVertical() ? cell.x - anchor.x : cell.y - anchor.y;
    }

    private void InstantiateRooms()
    {
        foreach (KeyValuePair<Vector2Int, RoomType> kv in layout)
        {
            Vector2Int cell = kv.Key;
            if (anchorOf.TryGetValue(cell, out Vector2Int anchor) && anchor != cell) continue;   // 큰 방의 나머지 칸
            RoomType type = kv.Value;
            Vector2Int span = spanOf.TryGetValue(cell, out Vector2Int s) ? s : Vector2Int.one;

            List<(Dir dir, int sub)> needed = RequiredDoors(cell);
            Room prefab = PickPrefab(type, needed, span);

            if (prefab == null)
            {
                Debug.LogError($"[DungeonGenerator] {cell} ({type}) 에 맞는 프리팹이 없음");
                continue;
            }

            // 큰 방은 차지하는 칸들의 한가운데에 놓는다
            Vector3 pos = CellToWorld(cell) + new Vector3((span.x - 1) * roomSize.x * 0.5f, (span.y - 1) * roomSize.y * 0.5f, 0f);
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

    // 방이 가져야 하는 문들. 방이 차지하는 칸마다, 방 밖의 이웃 칸이 있는 방향에 문이 하나씩 필요하다.
    private List<(Dir dir, int sub)> RequiredDoors(Vector2Int anchor)
    {
        var doors = new List<(Dir dir, int sub)>();
        foreach (Vector2Int cell in CellsOf(anchor))
            foreach (Dir d in DirUtil.All)
            {
                if (!Connects(cell, d)) continue;
                doors.Add((d, SubCellOf(anchor, cell, d)));
            }
        return doors;
    }

    // 필요한 문 방향을 모두 가진 프리팹 중에서 랜덤으로 고른다.
    // 정확히 그 조합만 가진 방이 있으면 그쪽을 먼저 쓴다 — 아래 PickNormal 참고.
    private Room PickPrefab(RoomType type, List<(Dir dir, int sub)> needed, Vector2Int span)
    {
        switch (type)
        {
            case RoomType.Start:    return startRoomPrefab;
            case RoomType.Boss:     return bossRoomPrefab != null ? bossRoomPrefab : PickNormal(needed, span);
            case RoomType.Treasure: return treasureRoomPrefab != null ? treasureRoomPrefab : PickNormal(needed, span);
            case RoomType.Shop:     return shopRoomPrefab != null ? shopRoomPrefab : PickNormal(needed, span);
            default:                return PickNormal(needed, span);
        }
    }

    private Room lastNormalPick;

    private Room PickAvoidingLast(List<Room> candidates)
    {
        if (candidates.Count > 1 && lastNormalPick != null)
        {
            var others = candidates.FindAll(r => r != lastNormalPick);
            if (others.Count > 0) candidates = others;
        }
        lastNormalPick = candidates[Random.Range(0, candidates.Count)];
        return lastNormalPick;
    }

    private Room PickNormal(List<(Dir dir, int sub)> needed, Vector2Int span)
    {
        // exact  : 필요한 문만 정확히 가진 방 (벽이 처음부터 제대로 그려져 있다)
        // superset: 필요한 문을 포함하되 남는 문은 벽으로 막게 되는 방
        List<Room> exact = new List<Room>();
        List<Room> superset = new List<Room>();

        for (int i = 0; i < normalRoomPrefabs.Count; i++)
        {
            Room p = normalRoomPrefabs[i];
            if (p == null || p.CellSpan != span) continue;

            bool hasAll = true;
            for (int d = 0; d < needed.Count; d++)
            {
                if (p.GetDoor(needed[d].dir, needed[d].sub) == null) { hasAll = false; break; }
            }
            if (!hasAll) continue;

            // 프리팹이 가진 문 개수를 세어 정확히 일치하는지 본다
            int owned = p.Doors.Count;

            if (owned == needed.Count) exact.Add(p);
            else superset.Add(p);
        }

        // 방금 고른 방은 이어서 또 쓰지 않는다 — 같은 방이 연달아 나오면 복붙한 티가 난다.
        // 고를 게 하나뿐이면 어쩔 수 없이 다시 쓴다.
        if (exact.Count > 0) return PickAvoidingLast(exact);
        if (superset.Count > 0) return PickAvoidingLast(superset);

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
            Vector2Int anchor = kv.Key;
            Room room = kv.Value;

            foreach (Door door in room.Doors)
            {
                if (door == null) continue;
                Dir d = door.dir;

                // 이 문이 붙어 있는 칸 → 그 방향의 이웃 칸
                // 문이 붙은 칸: 위쪽 문은 맨 윗줄, 오른쪽 문은 맨 오른쪽 열에 있다 (한 칸 방이면 전부 기준 칸)
                Vector2Int span = spanOf.TryGetValue(anchor, out Vector2Int sp) ? sp : Vector2Int.one;
                Vector2Int cell;
                switch (d)
                {
                    case Dir.Up:    cell = anchor + new Vector2Int(door.subCell, span.y - 1); break;
                    case Dir.Down:  cell = anchor + new Vector2Int(door.subCell, 0); break;
                    case Dir.Right: cell = anchor + new Vector2Int(span.x - 1, door.subCell); break;
                    default:        cell = anchor + new Vector2Int(0, door.subCell); break;
                }
                Vector2Int nCell = cell + d.Offset();

                if (!Connects(cell, d) || !anchorOf.TryGetValue(nCell, out Vector2Int nAnchor)
                    || !placed.TryGetValue(nAnchor, out Room neighbor))
                {
                    door.DisableAsWall();
                    continue;
                }

                Door other = neighbor.GetDoor(d.Opposite(), SubCellOf(nAnchor, nCell, d.Opposite()));
                if (other == null)
                {
                    // 이웃 방에 반대편 문이 없으면 통로가 성립하지 않는다
                    door.DisableAsWall();
                    continue;
                }

                door.Link(other);
                // 보스 방으로 올라가는 문은 감옥 문. 벽 그림을 깔고 그 위에 3칸짜리 문을 얹는다
                if (d == Dir.Up && neighbor.type == RoomType.Boss && BossGate != null)
                    door.SetGate(BossGate, true);
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
