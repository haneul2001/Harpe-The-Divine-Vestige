using System.Collections;
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

        [Tooltip("분리 반경 덮어쓰기. 0이면 프리팹 값 그대로 사용.\n" +
                 "프리팹의 기본값은 이미 3배 크기 사용을 전제로 잡혀 있으므로 보통 건드릴 필요 없다.")]
        [Min(0f)] public float separationRadius = 0f;

        [Tooltip("이 적이 이 방의 보스. 죽을 때 슬로우 + 줌인 피니시 연출이 나온다.\n" +
                 "아무것도 체크하지 않으면 보스 방에서는 가장 크게 스폰되는 적을 보스로 본다.")]
        public bool isBoss = false;
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

    [Header("스폰 제외 구역")]
    [Tooltip("화면 오른쪽 위에는 미니맵이 떠 있어서 그 아래에서 적이 나오면 가려서 안 보인다. "
           + "방 오른쪽 위 이만큼의 칸(정사각)에서는 적을 스폰하지 않는다. 0이면 제외 없음.")]
    [Min(0)]
    [SerializeField] private int noSpawnCornerCells = 3;

    [Header("클리어 판정")]
    [Tooltip("적 생존 여부를 확인하는 주기(초). 이벤트 대신 폴링이라 Enemy.cs를 고칠 필요 없음")]
    [SerializeField] private float clearCheckInterval = 0.2f;

    [Header("등장 연출")]
    [Tooltip("문이 쾅 닫히고 나서 첫 적이 튀어나오기까지의 뜸(초)")]
    [SerializeField] private float spawnLeadIn = 0.15f;
    [Tooltip("적이 한 마리씩 차례로 튀어나오는 간격(초). 0이면 전부 동시에")]
    [SerializeField] private float spawnStagger = 0.08f;
    [Tooltip("한 마리가 튀어나오는 데 걸리는 시간(초)")]
    [SerializeField] private float spawnPopDuration = 0.26f;
    [Tooltip("튀어나올 때 제 크기의 몇 배까지 부풀었다 돌아오는지. 1이면 그냥 커지기만 한다")]
    [SerializeField] private float spawnOvershoot = 1.18f;
    [Tooltip("전투 시작으로 문이 잠길 때 카메라 흔들림 세기. 0이면 없음")]
    [SerializeField] private float doorSlamShake = 0.25f;
    [Tooltip("방을 클리어해 문이 열릴 때 카메라 흔들림 세기. 0이면 없음")]
    [SerializeField] private float clearShake = 0.15f;

    [Header("보스 처치 피니시 (던파식 슬로우 + 줌인)")]
    [Tooltip("보스가 죽을 때 시간이 느려지고 카메라가 당겨진다")]
    [SerializeField] private bool bossFinish = true;
    [Tooltip("느려진 시간 배율. 0.2~0.3이 묵직하다")]
    [Range(0.05f, 1f)]
    [SerializeField] private float finishTimeScale = 0.25f;
    [Tooltip("카메라 시야 배율. 작을수록 확대. 0.7이면 30% 당겨진다")]
    [Range(0.3f, 1f)]
    [SerializeField] private float finishZoom = 0.7f;
    [Tooltip("카메라가 보스 쪽으로 얼마나 따라붙는지. 1이면 보스를 화면 정중앙에 놓는다")]
    [Range(0f, 1f)]
    [SerializeField] private float finishFocusWeight = 0.6f;
    [Tooltip("줌인에 걸리는 시간(초, 실제 시간)")]
    [SerializeField] private float finishZoomIn = 0.28f;
    [Tooltip("줌인 상태로 머무는 시간(초, 실제 시간)")]
    [SerializeField] private float finishHold = 0.55f;
    [Tooltip("원래대로 돌아오는 시간(초, 실제 시간)")]
    [SerializeField] private float finishRecover = 0.4f;
    [Tooltip("처치 순간 카메라 흔들림. 0이면 없음")]
    [SerializeField] private float finishShake = 0.5f;

    [Header("에디터 표시")]
    [Tooltip("DungeonGenerator의 roomSize와 같은 값을 넣으면 방 크기를 맞추기 쉬움")]
    [SerializeField] private Vector2 gizmoRoomSize = new Vector2(32f, 18f);

    // 방의 실제 크기. 생성기가 격자에 배치할 때 쓴다.
    // 프리팹이 자기 크기를 들고 있으므로 생성기 인스펙터의 숫자와 어긋날 일이 없다.
    public Vector2 Size { get { return gizmoRoomSize; } }

    // 생성기가 채움
    public Vector2Int GridPos { get; private set; }

    public bool IsCleared { get; private set; }

    // 적이 스폰됐고 아직 못 깬 상태. 문이 잠겨 있는 구간과 같다.
    public bool IsInCombat { get { return spawned && !IsCleared; } }
    // 플레이어가 한 번이라도 들어온 방 (미니맵 표시용)
    public bool Visited { get; private set; }
    public bool HasEnemies => spawns != null && spawns.Count > 0;

    private readonly List<Enemy> alive = new List<Enemy>();
    private readonly List<int> spawnOrder = new List<int>();
    private int spawnCursor;
    private bool spawned;
    private float nextClearCheck;

    // 등장 연출이 아직 안 끝난 적들. 방을 나가 버리면 코루틴이 죽으므로
    // 크기 0에 AI가 꺼진 채로 남지 않도록 여기 담아 두고 즉시 마무리한다.
    private class PendingPop
    {
        public Enemy enemy;
        public Vector3 targetScale;
    }
    private readonly List<PendingPop> pendingPops = new List<PendingPop>();

    // 상단 체력바에 띄울 보스. 때리지 않아도 방에 들어가면 바로 보이게 하려고 들고 있는다.
    private Enemy barBoss;

    // 보스 처치 연출 상태
    private readonly HashSet<Enemy> bossEnemies = new HashSet<Enemy>();
    private bool finishPlayed;
    private bool finishRunning;
    // 연출 시작 전 카메라 시야. 도중에 방이 꺼져도 되돌릴 수 있게 필드로 들고 있는다.
    private float finishBaseSize = -1f;
    // 슬로우가 이미 걸린 상태에서 다시 읽으면 값이 중첩되므로 원래 물리 간격을 한 번만 잡아 둔다
    private static float defaultFixedDelta = -1f;

    private void Awake()
    {
        if (defaultFixedDelta <= 0f) defaultFixedDelta = Time.fixedDeltaTime;

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

        // 적이 남아 있으면 문을 잠근다 — 여기가 전투 시작 신호라 카메라를 한 번 친다
        if (alive.Count > 0)
        {
            SetDoorsLocked(true);
            if (doorSlamShake > 0f) CameraShake.Shake(doorSlamShake);

            StartCoroutine(PlaySpawnSequence());

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
        if (clearShake > 0f) CameraShake.Shake(clearShake);
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

    // 어느 엔트리가 보스인지 정한다.
    // 아무것도 체크돼 있지 않으면 보스 방에 한해 "가장 크게 스폰되는 적"을 보스로 본다.
    private int ResolveBossEntry()
    {
        for (int i = 0; i < spawns.Count; i++)
            if (spawns[i] != null && spawns[i].isBoss) return -2;   // 명시적 지정이 있음

        if (type != RoomType.Boss) return -1;

        int best = -1;
        float bestScale = 0f;
        for (int i = 0; i < spawns.Count; i++)
        {
            if (spawns[i] == null || spawns[i].prefab == null) continue;
            if (spawns[i].scale > bestScale) { bestScale = spawns[i].scale; best = i; }
        }
        return best;
    }

    private void SpawnEnemies()
    {
        alive.Clear();
        bossEnemies.Clear();
        ResetSpawnOrder();

        int autoBossIndex = ResolveBossEntry();

        for (int i = 0; i < spawns.Count; i++)
        {
            SpawnEntry entry = spawns[i];
            if (entry == null || entry.prefab == null) continue;

            bool entryIsBoss = entry.isBoss || i == autoBossIndex;

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

                Vector3 targetScale = Vector3.one * entry.scale;
                go.transform.localScale = targetScale;

                // 분리 반경은 크기 배율을 따라가지 않는다.
                // 프리팹 기본값(2.6)이 이미 스케일 3 기준으로 잡힌 값이라
                // 여기에 배율을 또 곱하면 반경이 방 폭의 절반까지 커져 온 방의 적이 서로 밀어낸다.
                if (entry.separationRadius > 0f)
                    enemy.SeparationRadius = entry.separationRadius;

                alive.Add(enemy);
                PrepareForPopIn(enemy, targetScale);

                // 등급은 처치 연출 옵션과 무관하게 붙인다 — 상단 체력바가 이 값을 본다
                if (entryIsBoss)
                {
                    enemy.Grade = EnemyGrade.Boss;
                    barBoss = enemy;
                }

                if (entryIsBoss && bossFinish)
                {
                    bossEnemies.Add(enemy);
                    enemy.Died += OnEnemyDied;
                }
            }
        }
    }

    // ─────────────────────────────────────────────
    // 보스 처치 피니시 — 시간이 늘어지고 카메라가 훅 들어간다
    // ─────────────────────────────────────────────

    private void OnEnemyDied(Enemy enemy)
    {
        if (enemy != null) enemy.Died -= OnEnemyDied;

        if (!bossFinish || finishPlayed) return;
        if (enemy == null || !bossEnemies.Contains(enemy)) return;

        finishPlayed = true;
        StartCoroutine(PlayBossFinish(enemy.transform.position));
    }

    // 연출 내내 실제 시간(unscaled)으로 돌아야 한다. 슬로우가 걸린 상태에서
    // 스케일된 시간을 쓰면 연출 자체가 같이 느려져 영영 안 끝난다.
    private IEnumerator PlayBossFinish(Vector3 bossPos)
    {
        finishRunning = true;

        Camera cam = Camera.main;
        CameraFollow follow = FindObjectOfType<CameraFollow>();

        float baseSize = cam != null ? cam.orthographicSize : 0f;
        finishBaseSize = baseSize;

        // 카메라가 쉬는 자리 = 방 중심. transform.position을 쓰면 흔들림 오프셋이 섞이지 않는다.
        Vector3 restPos = new Vector3(
            transform.position.x, transform.position.y,
            cam != null ? cam.transform.position.z : -40f);

        Vector3 focusPos = Vector3.Lerp(
            restPos,
            new Vector3(bossPos.x, bossPos.y, restPos.z),
            finishFocusWeight);

        if (finishShake > 0f) CameraShake.Shake(finishShake);

        if (follow != null)
        {
            follow.Suspended = true;
            follow.OverridePosition = restPos;
        }

        // ① 늘어지며 당겨진다
        yield return LerpFinish(0f, 1f, finishZoomIn, cam, follow, baseSize, restPos, focusPos);

        // ② 유지
        if (finishHold > 0f) yield return new WaitForSecondsRealtime(finishHold);

        // ③ 풀린다
        yield return LerpFinish(1f, 0f, finishRecover, cam, follow, baseSize, restPos, focusPos);

        RestoreAfterFinish(cam, follow, baseSize);
    }

    private IEnumerator LerpFinish(float from, float to, float duration,
        Camera cam, CameraFollow follow, float baseSize, Vector3 restPos, Vector3 focusPos)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += duration > 0f ? Time.unscaledDeltaTime / duration : 1f;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            float k = Mathf.Lerp(from, to, e);

            SetTimeScale(Mathf.Lerp(1f, finishTimeScale, k));

            if (cam != null) cam.orthographicSize = Mathf.Lerp(baseSize, baseSize * finishZoom, k);
            if (follow != null) follow.OverridePosition = Vector3.Lerp(restPos, focusPos, k);

            yield return null;
        }
    }

    private void RestoreAfterFinish(Camera cam, CameraFollow follow, float baseSize)
    {
        SetTimeScale(1f);
        if (cam != null && baseSize > 0f) cam.orthographicSize = baseSize;
        if (follow != null) follow.Suspended = false;
        finishRunning = false;
        finishBaseSize = -1f;
    }

    // timeScale만 바꾸면 물리가 뚝뚝 끊긴다. fixedDeltaTime도 같이 줄여야 부드럽다.
    private static void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        if (defaultFixedDelta > 0f) Time.fixedDeltaTime = defaultFixedDelta * scale;
    }

    // ─────────────────────────────────────────────
    // 등장 연출 — 크기 0에서 튀어나오며 AI가 켜진다
    // ─────────────────────────────────────────────

    // Enemy 컴포넌트를 꺼 두면 Update(상태머신)가 멈춘다.
    // Unity는 비활성 컴포넌트의 Start를 "처음 켜지는 시점"까지 미루므로,
    // 초기화(사거리 자동 계산 등)도 최종 크기가 정해진 뒤에 돌아 오히려 정확하다.
    private void PrepareForPopIn(Enemy enemy, Vector3 targetScale)
    {
        if (spawnPopDuration <= 0f) return;

        enemy.enabled = false;
        enemy.transform.localScale = Vector3.zero;

        SetAlpha(enemy, 0f);

        pendingPops.Add(new PendingPop { enemy = enemy, targetScale = targetScale });
    }

    private IEnumerator PlaySpawnSequence()
    {
        if (spawnLeadIn > 0f) yield return new WaitForSeconds(spawnLeadIn);

        // 보스는 첫 타를 맞기 전에 이름과 체력이 보여야 한다
        if (barBoss != null) EnemyHealthBar.ShowBoss(barBoss);

        // 리스트 사본으로 돈다 — 연출 중 방을 나가면 원본이 비워질 수 있다
        PendingPop[] queue = pendingPops.ToArray();

        for (int i = 0; i < queue.Length; i++)
        {
            StartCoroutine(PopIn(queue[i]));

            if (spawnStagger > 0f && i < queue.Length - 1)
                yield return new WaitForSeconds(spawnStagger);
        }
    }

    private IEnumerator PopIn(PendingPop pop)
    {
        Enemy enemy = pop.enemy;
        float time = 0f;

        while (time < spawnPopDuration)
        {
            if (enemy == null) yield break;

            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / spawnPopDuration);

            // 0 → 오버슛 → 제 크기. 앞 60%에서 부풀고 뒤 40%에서 가라앉는다.
            float s = (k < 0.6f)
                ? Mathf.Lerp(0f, spawnOvershoot, k / 0.6f)
                : Mathf.Lerp(spawnOvershoot, 1f, (k - 0.6f) / 0.4f);

            enemy.transform.localScale = pop.targetScale * s;
            SetAlpha(enemy, k);

            yield return null;
        }

        FinishPop(pop);
    }

    private void FinishPop(PendingPop pop)
    {
        pendingPops.Remove(pop);

        Enemy enemy = pop.enemy;
        if (enemy == null) return;

        enemy.transform.localScale = pop.targetScale;
        SetAlpha(enemy, 1f);
        enemy.enabled = true;
    }

    // 방을 나가면 이 오브젝트가 꺼지면서 코루틴이 죽는다.
    // 등장 도중이던 적이 크기 0에 AI가 꺼진 채로 굳지 않도록 즉시 마무리한다.
    private void OnDisable()
    {
        StopAllCoroutines();

        // 피니시 도중 방이 꺼지면(클리어로 문이 열려 그대로 나가 버리는 경우)
        // 코루틴이 죽으면서 시간이 느려진 채로 영영 묶인다. 반드시 되돌린다.
        if (finishRunning)
        {
            CameraFollow follow = FindObjectOfType<CameraFollow>();
            RestoreAfterFinish(Camera.main, follow, finishBaseSize);
        }

        // 남은 구독 정리
        foreach (Enemy e in bossEnemies)
            if (e != null) e.Died -= OnEnemyDied;

        for (int i = pendingPops.Count - 1; i >= 0; i--)
        {
            PendingPop pop = pendingPops[i];
            if (pop.enemy != null)
            {
                pop.enemy.transform.localScale = pop.targetScale;
                SetAlpha(pop.enemy, 1f);
                pop.enemy.enabled = true;
            }
        }
        pendingPops.Clear();
    }

    // 적 본체 스프라이트만 건드린다. 그림자/히트박스 표시까지 같이 페이드하면
    // 크기 0일 때 잔상처럼 남는 것들이 생긴다.
    private static void SetAlpha(Enemy enemy, float a)
    {
        if (enemy == null) return;

        SpriteRenderer sr = enemy.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }

    // 미니맵에 가리는 오른쪽 위 구석인지. 방 안에서의 상대 위치로 판단하므로
    // 방이 격자 어디에 배치되든 똑같이 동작한다.
    private bool IsInNoSpawnCorner(Transform point)
    {
        if (noSpawnCornerCells <= 0 || point == null) return false;

        Vector3 local = point.position - transform.position;
        float halfX = gizmoRoomSize.x * 0.5f;
        float halfY = gizmoRoomSize.y * 0.5f;

        return local.x >= halfX - noSpawnCornerCells
            && local.y >= halfY - noSpawnCornerCells;
    }

    // 스폰 포인트를 섞어서 순서대로 소비한다. 무작위로 매번 뽑으면
    // 같은 자리에 여러 마리가 겹쳐서 나온다.
    private void ResetSpawnOrder()
    {
        spawnOrder.Clear();
        if (spawnPoints == null) return;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null) continue;
            if (IsInNoSpawnCorner(spawnPoints[i])) continue;   // 미니맵 아래는 건너뛴다
            spawnOrder.Add(i);
        }

        // 전부 제외돼 버리면 스폰할 자리가 없어진다 — 그럴 땐 제외를 무시하고 다 쓴다
        if (spawnOrder.Count == 0)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
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
