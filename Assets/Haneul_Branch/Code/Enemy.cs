using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public class Enemy : MonoBehaviour
{
    [HideInInspector] public Rigidbody2D rb;
    [SerializeField] private Transform backPosition;

    public Transform BackPosition => backPosition;
    
    [Header("분리")]
    [SerializeField] private float separationRadius = 0.8f; //몹끼리 밀어내는 범위
    [SerializeField] private float separationForce = 2f;//몹끼리 밀어내는 힘
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
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        StateMachine = new EnemyStateMachine();
        // CombatIdleState = new CombatIdleState(this, StateMachine);
        IdleState = new IdleState(this, StateMachine);
        ChaseState = new ChaseState(this, StateMachine);
        AttackState = new AttackState(this, StateMachine);
        HitState = new HitState(this, StateMachine);
    }

    protected virtual void Start()
    {
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
        Debug.Log($"MoveToPlayer 호출 / isAttacking = {isAttacking}");
        if (player == null)
            return;

        Vector3 moveDir =
            (player.position - transform.position).normalized;

        Vector3 separation =
            GetSeparation();

        Vector3 finalDir =
            (moveDir + separation).normalized;

        rb.velocity = finalDir * moveSpeed;

        animator.SetBool("isFollow", true);
    }
    public void StopMove()
    {
        Debug.Log($"StopMove 호출, velocity = {rb.velocity}");

        rb.velocity = Vector2.zero;

        Debug.Log($"StopMove 후 velocity = {rb.velocity}");

        if (animator != null)
            animator.SetBool("isFollow", false);
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

    public virtual void Attack() 
    {   
        //Override에서 처리
    }
    public virtual void ShowAttackRange(bool show)
    {
    }
    public void TakeDamage(int damage)
    {
        if (isDead)
            return;
        Debug.Log("TakeDamage 호출");

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
    private Vector2 GetSeparation()
    {
        // 내가 공격 중이면 분리 안 함
        if (isAttacking)
            return Vector2.zero;

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                separationRadius);

        Vector2 push = Vector2.zero;

        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == gameObject)
                continue;

            Enemy other = hit.GetComponent<Enemy>();

            if (other == null)
                continue;

            // 공격 중인 몬스터는 밀지 않음
            if (other.isAttacking)
                continue;

            Vector2 dir =
                (Vector2)(transform.position - other.transform.position);

            float distance = dir.magnitude;

            if (distance > 0f)
            {
                push += dir.normalized / distance;
            }
        }

        return push * separationForce;
    }
    public bool CanHarvest
    {
        get
        {
            return (float)hp / maxHp <= 0.3f;
        }
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