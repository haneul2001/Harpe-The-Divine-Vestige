using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public class Enemy : MonoBehaviour
{
    [HideInInspector] public Rigidbody2D rb;
    [SerializeField] private Transform backPosition;

    public Transform BackPosition => backPosition;
    
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

    // 무할당 조회용 버퍼/필터
    private static readonly Collider2D[] _sepBuffer = new Collider2D[16];
    private ContactFilter2D _sepFilter;
    // 개체마다 고정된 산개 방향(황금비 해시) — 붐빌 때 서로 다른 쪽으로 퍼지고 프레임마다 흔들리지 않음
    private Vector2 _scatterDir = Vector2.right;
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
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        AttackBehavior = GetComponent<IEnemyAttack>();

        // 분리 조회는 Enemy 레이어만 (히트박스/벽/플레이어 제외, 무할당)
        _sepFilter = new ContactFilter2D { useTriggers = false };
        _sepFilter.SetLayerMask(1 << gameObject.layer);

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

    public void Initialize(EnemyInfo info)
    {
        enemyInfo = info;
        ApplyEnemyInfo();
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
        hp = maxHp;

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

    public void MoveToPlayer()
    {
        if (player == null)
            return;

        Vector2 moveDir =
            ((Vector2)(player.position - transform.position)).normalized;

        // 분리는 방향(크기 ≤ 1) — 가중치로 섞어 추격을 압도하지 않게
        Vector2 separation = GetSeparation();

        Vector2 finalDir =
            (moveDir + separation * separationWeight).normalized;

        rb.velocity = finalDir * moveSpeed;

        animator.SetBool("isFollow", true);
    }
    public void StopMove()
    {
        rb.velocity = Vector2.zero;

        if (animator != null)
            animator.SetBool("isFollow", false);
    }
    // 사거리 안 / 공격 예고·회복 중: 전진(seek)은 멈추고 분리만 적용해
    // 서로 겹치지 않게 자리를 잡는다. 주변에 아무도 없으면 분리가 0이라 자연히 정지.
    public void SettleWithSeparation()
    {
        Vector2 separation = GetSeparation();

        // 충분히 떨어져 있으면(분리 힘이 데드존 미만) 완전히 정지 → 제자리걸음 방지.
        // 실제로 겹칠 때만 움직여 자리를 벌린다.
        if (separation.magnitude < settleDeadzone)
        {
            StopMove();
            return;
        }

        rb.velocity = separation * moveSpeed;

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

        rb.velocity = Vector2.zero;
        if (animator != null)
        {
            animator.SetBool("isFollow", false);
            animator.SetTrigger("dead");
        }

        Destroy(gameObject, 1.5f);//사라지는 시간
    }

    public float DistanceToPlayer()
    {
        if (player == null) return float.MaxValue;
        return Vector3.Distance(transform.position, player.position);
    }
    // 주변 적을 피하는 방향(크기 0~1)을 반환. 부드러운 감쇠로 튕김/진동 없음.
    private Vector2 GetSeparation()
    {
        if (separationWeight <= 0f)
            return Vector2.zero;

        int count = Physics2D.OverlapCircle(
            transform.position, separationRadius, _sepFilter, _sepBuffer);

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
                float closeness = 1f - Mathf.Clamp01(d / separationRadius);
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
        if (pressure > crowdPressureThreshold)
            push += _scatterDir * Mathf.Min(pressure - crowdPressureThreshold, 1f);

        // 방향만 사용하도록 크기를 1로 제한 → 추격 방향을 압도하지 않음
        if (push.sqrMagnitude > 1f)
            push.Normalize();

        return push;
    }
    public bool CanHarvest
    {
        get
        {
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
        StopMove();
        if(animator != null)
        {
            animator.SetBool("isFollow", false);
            animator.SetTrigger("dead");
        }
        Destroy(gameObject, destroyDelay);
    }
}