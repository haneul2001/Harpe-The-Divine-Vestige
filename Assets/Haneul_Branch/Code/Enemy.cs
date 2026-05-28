using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public class Enemy : MonoBehaviour
{
   
    [Header("AI 설정")]
    public float detectRange = 40f;
    public float attackRange = 2.5f;
    public float moveSpeed = 2.5f;
    [Header("전투 대기")]
    public float combatIdleDuration = 1.5f;
    // 공격 관련
    public float attackCooldown = 2f;
    public float attackDelay = 0.5f;
    public bool isAttacking { get; protected set; }
    [Header("공격")]
    [SerializeField] protected int attackDamage = 1;
    public int AttackDamage => attackDamage;
    [Header("피격")]
    public float hitDuration = 0.2f;
    
    [Header("참조")]
    public Transform player;

    [Header("체력")]
    public int hp = 3;

    [HideInInspector] public Animator animator;
    [HideInInspector] public SpriteRenderer spriteRenderer;

    public EnemyStateMachine StateMachine { get; private set; }

    // 상태들
    [HideInInspector] public IdleState IdleState;
    [HideInInspector] public ChaseState ChaseState;
    [HideInInspector] public AttackState AttackState;
    [HideInInspector] public CombatIdleState CombatIdleState;
    [HideInInspector] public HitState HitState;
    [HideInInspector] public float lastAttackTime;

    // 방향
    public bool IsFacingRight { get; private set; }
    public bool isDead { get; private set; }
    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        StateMachine = new EnemyStateMachine();
        CombatIdleState = new CombatIdleState(this, StateMachine);
        IdleState = new IdleState(this, StateMachine);
        ChaseState = new ChaseState(this, StateMachine);
        AttackState = new AttackState(this, StateMachine);
        HitState = new HitState(this, StateMachine);
    }

    protected virtual void Start()
    {
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

        Vector3 target = player.position;
        transform.position = Vector3.MoveTowards(
            transform.position,
            target,
            moveSpeed * Time.deltaTime
        );

        if (animator != null) animator.SetBool("isFollow", true);
    }

    public void StopMove()
    {
        if (animator != null) animator.SetBool("isFollow", false);
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
        return Time.time - lastAttackTime >= attackCooldown;
    }

    public virtual void Attack() 
    {
        lastAttackTime = Time.time;

        if (animator != null)
        {
            animator.SetTrigger("attack");
        }
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
        if (animator != null)
        {
            animator.SetBool("isFollow", false);
            animator.SetTrigger("dead");
        }

        Destroy(gameObject, 1.5f);
    }

    public float DistanceToPlayer()
    {
        if (player == null) return float.MaxValue;
        return Vector3.Distance(transform.position, player.position);
    }
}