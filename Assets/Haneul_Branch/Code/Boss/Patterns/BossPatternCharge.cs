using System.Collections;
using UnityEngine;

// 돌진. 플레이어 쪽으로 몸을 던진다. 여러 번 연달아 하면 장판처럼 방을 훑는다.
//
// 벽 감지는 돌진 좀비와 같은 방식이다 — 막히면 즉시 멈춘다.
// 안 그러면 보스가 플레이어를 벽 너머로 밀어 넣는다.
public class BossPatternCharge : BossPattern
{
    [Header("돌진")]
    [Min(0.1f)] [SerializeField] private float dashSpeed = 12f;
    [Min(0.05f)] [SerializeField] private float dashDuration = 0.35f;

    [Tooltip("연속으로 몇 번 돌진할지. 2 이상이면 매번 플레이어 쪽으로 다시 겨눈다")]
    [Min(1)] [SerializeField] private int dashCount = 1;

    [Tooltip("돌진 사이에 멈춰 서는 시간(초). 이 틈이 피할 구멍이 된다")]
    [Min(0.05f)] [SerializeField] private float betweenDashes = 0.4f;

    [Header("벽 감지")]
    [Tooltip("여기에 걸리면 즉시 멈춘다. 보통 Wall 레이어")]
    [SerializeField] private LayerMask wallLayer;
    [Min(0.01f)] [SerializeField] private float wallCheckDistance = 0.15f;

    [Header("연출")]
    [SerializeField] private float shake = 0.25f;

    private ContactFilter2D wallFilter;
    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[1];

    public override bool MovesSelf { get { return true; } }

    private void Awake()
    {
        wallFilter = new ContactFilter2D { useTriggers = false };
        wallFilter.SetLayerMask(wallLayer);
    }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        Rigidbody2D rb = boss.rb;
        if (rb == null) yield break;

        CollisionDetectionMode2D original = rb.collisionDetectionMode;
        // 빠르게 움직이는 동안 벽을 뚫고 나가지 않게 한다
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        for (int i = 0; i < dashCount && !boss.isDead; i++)
        {
            if (i > 0)
            {
                float wait = betweenDashes;
                while (wait > 0f && !boss.isDead)
                {
                    wait -= Time.deltaTime;
                    rb.velocity = Vector2.zero;
                    yield return null;
                }
                if (boss.isDead) break;
            }

            // 매 돌진마다 다시 겨눈다. 첫 방향만 쓰면 두 번째 돌진이 허공을 지른다.
            Vector2 dir = (i == 0) ? dirToPlayer : boss.AimAtPlayer();

            // 돌진마다 판정을 다시 연다 — 한 번 맞힌 뒤에도 다음 돌진이 들어가야 한다
            boss.RearmHitBox();
            if (shake > 0f) CameraShake.Shake(shake);

            float t = dashDuration;
            while (t > 0f && !boss.isDead)
            {
                t -= Time.deltaTime;
                rb.velocity = dir * dashSpeed;

                if (IsBlocked(rb, dir)) break;
                yield return null;
            }

            rb.velocity = Vector2.zero;
        }

        rb.velocity = Vector2.zero;
        rb.collisionDetectionMode = original;
    }

    private bool IsBlocked(Rigidbody2D rb, Vector2 dir)
    {
        if (wallLayer.value == 0) return false;
        return rb.Cast(dir, wallFilter, hitBuffer, wallCheckDistance) > 0;
    }
}
