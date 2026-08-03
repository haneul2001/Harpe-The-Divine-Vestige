using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public class Enemy : MonoBehaviour
{
    [HideInInspector] public Rigidbody2D rb;
    [SerializeField] private Transform backPosition;

    public Transform BackPosition => backPosition;

    // 스폰 시 transform.localScale을 키우면 콜라이더는 커지지만 분리 반경은 그대로라
    // 서로 겹친다(분리 힘이 0이 됨). 스폰하는 쪽에서 스케일만큼 같이 키워 주기 위한 접근자.
    public float SeparationRadius
    {
        get { return separationRadius; }
        set { separationRadius = value; }
    }

    [Header("분리 (몹끼리 겹침 방지)")]
    [Tooltip("이 반경 안의 다른 적을 부드럽게 피함")]
    [SerializeField] private float separationRadius = 0.8f;
    [Tooltip("추격 대비 분리 강도(0~1). 0이면 분리 없음, 클수록 서로 잘 벌어짐")]
    [Range(0f, 1f)]
    [SerializeField] private float separationWeight = 0.6f;

    [Header("산개 (뭉침 강제 해소)")]
    [Tooltip("주변 붐빔이 이 값을 넘으면 산개 성분 작동(대략 이만큼의 적이 밀착). 대칭적으로 둘러싸여 분리 힘이 상쇄돼 끼이는 것을 방지")]
    [SerializeField] private float crowdPressureThreshold = 1.5f;
    [Tooltip("분리 힘이 이 값보다 약하면(=충분히 떨어져 있으면) 제자리걸음 없이 완전히 정지")]
    [SerializeField] private float settleDeadzone = 0.25f;

    [Header("공격 대기 중 자리잡기")]
    [Tooltip("사거리 안에서 공격 쿨타임을 기다리는 동안의 이동 속도 배율.\n" +
             "0 = 대기 중에는 서로 전혀 밀지 않고 그 자리에 고정 (기본).\n" +
             "겹친 채로 굳는 게 신경 쓰이면 0.1~0.15 정도만 주면 아주 천천히 풀린다.\n" +
             "1이면 추격과 같은 전속력으로 밀려나 '공격 준비 중인데 옆으로 달리는' 그림이 된다.")]
    [Range(0f, 1f)]
    [SerializeField] private float settleSpeedFactor = 0f;

    [Tooltip("대기 중에는 분리 반경을 이 비율로 좁힌다.\n" +
             "추격 때처럼 넓게 잡으면 멀리 떨어진 적끼리도 계속 밀어내서 제자리에 못 선다.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float settleRadiusFactor = 0.5f;

    [Tooltip("공격(예고·회복) 중에는 위치를 물리적으로 고정해 다른 적/플레이어에게 밀리지 않게 한다.\n" +
             "rb.velocity를 0으로 두는 것만으로는 침투 해소(depenetration)로 밀려나므로 제약을 건다.\n" +
             "Kinematic 전환과 달리 Dynamic을 유지하므로 벽 충돌은 그대로 살아 있다.")]
    [SerializeField] private bool lockPositionWhileAttacking = true;
    public bool LockPositionWhileAttacking => lockPositionWhileAttacking;

    // 프리팹의 원래 제약(보통 FreezeRotation) — 잠금 해제 시 이 값으로 되돌린다.
    private RigidbodyConstraints2D baseConstraints;
    private bool isPositionLocked;

    [Header("우회 (앞이 막혔을 때)")]
    [Tooltip("앞을 막는 것으로 취급할 레이어. 비워 두면 Awake에서 Enemy + Wall로 자동 설정.\n" +
             "Wall이 들어 있어야 우회하다 방 밖으로 새지 않는다 (잠긴 문의 Blocker도 Wall 레이어).")]
    [SerializeField] private LayerMask obstacleLayers;
    [Tooltip("진행 방향으로 이만큼 앞을 살펴 막혔는지 판단한다. 몸 크기보다 조금 크게")]
    [SerializeField] private float avoidProbeDistance = 0.6f;
    [Tooltip("막혔을 때 틀어 볼 최대 각도(도). 이 범위에서 플레이어 쪽에 가장 가까운 빈 길을 고른다")]
    [Range(15f, 170f)]
    [SerializeField] private float avoidMaxAngle = 120f;
    [Tooltip("우회 각도 후보 간격(도). 작을수록 부드럽지만 캐스트 횟수가 늘어난다")]
    [Range(10f, 60f)]
    [SerializeField] private float avoidAngleStep = 30f;

    private ContactFilter2D _avoidFilter;
    private readonly RaycastHit2D[] _avoidBuffer = new RaycastHit2D[8];
    // 직전에 우회한 방향(+1 좌 / -1 우). 매 프레임 좌우가 뒤집히면 제자리에서 떨기만 한다.
    private int avoidSide;

    // 무할당 조회용 버퍼/필터
    private static readonly Collider2D[] _sepBuffer = new Collider2D[16];
    private ContactFilter2D _sepFilter;
    // 개체마다 고정된 산개 방향(황금비 해시) — 붐빌 때 서로 다른 쪽으로 퍼지고 프레임마다 흔들리지 않음
    private Vector2 _scatterDir = Vector2.right;
    // 대기 중 자리잡기가 진행 중인지 (데드존 히스테리시스용)
    private bool isSettling;
    [Header("AI 설정")]
    public float detectRange = 40f;//플레이어 감지 범위
    public float attackRange = 2.5f;//공격 범위
    public float moveSpeed = 2.5f; //이동속도
    [Header("전투 대기")]
    // public float combatIdleDuration = 1.5f;
    // 공격 관련
    public float attackIdleTime  = 2f; //CanAttack()에서 사용되는 공격 쿨타임
    public float attackWarningDuration = 2f; //오버라이드 된 코드에서 공격 예고 시간으로 사용
    public bool isAttacking { get; protected set; }
    // 공격 히트박스가 켜져 있는 실제 발동 구간 (돌진/제자리 등 파생이 속도를 직접 제어).
    // 이 구간에는 분리 정렬을 적용하지 않는다.
    public bool IsAttackActive { get; protected set; }
    public IEnemyAttack AttackBehavior { get; private set; }
    [Header("공격")]
    [SerializeField] protected int attackDamage = 1;//공격력
    public int AttackDamage => attackDamage;
    [Header("피격")]
    public float hitDuration = 0.2f;//피격 상태 지속 시간
    
    [Header("참조")]
    public Transform player;

    [Header("체력")]
    public int maxHp = 5;//체력
    public int hp { get; private set; }

    [Header("데이터 (선택)")]
    [Tooltip("있으면 hp/이동속도/공격력을 이 SO 값으로 오버라이드")]
    [SerializeField] private EnemyInfo enemyInfo;
    public EnemyInfo Info => enemyInfo;

    [HideInInspector] public Animator animator;
    [HideInInspector] public SpriteRenderer spriteRenderer;

    public EnemyStateMachine StateMachine { get; private set; }

    // 상태들
    [HideInInspector] public IdleState IdleState;
    [HideInInspector] public ChaseState ChaseState;
    [HideInInspector] public AttackState AttackState;
    // [HideInInspector] public CombatIdleState CombatIdleState;
    [HideInInspector] public HitState HitState;
    [HideInInspector] public float lastAttackTime;

    // 방향
    public bool IsFacingRight { get; private set; }
    public bool isDead { get; private set; }

    // 죽는 순간 알린다. Room의 클리어 판정은 0.2초 폴링이라 타격 순간을 잡을 수 없어
    // 보스 처치 연출처럼 타이밍이 중요한 곳은 이 이벤트를 쓴다.
    public event System.Action<Enemy> Died;
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null) baseConstraints = rb.constraints;
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        AttackBehavior = GetComponent<IEnemyAttack>();

        // 분리 조회는 Enemy 레이어만 (히트박스/벽/플레이어 제외, 무할당)
        _sepFilter = new ContactFilter2D { useTriggers = false };
        _sepFilter.SetLayerMask(1 << gameObject.layer);

        // 우회 조회는 적 + 벽. 벽이 빠지면 우회하다 방 밖으로 나간다.
        if (obstacleLayers.value == 0)
            obstacleLayers = (1 << gameObject.layer) | LayerMask.GetMask("Enemy", "Wall");
        _avoidFilter = new ContactFilter2D { useTriggers = false };
        _avoidFilter.SetLayerMask(obstacleLayers);

        // 개체마다 고르게 분산된 고정 산개 방향 (황금비로 해싱)
        float frac = Mathf.Abs(GetInstanceID()) * 0.61803398875f;
        frac -= Mathf.Floor(frac);
        float scatterAngle = frac * Mathf.PI * 2f;
        _scatterDir = new Vector2(Mathf.Cos(scatterAngle), Mathf.Sin(scatterAngle));

        StateMachine = new EnemyStateMachine();
        // CombatIdleState = new CombatIdleState(this, StateMachine);
        IdleState = new IdleState(this, StateMachine);
        ChaseState = new ChaseState(this, StateMachine);
        AttackState = new AttackState(this, StateMachine);
        HitState = new HitState(this, StateMachine);
    }

    // 체력이 확정됐는지. 확정 전에는 hp가 0이라 CanHarvest가 참이 되어
    // 스폰 순간 처형 표시가 번쩍인다. 특히 등장 연출 중에는 Enemy 컴포넌트가
    // 꺼져 있어 Start가 미뤄지므로 그 시간 내내 떠 있게 된다.
    private bool statsReady;

    public void Initialize(EnemyInfo info)
    {
        enemyInfo = info;
        ApplyEnemyInfo();

        // Start를 기다리지 않고 여기서 바로 체력을 채운다
        hp = maxHp;
        statsReady = true;
    }

    private void ApplyEnemyInfo()
    {
        if (enemyInfo == null) return;
        maxHp = enemyInfo.HP;
        moveSpeed = enemyInfo.Speed;
        attackDamage = enemyInfo.Damage;
    }

    protected virtual void Start()
    {
        ApplyEnemyInfo();

        // Initialize로 이미 채웠으면 덮어쓰지 않는다 (등장 연출 중 피해를 입었을 수도 있다)
        if (!statsReady)
        {
            hp = maxHp;
            statsReady = true;
        }

        // Inspector 할당이 없을 때만 씬의 Player를 찾음
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

            if (playerObj != null)
            {
                player = playerObj.transform;
                Debug.Log($"Scene에서 Player 할당 성공: {player.name}");
            }
            else
            {
                Debug.LogError("Tag 'Player'를 가진 Player를 찾을 수 없음");
            }
        }
        else
        {
            Debug.Log($"Inspector Player 사용: {player.name}");
        }

        StateMachine.Initialize(IdleState);
    }

    private void Update()
    {
        //Debug.Log(player.position); // player가 제대로 할당되었는지 확인하는 로그
        if (player == null || isDead)
            return;

        StateMachine.Update();

       
    }

    // =========================
    // 공용 함수
    // =========================

    // 위치 고정 on/off. Dynamic은 유지하고 위치 제약만 켠다.
    //  - 켜면: 다른 적이 밀어도 침투 해소로조차 밀리지 않음. 밀고 온 쪽이 벽처럼 막혀 옆으로 흘러간다.
    //  - Kinematic 전환을 쓰지 않는 이유: Kinematic은 벽 콜라이더도 무시해서 몹이 벽을 통과한다.
    public void SetPositionLocked(bool locked)
    {
        if (rb == null || isPositionLocked == locked)
            return;

        isPositionLocked = locked;
        rb.constraints = locked
            ? baseConstraints | RigidbodyConstraints2D.FreezePosition
            : baseConstraints;

        // 잠그는 순간 남아 있던 속도는 버린다 (해제 시 튀어나가지 않도록)
        if (locked) rb.velocity = Vector2.zero;
    }

    public void MoveToPlayer()
    {
        if (player == null)
            return;

        // 이동하려면 잠금은 반드시 풀려 있어야 한다 (상태 전환 누락 대비 안전장치)
        SetPositionLocked(false);

        Vector2 moveDir =
            ((Vector2)(player.position - transform.position)).normalized;

        // 분리는 방향(크기 ≤ 1) — 가중치로 섞어 추격을 압도하지 않게
        Vector2 separation = GetSeparation();

        Vector2 finalDir =
            (moveDir + separation * separationWeight).normalized;

        // 공격 대기 중인 몹 등 앞을 막는 것이 있으면 옆으로 돌아간다.
        finalDir = AvoidObstacles(finalDir);

        // 사방이 다 막혔으면 비집고 들어가며 제자리걸음 하지 말고 그냥 선다.
        if (finalDir == Vector2.zero)
        {
            StopMove();
            return;
        }

        rb.velocity = finalDir * moveSpeed;

        animator.SetBool("isFollow", true);
    }
    public void StopMove()
    {
        rb.velocity = Vector2.zero;
        isSettling = false;

        if (animator != null)
            animator.SetBool("isFollow", false);
    }
    // 사거리 안에서 공격 쿨타임을 기다리는 동안: 전진(seek)은 멈추고 겹침만 푼다.
    //
    // 추격 때와 같은 반경·속도로 밀어내면 공격을 준비하는 적이 전속력으로 옆으로
    // 미끄러져 어색하다. 그래서 여기서는
    //   ① 반경을 좁혀 실제로 겹칠 때만 반응하고
    //   ② 속도를 낮춰 천천히 비켜주며
    //   ③ 산개(scatter)는 끈다 — 뭉침 해소용이라 제자리 대기 중엔 그냥 흘러가게만 만든다.
    public void SettleWithSeparation()
    {
        // 0이면 대기 중 서로 밀지 않는다 — 조회 자체를 건너뛴다.
        if (settleSpeedFactor <= 0f)
        {
            StopMove();
            return;
        }

        Vector2 separation = GetSeparation(settleRadiusFactor, false);
        float mag = separation.magnitude;

        // 히스테리시스: 한 번 멈추면 데드존을 확실히 넘어야 다시 움직인다.
        // 단일 임계값이면 경계에서 정지/이동이 매 프레임 번갈아 일어나 덜덜 떤다.
        float threshold = isSettling ? settleDeadzone * 0.5f : settleDeadzone;

        if (mag < threshold)
        {
            StopMove();
            return;
        }

        isSettling = true;
        SetPositionLocked(false);
        rb.velocity = separation * moveSpeed * settleSpeedFactor;

        if (animator != null)
            animator.SetBool("isFollow", true);
    }
    public void FaceToPlayer()
    {
        if (player == null)
            return;

        float dx = player.position.x - transform.position.x;

        if (Mathf.Abs(dx) <= 0.01f)
            return;

        SetFacing(dx > 0);
    }

    private void SetFacing(bool faceRight)
    {
        IsFacingRight = faceRight;
        if(faceRight) transform.rotation = Quaternion.Euler(0f,0f,0f);
        else transform.rotation = Quaternion.Euler(0f,180f,0f);
    }

    public bool CanAttack()
    {
        return Time.time - lastAttackTime >= attackIdleTime && DistanceToPlayer() <= attackRange;
    }

    public void TakeDamage(int damage, bool isCritical = false)
    {
        if (isDead)
            return;
        Debug.Log("TakeDamage 호출");

        // 플로팅 데미지 숫자 표시 (죽는 타격도 보이도록 hp 차감 전에 호출)
        if (DamageNumberSpawner.Instance != null)
            DamageNumberSpawner.Instance.Show(transform.position, damage, isCritical);

        hp -= damage;

        if (hp <= 0)
        {
            Die();
            return;
        }

        StateMachine.ChangeState(HitState);
    }

    private void Die()
    {
        isDead = true;

        DetachFromPhysics();

        if (animator != null)
        {
            animator.SetBool("isFollow", false);
            animator.SetTrigger("dead");
        }

        if (Died != null) Died(this);

        Destroy(gameObject, 1.5f);//사라지는 시간
    }

    // 죽는 순간 물리에서 완전히 빠진다.
    // 시체가 다른 적/플레이어에게 밀려 미끄러지지도, 반대로 남의 길을 막지도 않는다.
    // 죽은 자리에 그대로 서서 사망 애니메이션만 재생된다.
    private void DetachFromPhysics()
    {
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            SetPositionLocked(false);

            // 이 리지드바디에 붙은 콜라이더(몸통·발밑·히트박스)가 한 번에 시뮬레이션에서 빠진다
            rb.simulated = false;
        }

        // 리지드바디가 없는 프리팹이나 자체 리지드바디를 가진 자식까지 확실히 정리
        Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++)
            cols[i].enabled = false;
    }

    public float DistanceToPlayer()
    {
        if (player == null) return float.MaxValue;
        return Vector3.Distance(transform.position, player.position);
    }

    // 플레이어를 인식하고 있는지. 은신 중이면 거리와 무관하게 못 본다.
    // 추격을 시작하는 조건이자, 추격을 유지하는 조건이기도 하다.
    public bool CanSeePlayer()
    {
        if (player == null) return false;
        if (PlayerStealth.IsHidden) return false;

        return DistanceToPlayer() <= detectRange;
    }
    // 주변 적을 피하는 방향(크기 0~1)을 반환. 부드러운 감쇠로 튕김/진동 없음.
    // 추격 중에는 기본값(전체 반경 + 산개)을 쓴다.
    private Vector2 GetSeparation()
    {
        return GetSeparation(1f, true);
    }

    // radiusScale: 분리 반경 배율 (대기 중엔 좁혀서 실제 겹칠 때만 반응)
    // allowScatter: 뭉침 강제 해소용 산개 성분을 섞을지
    private Vector2 GetSeparation(float radiusScale, bool allowScatter)
    {
        if (separationWeight <= 0f)
            return Vector2.zero;

        float radius = separationRadius * radiusScale;
        if (radius <= 0.0001f)
            return Vector2.zero;

        int count = Physics2D.OverlapCircle(
            transform.position, radius, _sepFilter, _sepBuffer);

        Vector2 push = Vector2.zero;
        float pressure = 0f; // 방향과 무관한 '붐빔' 정도 — 대칭으로 둘러싸여 벡터가 상쇄돼도 누적됨

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = _sepBuffer[i];
            if (hit == null || hit.gameObject == gameObject)
                continue;

            Enemy other = hit.GetComponentInParent<Enemy>();
            if (other == null || other == this)
                continue;

            Vector2 away = (Vector2)(transform.position - other.transform.position);
            float d = away.magnitude;

            if (d > 0.0001f)
            {
                // 가까울수록 강하고 경계에서 0 (1/d 폭주 대신 선형 감쇠)
                float closeness = 1f - Mathf.Clamp01(d / radius);
                push += away.normalized * closeness;
                pressure += closeness;
            }
            else
            {
                // 완전히 겹치면 임의 방향으로 살짝
                push += Random.insideUnitCircle.normalized * 0.5f;
                pressure += 0.5f;
            }
        }

        // 산개: 붐빔이 임계치를 넘으면(대칭 상쇄로 분리 힘이 0에 가까워도) 개체별 고정
        // 방향으로 흩어지는 성분을 더해 뭉침을 강제로 푼다. 초과분에 비례(최대 1).
        if (allowScatter && pressure > crowdPressureThreshold)
            push += _scatterDir * Mathf.Min(pressure - crowdPressureThreshold, 1f);

        // 방향만 사용하도록 크기를 1로 제한 → 추격 방향을 압도하지 않음
        if (push.sqrMagnitude > 1f)
            push.Normalize();

        return push;
    }
    // ─────────────────────────────────────────────
    // 우회 — 앞이 막히면 옆으로 돌아간다
    // ─────────────────────────────────────────────
    //
    // 경로 탐색(A*/NavMesh)이 아니라 "가고 싶은 방향을 조금씩 틀어 보고 비어 있는 첫 방향으로 간다"는
    // 방식이다. 방 하나 안에서 몹 몇 마리를 피해 가는 데는 이 정도로 충분하고, 매 프레임 다시
    // 계산하므로 앞의 몹이 비키면 알아서 원래 방향으로 돌아온다.
    //
    // 장애물 레이어에 Wall이 들어 있어 벽 쪽 후보는 애초에 선택되지 않는다 → 방 밖으로 새지 않는다.
    private Vector2 AvoidObstacles(Vector2 desired)
    {
        if (rb == null || desired == Vector2.zero)
            return desired;

        // 정면이 뚫려 있으면 그대로. 대부분의 프레임은 캐스트 한 번으로 끝난다.
        if (IsDirectionClear(desired))
        {
            avoidSide = 0;
            return desired;
        }

        // 직전에 돌아간 쪽을 먼저 시도한다. 좌우를 매 프레임 새로 고르면 몸이 떨린다.
        int firstSide = avoidSide != 0 ? avoidSide : 1;

        for (float angle = avoidAngleStep; angle <= avoidMaxAngle; angle += avoidAngleStep)
        {
            for (int i = 0; i < 2; i++)
            {
                int side = (i == 0) ? firstSide : -firstSide;
                Vector2 candidate = Rotate(desired, angle * side);

                if (IsDirectionClear(candidate))
                {
                    avoidSide = side;
                    return candidate;
                }
            }
        }

        avoidSide = 0;
        return Vector2.zero;
    }

    private bool IsDirectionClear(Vector2 dir)
    {
        int count = rb.Cast(dir, _avoidFilter, _avoidBuffer, avoidProbeDistance);

        for (int i = 0; i < count; i++)
        {
            RaycastHit2D h = _avoidBuffer[i];
            if (h.collider == null)
                continue;

            // 이미 몸이 겹쳐 있는 콜라이더는 거리 0으로 잡힌다. 그 콜라이더에서 "벗어나는"
            // 방향이면(법선과 진행 방향이 같은 쪽) 막힌 게 아니다. 이걸 걸러내지 않으면
            // 옆구리가 닿는 순간 모든 방향이 막힌 것으로 판정돼 그대로 굳어 버린다.
            if (h.distance <= 0.0001f && Vector2.Dot(h.normal, dir) > 0f)
                continue;

            return false;
        }

        return true;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    public bool CanHarvest
    {
        get
        {
            // 죽으면 hp가 0 이하라 비율 조건은 항상 참이 된다.
            // 사망 중인 적은 처형 대상도 아니고 처형 표시도 뜨면 안 되므로 먼저 걸러낸다.
            if (isDead) return false;

            // 체력이 채워지기 전에도 hp가 0이라 같은 문제가 생긴다 (스폰 순간 번쩍임)
            if (!statsReady || maxHp <= 0) return false;

            return (float)hp / maxHp <= 0.3f;
        }
    }
    // 패링 등으로 인한 경직 (기존 피격 상태 재사용)
    public void Stagger()
    {
        if (isDead) return;
        StateMachine.ChangeState(HitState);
    }

    public void HarvestDie(float destroyDelay)
    {
        if(isDead) return;
        isDead = true;

        DetachFromPhysics();

        if(animator != null)
        {
            animator.SetBool("isFollow", false);
            animator.SetTrigger("dead");
        }

        if (Died != null) Died(this);

        Destroy(gameObject, destroyDelay);
    }
}