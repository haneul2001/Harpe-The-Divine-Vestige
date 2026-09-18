using System.Collections;
using UnityEngine;

// 돌진 좀비: 공격 예고 후 플레이어 방향으로 돌진하며 부딪힘.
public class ZombieEnemy : AttackEnemyBase
{
    [Header("돌진 설정")]
    [SerializeField] private float dashSpeed = 10f;
    [SerializeField] private float dashDuration = 0.3f;

    [Header("돌진 벽 감지")]
    [Tooltip("돌진 중 이 레이어(벽/장애물)에 막히면 즉시 멈춤")]
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float wallCheckDistance = 0.1f;

    private ContactFilter2D wallFilter;
    private readonly RaycastHit2D[] wallHitBuffer = new RaycastHit2D[1];
    private CollisionDetectionMode2D originalMode;

    protected override void OnStart()
    {
        wallFilter = new ContactFilter2D { useTriggers = false };
        wallFilter.SetLayerMask(wallLayer);
        originalMode = rb.collisionDetectionMode;
    }

    // 돌진은 발동 구간에 직접 이동하므로 그동안은 위치를 잠그지 않는다.
    protected override bool LockPositionDuringActivePhase => false;

    // 돌진하며 파고드는 거리만큼은 히트박스보다 멀리서 시작해야 한다.
    // (히트박스 도달 거리만 쓰면 이미 닿은 뒤에야 돌진을 시작해 의미가 없다)
    protected override float ExtraAttackReach => dashSpeed * dashDuration;

    protected override IEnumerator AttackActivePhase(Vector2 dir)
    {
        // Dynamic 유지(벽이 좀비를 막게 함) + 고속 이동 터널링 방지
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        float timer = dashDuration;
        while (timer > 0f && !isDead)
        {
            timer -= Time.deltaTime;
            rb.velocity = dir * dashSpeed;

            // 벽/장애물에 막히면 즉시 돌진 종료 (플레이어를 벽 너머로 밀지 않도록)
            if (IsBlockedByWall(dir))
            {
                rb.velocity = Vector2.zero;
                break;
            }

            yield return null;
        }

        rb.velocity = Vector2.zero;
    }

    protected override void OnAttackFinally()
    {
        if (rb != null) rb.collisionDetectionMode = originalMode;
    }

    // 돌진 방향으로 콜라이더를 살짝 캐스트해서 벽/장애물에 막혔는지 확인
    private bool IsBlockedByWall(Vector2 direction)
    {
        if (wallLayer.value == 0) return false;
        return rb.Cast(direction, wallFilter, wallHitBuffer, wallCheckDistance) > 0;
    }
}
