using System.Collections;
using UnityEngine;

public class ZombieEnemy : Enemy, IEnemyAttack
{
    [Header("좀비 공격 설정")]
    [SerializeField] private GameObject attackHitBox;
    [SerializeField] private GameObject attackRangeBox;
    [SerializeField] private GameObject attackPivot;
    private AttackRangeSet attackRangeSet;
    private EnemyHitBox hitBox;

    [Header("돌진 설정")]
    [SerializeField] private float dashSpeed = 10f;
    [SerializeField] private float dashDuration = 0.3f;

    [Header("돌진 벽 감지")]
    [Tooltip("돌진 중 이 레이어(벽/장애물)에 막히면 즉시 멈춤")]
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float wallCheckDistance = 0.1f;
    private ContactFilter2D wallFilter;
    private readonly RaycastHit2D[] wallHitBuffer = new RaycastHit2D[1];

    public bool IsRunning => isAttacking;
    public bool IsGroundOnly => false; // 근접 대시 = 지상+공중 다 맞힘

    protected override void Start()
    {
        base.Start();

        attackRangeSet = attackPivot.GetComponent<AttackRangeSet>();
        hitBox = attackHitBox.GetComponent<EnemyHitBox>();
        hitBox.Initialize(this);

        attackHitBox.SetActive(false);
        attackRangeBox.SetActive(false);

        wallFilter = new ContactFilter2D { useTriggers = false };
        wallFilter.SetLayerMask(wallLayer);
    }

    public void ShowRange(bool show)
    {
        attackRangeBox.SetActive(show);
    }

    public void Execute()
    {
        if (isAttacking)
            return;

        isAttacking = true;

        StartCoroutine(DashAttackCoroutine());
    }

    private IEnumerator DashAttackCoroutine()
    {
        CollisionDetectionMode2D originalMode = rb.collisionDetectionMode;

        try
        {
            // Dynamic 유지(벽이 좀비를 막게 함) + 고속 이동 터널링 방지
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            StopMove();

            FaceToPlayer();

            Vector2 dashDirection =
                (player.position - transform.position).normalized;

            attackRangeSet.SetDirection(dashDirection);

            attackRangeBox.SetActive(true);
            yield return new WaitForSeconds(attackWarningDuration);

            attackRangeBox.SetActive(false);

            animator.SetTrigger("attack");

            hitBox.ResetHit();
            attackHitBox.SetActive(true);

            float timer = dashDuration;
            while (timer > 0f && !isDead)
            {
                timer -= Time.deltaTime;
                rb.velocity = dashDirection * dashSpeed;

                // 벽/장애물에 막히면 즉시 돌진 종료 (플레이어를 벽 너머로 밀지 않도록)
                if (IsBlockedByWall(dashDirection))
                {
                    rb.velocity = Vector2.zero;
                    break;
                }

                yield return null;
            }

            rb.velocity = Vector2.zero;
            attackHitBox.SetActive(false);

            yield return new WaitForSeconds(1f);
        }
        finally
        {
            if (rb != null) rb.collisionDetectionMode = originalMode;
            lastAttackTime = Time.time;
            isAttacking = false;
        }
    }

    // 돌진 방향으로 콜라이더를 살짝 캐스트해서 벽/장애물에 막혔는지 확인
    private bool IsBlockedByWall(Vector2 direction)
    {
        if (wallLayer.value == 0) return false;
        return rb.Cast(direction, wallFilter, wallHitBuffer, wallCheckDistance) > 0;
    }

    private void OnDisable()
    {
        if (attackHitBox != null) attackHitBox.SetActive(false);
        if (attackRangeBox != null) attackRangeBox.SetActive(false);
    }
}
