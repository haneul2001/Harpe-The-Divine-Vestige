using System.Collections;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    // ====================== 상태 ======================
    enum EnemyState
    {
        Idle,
        Chase,
        Attack,
        Execute,
        Dead
    }

    private EnemyState currentState;

    // ====================== 설정 ======================
    [Header("AI 설정")]
    public float detectRange = 8f;
    public float attackRange = 1.5f;
    public float moveSpeed = 2.5f;

    [Header("처형 위치")]
    public Transform backPosition;
    public float backOffset = 0.6f;

    [Header("참조")]
    public Transform player;
    public GameObject harvest_image;

    // ====================== 내부 ======================
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private bool isFacingRight = false;
    private bool isAttacking = false;

    public float attackCooldown = 4f;
    public float attackDelay = 0.5f;
    private float lastAttackTime = 0f;

    public int hp = 3;

    // ====================== 초기화 ======================
    void Start()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (harvest_image == null)
            harvest_image = transform.Find("harvest_point")?.gameObject;

        if (harvest_image != null)
            harvest_image.SetActive(false);

        if (backPosition == null)
            backPosition = transform.Find("BackPosition");

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        FaceToPlayer();
        ChangeState(EnemyState.Idle);
    }

    // ====================== 업데이트 ======================
    void Update()
    {
        if (player == null || currentState == EnemyState.Dead)
            return;

        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdle();
                break;

            case EnemyState.Chase:
                UpdateChase();
                break;

            case EnemyState.Attack:
                UpdateAttack();
                break;
        }
    }

    // ====================== 상태별 로직 ======================

    void UpdateIdle()
    {
        float distance = Vector2.Distance(transform.position, player.position);

        if (distance <= detectRange)
            ChangeState(EnemyState.Chase);
    }

    void UpdateChase()
    {
        float distance = Vector2.Distance(transform.position, player.position);

        FaceToPlayer();
        MoveToPlayer();

        if (distance <= attackRange && CanAttack())
        {
            ChangeState(EnemyState.Attack);
        }
    }

    void UpdateAttack()
    {
        FaceToPlayer();

        if (!isAttacking)
        {
            StartCoroutine(AttackRoutine());
        }
    }

    // ====================== 상태 전환 ======================
    void ChangeState(EnemyState newState)
    {
        currentState = newState;

        switch (newState)
        {
            case EnemyState.Idle:
                animator.SetBool("isWalking", false);
                break;

            case EnemyState.Chase:
                break;

            case EnemyState.Attack:
                break;

            case EnemyState.Execute:
                StartCoroutine(ExecutionSequence());
                break;

            case EnemyState.Dead:
                Die();
                break;
        }
    }

    // ====================== 이동 ======================
    void MoveToPlayer()
    {
        Vector2 targetPos = new Vector2(player.position.x, transform.position.y);

        transform.position = Vector2.MoveTowards(
            transform.position,
            targetPos,
            moveSpeed * Time.deltaTime
        );

        animator.SetBool("isWalking", true);
    }

    // ====================== 방향 ======================
    void FaceToPlayer()
    {
        float dx = player.position.x - transform.position.x;

        if (Mathf.Abs(dx) > 0.01f)
            SetFacing(dx > 0);
    }

    void SetFacing(bool faceRight)
    {
        isFacingRight = faceRight;
        spriteRenderer.flipX = !faceRight;
        FlipBackPosition();
    }

    void FlipBackPosition()
    {
        if (backPosition == null) return;

        float xPos = isFacingRight ? -backOffset : backOffset;
        backPosition.localPosition = new Vector3(xPos, 0, 0);
    }

    // ====================== 공격 ======================
    bool CanAttack()
    {
        return Time.time - lastAttackTime > attackCooldown;
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        animator.SetBool("isWalking", false);
        animator.SetTrigger("undead atk");

        yield return new WaitForSeconds(attackDelay);

        isAttacking = false;
        ChangeState(EnemyState.Chase);
    }

    // ====================== 처형 ======================
    public bool CanExecute()
    {
        return hp <= 2 && currentState != EnemyState.Execute;
    }

    public void TryExecution(Transform playerTransform)
    {
        if (!CanExecute()) return;

        player = playerTransform;
        ChangeState(EnemyState.Execute);
    }

    IEnumerator ExecutionSequence()
    {
        animator.SetBool("isWalking", false);

        Rigidbody2D prb = player.GetComponent<Rigidbody2D>();
        PlayerMove pm = player.GetComponent<PlayerMove>();

        if (pm != null) pm.isExecuting = true;
        if (prb != null) prb.velocity = Vector2.zero;

        player.position = backPosition.position;

        SpriteRenderer playerSR = player.GetComponent<SpriteRenderer>();
        if (playerSR != null)
        {
            playerSR.flipX = isFacingRight;
        }

        yield return null;

        Animator playerAnim = player.GetComponent<Animator>();
        if (playerAnim != null)
        {
            playerAnim.ResetTrigger("Attack");
            playerAnim.SetTrigger("Execution");
        }

        yield return new WaitForSeconds(0.2f);

        animator.SetTrigger("undead death");

        yield return new WaitForSeconds(1.0f);

        if (pm != null) pm.isExecuting = false;

        Destroy(gameObject);
    }

    // ====================== 데미지 ======================
    public void TakeDamage(int damage)
    {
        if (currentState == EnemyState.Execute) return;

        hp -= damage;

        if (hp <= 2 && harvest_image != null)
            harvest_image.SetActive(true);

        if (hp <= 0)
        {
            ChangeState(EnemyState.Dead);
        }
        else
        {
            animator.SetTrigger("undead hurt");
        }
    }

    // ====================== 사망 ======================
    void Die()
    {
        StopAllCoroutines();

        animator.SetBool("isWalking", false);
        animator.SetTrigger("undead death");

        Destroy(gameObject, 1.8f);
    }
}