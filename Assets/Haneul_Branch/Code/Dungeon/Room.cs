using System.Collections.Generic;
using UnityEngine;

// 방 프리팹 루트에 붙이는 컴포넌트.
// 방 하나 = 프리팹 하나. 생성기가 그리드 위에 배치하고, RoomManager가 하나씩 켜고 끈다.
public class Room : MonoBehaviour
{
    [System.Serializable]
    public class SpawnEntry
    {
        public GameObject prefab;
        [Tooltip("있으면 Enemy.Initialize로 HP/속도/공격력을 이 값으로 덮어씀")]
        public EnemyInfo info;
        [Min(1)] public int count = 1;
        [Tooltip("스폰 시 적용할 크기 배율. Chapter1-2처럼 잡몹 3배, 보스 5배 식으로 씀")]
        [Min(0.1f)] public float scale = 1f;
    }

    [Header("방 종류")]
    public RoomType type = RoomType.Normal;

    [Header("카메라 경계 — 이 방 안에 빈 오브젝트 4개를 두고 연결")]
    public Transform camXMin;
    public Transform camXMax;
    public Transform camYMin;
    public Transform camYMax;

    [Header("문 — 이 방이 가진 방향만 채움")]
    [SerializeField] private List<Door> doors = new List<Door>();

    [Header("적 스폰")]
    [Tooltip("적이 생성될 위치들. 비어 있으면 방 중심에 스폰")]
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("이 방에서 나올 적 목록")]
    [SerializeField] private List<SpawnEntry> spawns = new List<SpawnEntry>();
    [Tooltip("스폰된 적이 들어갈 부모. 비우면 방 루트 (스케일 1이어야 함)")]
    [SerializeField] private Transform enemyContainer;

    [Header("클리어 판정")]
    [Tooltip("적 생존 여부를 확인하는 주기(초). 이벤트 대신 폴링이라 Enemy.cs를 고칠 필요 없음")]
    [SerializeField] private float clearCheckInterval = 0.2f;

    [Header("에디터 표시")]
    [Tooltip("DungeonGenerator의 roomSize와 같은 값을 넣으면 방 크기를 맞추기 쉬움")]
    [SerializeField] private Vector2 gizmoRoomSize = new Vector2(32f, 18f);

    // 생성기가 채움
    public Vector2Int GridPos { get; private set; }

    public bool IsCleared { get; private set; }
    // 플레이어가 한 번이라도 들어온 방 (미니맵 표시용)
    public bool Visited { get; private set; }
    public bool HasEnemies => spawns != null && spawns.Count > 0;

    private readonly List<Enemy> alive = new List<Enemy>();
    private readonly List<int> spawnOrder = new List<int>();
    private int spawnCursor;
    private bool spawned;
    private float nextClearCheck;

    private void Awake()
    {
        EnsureDoors();

        if (enemyContainer == null)
            enemyContainer = transform;
    }

    // 인스펙터에서 문 목록을 안 채웠으면 자식에서 자동 수집.
    // 생성기는 프리팹 "에셋"에도 GetDoor를 호출하는데(문 방향으로 프리팹을 고르므로)
    // 에셋에는 Awake가 돌지 않기 때문에 조회 시점에도 한 번 더 확인한다.
    private void EnsureDoors()
    {
        if (doors == null) doors = new List<Door>();
        if (doors.Count > 0) return;

        doors.AddRange(GetComponentsInChildren<Door>(true));
    }

    public void Configure(Vector2Int gridPos)
    {
        GridPos = gridPos;
    }

    public Door GetDoor(Dir dir)
    {
        EnsureDoors();

        for (int i = 0; i < doors.Count; i++)
        {
            if (doors[i] != null && doors[i].dir == dir)
                return doors[i];
        }
        return null;
    }

    public IReadOnlyList<Door> Doors
    {
        get { EnsureDoors(); return doors; }
    }

    // ─────────────────────────────────────────────
    // 입장 / 클리어
    // ─────────────────────────────────────────────

    // RoomManager가 플레이어를 이 방에 넣은 직후 호출.
    public void OnPlayerEnter()
    {
        Visited = true;

        if (IsCleared || !HasEnemies)
        {
            IsCleared = true;
            SetDoorsLocked(false);
            return;
        }

        if (!spawned)
        {
            SpawnEnemies();
            spawned = true;
        }

        // 적이 남아 있으면 문을 잠근다
        if (alive.Count > 0)
        {
            SetDoorsLocked(true);
            nextClearCheck = Time.time + clearCheckInterval;
        }
        else
        {
            MarkCleared();
        }
    }

    private void Update()
    {
        if (IsCleared || !spawned) return;
        if (Time.time < nextClearCheck) return;

        nextClearCheck = Time.time + clearCheckInterval;

        // Enemy에 사망 이벤트가 없으므로 파괴 여부 / isDead를 폴링한다.
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            Enemy e = alive[i];
            if (e == null || e.isDead)
                alive.RemoveAt(i);
        }

        if (alive.Count == 0)
            MarkCleared();
    }

    private void MarkCleared()
    {
        IsCleared = true;
        SetDoorsLocked(false);
        Debug.Log($"[Room] {name} {GridPos} 클리어");
    }

    private void SetDoorsLocked(bool locked)
    {
        for (int i = 0; i < doors.Count; i++)
        {
            Door d = doors[i];
            if (d == null || !d.IsConnected) continue;   // 벽으로 막힌 문은 그대로
            d.SetLocked(locked);
        }
    }

    // ─────────────────────────────────────────────
    // 스폰
    // ─────────────────────────────────────────────

    private void SpawnEnemies()
    {
        alive.Clear();
        ResetSpawnOrder();

        for (int i = 0; i < spawns.Count; i++)
        {
            SpawnEntry entry = spawns[i];
            if (entry == null || entry.prefab == null) continue;

            for (int n = 0; n < entry.count; n++)
            {
                Vector3 pos = PickSpawnPos();
                GameObject go = Instantiate(entry.prefab, pos, Quaternion.identity, enemyContainer);

                Enemy enemy = go.GetComponent<Enemy>();
                if (enemy == null)
                {
                    Debug.LogError($"[Room] {entry.prefab.name} 에 Enemy 컴포넌트가 없음", this);
                    continue;
                }

                if (entry.info != null)
                {
                    enemy.Initialize(entry.info);
                    if (!string.IsNullOrEmpty(entry.info.EnemyName))
                        go.name = entry.info.EnemyName;
                }

                if (!Mathf.Approximately(entry.scale, 1f))
                {
                    go.transform.localScale = Vector3.one * entry.scale;
                    // 콜라이더가 커진 만큼 분리 반경도 키워야 서로 겹치지 않는다
                    enemy.SeparationRadius *= entry.scale;
                }

                alive.Add(enemy);
            }
        }
    }

    // 스폰 포인트를 섞어서 순서대로 소비한다. 무작위로 매번 뽑으면
    // 같은 자리에 여러 마리가 겹쳐서 나온다.
    private void ResetSpawnOrder()
    {
        spawnOrder.Clear();
        if (spawnPoints == null) return;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null) spawnOrder.Add(i);
        }

        for (int i = spawnOrder.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = spawnOrder[i];
            spawnOrder[i] = spawnOrder[j];
            spawnOrder[j] = tmp;
        }

        spawnCursor = 0;
    }

    private Vector3 PickSpawnPos()
    {
        if (spawnOrder.Count == 0)
            return transform.position;

        // 스폰 포인트보다 적이 많으면 한 바퀴 돌고 다시 섞어서 재사용
        if (spawnCursor >= spawnOrder.Count)
        {
            ResetSpawnOrder();
            if (spawnOrder.Count == 0) return transform.position;
        }

        Transform t = spawnPoints[spawnOrder[spawnCursor++]];
        return t != null ? t.position : transform.position;
    }

    // ─────────────────────────────────────────────
    // 에디터 보조 — 방 크기 맞추기용
    // ─────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(transform.position, new Vector3(gizmoRoomSize.x, gizmoRoomSize.y, 0f));
    }
}
