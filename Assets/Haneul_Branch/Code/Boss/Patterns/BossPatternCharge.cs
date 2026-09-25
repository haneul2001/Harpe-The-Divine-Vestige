using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

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

    [Tooltip("돌진을 시작하고 이만큼 뒤에 벤다(초).\n"
           + "0이면 나가는 순간 같이 베어 '제자리에서 베고 이동'처럼 보인다 — 조금 늦춰야 전진베기로 읽힌다")]
    [Min(0f)] [SerializeField] private float slashDelay = 0.12f;

    private ContactFilter2D wallFilter;
    // 겹쳐 있는 콜라이더가 자리를 차지하므로 넉넉히 둔다 — 한 칸이면 진짜 벽이 밀려 나간다
    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[8];

    public override bool MovesSelf { get { return true; } }

    // 예고는 "칼이 닿는 자리"가 아니라 "돌진해서 훑고 지나갈 자리" 전체다.
    // 히트박스 크기만 보여 주면 앞쪽만 위험한 줄 알고 서 있다가 지나가는 몸에 쓸린다.
    public override DangerShape[] DangerShapes(BossEnemy boss)
    {
        DangerShape box = HitBoxShape(boss);
        if (!box.Valid) return new DangerShape[] { box };

        float run = dashSpeed * dashDuration;
        var size = new Vector2(box.size.x + run, box.size.y);

        // 베기 모양대로 부채꼴이되, 반지름은 돌진해서 훑고 지나갈 거리까지다.
        // 보스 배율로 다시 재지 않는다 — 돌진 거리는 몸 크기와 무관한 월드 값이다.
        return new DangerShape[] { DangerShape.SectorFromBox(size, DangerOrigin.Boss) };
    }

    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration)
    {
        DangerShape s = DangerShapes(boss)[0];
        if (!s.Valid) return null;

        float angle = Mathf.Atan2(dirToPlayer.y, dirToPlayer.x) * Mathf.Rad2Deg;

        // 보스를 따라다니면 안 된다 — 돌진하는 동안 칸까지 같이 밀려가면 늘 앞이 비어 보인다
        return DangerZone.Sector(boss.transform.position, s.Radius, s.halfAngle, angle, duration);
    }

    // 베기는 돌진이 시작된 뒤에 직접 띄운다 — 베이스가 판정과 동시에 띄우면
    // 앞으로 나가는 동작과 베는 그림이 한 순간에 겹쳐 "제자리에서 벴다"로 보인다
    public override bool DrawsOwnSlash { get { return slashDelay > 0.001f; } }

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

        // 돌진하는 동안에는 플레이어를 뚫고 지나간다.
        // 몸통은 이미 통과 설정이지만 발 판정 같은 곁다리 콜라이더가 남아 있어,
        // 플레이어가 길 한가운데 서 있으면 보스가 거기서 걸려 제자리걸음만 한다.
        SetPassThroughPlayer(boss, true);

        for (int i = 0; i < dashCount && !boss.isDead; i++)
        {
            // 매 돌진마다 다시 겨눈다. 첫 방향만 쓰면 두 번째 돌진이 허공을 지른다.
            Vector2 dir = (i == 0) ? dirToPlayer : boss.AimAtPlayer();

            if (i > 0)
            {
                // 두 번째 돌진부터는 나갈 자리를 먼저 보여 주고, 동작도 새로 건다.
                // 안 그러면 보스는 한 번 휘두른 자세 그대로 세 번 미끄러진다.
                float untilHit = boss.AttackHitTimeOf(AnimationIndex);
                DangerZone zone = ShowDashDanger(boss, dir, betweenDashes + untilHit);

                yield return Hold(boss, betweenDashes);
                if (boss.isDead) { if (zone != null) zone.Cancel(); break; }

                boss.PlayAttackAnimation(AnimationIndex);

                // 칼이 닿는 순간에 맞춰 나간다 — 예고가 다 차는 시점이기도 하다
                yield return boss.WaitForNextAnimHit(untilHit + 1f);
                if (boss.isDead) { if (zone != null) zone.Cancel(); break; }

                if (zone != null) zone.CompleteNow();
            }

            // 돌진마다 판정을 다시 연다 — 한 번 맞힌 뒤에도 다음 돌진이 들어가야 한다
            boss.RearmHitBox();
            if (shake > 0f) CameraShake.Shake(shake);

            float t = dashDuration;
            bool slashed = !DrawsOwnSlash;      // 베이스가 이미 띄웠으면 다시 안 띄운다
            while (t > 0f && !boss.isDead)
            {
                t -= Time.deltaTime;
                rb.velocity = dir * dashSpeed;

                // 몸이 먼저 나가고, 조금 뒤에 칼이 따라 나간다
                if (!slashed && dashDuration - t >= slashDelay)
                {
                    slashed = true;
                    boss.PlaySlashNow(dir);
                }

                if (IsBlocked(rb, dir)) break;
                yield return null;
            }

            // 벽에 막혀 일찍 끊겨도 벤 그림은 나와야 한다
            if (!slashed && !boss.isDead) boss.PlaySlashNow(dir);

            rb.velocity = Vector2.zero;
        }

        rb.velocity = Vector2.zero;
        rb.collisionDetectionMode = original;
        SetPassThroughPlayer(boss, false);
    }

    // 돌진 동안만 플레이어와의 물리 충돌을 끈다.
    //
    // 레이어 표에서 이미 무시하기로 한 짝은 건드리지 않는다 —
    // IgnoreCollision은 표보다 세서, 여기서 false로 되돌리면 원래 통과하던 몸통까지 다시 부딪힌다.
    private void SetPassThroughPlayer(BossEnemy boss, bool pass)
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;

        Collider2D[] mine = boss.GetComponentsInChildren<Collider2D>(true);
        Collider2D[] theirs = p.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < mine.Length; i++)
        {
            if (mine[i] == null || mine[i].isTrigger) continue;

            for (int j = 0; j < theirs.Length; j++)
            {
                if (theirs[j] == null || theirs[j].isTrigger) continue;
                if (Physics2D.GetIgnoreLayerCollision(mine[i].gameObject.layer, theirs[j].gameObject.layer)) continue;

                Physics2D.IgnoreCollision(mine[i], theirs[j], pass);
            }
        }
    }

    // 이번 돌진이 훑고 지나갈 자리. 예고 부채꼴과 같은 크기다.
    private DangerZone ShowDashDanger(BossEnemy boss, Vector2 dir, float untilHit)
    {
        DangerShape s = DangerShapes(boss)[0];
        if (!s.Valid) return null;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        DangerZone zone = DangerZone.Sector(boss.transform.position, s.Radius, s.halfAngle, angle, untilHit);
        return zone.HoldUntilHit().FillIn(Mathf.Max(0.1f, untilHit * 0.75f));
    }

    // 다음 돌진을 기다리는 동안 제자리에 세워 둔다
    private IEnumerator Hold(BossEnemy boss, float seconds)
    {
        float t = seconds;
        while (t > 0f && !boss.isDead)
        {
            t -= Time.deltaTime;
            if (boss.rb != null) boss.rb.velocity = Vector2.zero;
            yield return null;
        }
    }

    private bool IsBlocked(Rigidbody2D rb, Vector2 dir)
    {
        if (wallLayer.value == 0) return false;

        int hits = rb.Cast(dir, wallFilter, hitBuffer, wallCheckDistance);
        for (int i = 0; i < hits; i++)
        {
            // 거리 0은 "앞에 벽이 있다"가 아니라 "이미 겹쳐 있다"는 뜻이다.
            // 보스는 덩치가 커서 방 안에 서 있기만 해도 벽 타일맵과 늘 맞닿아 있는데,
            // 이걸 막힘으로 치면 돌진이 첫 프레임에 끊겨 제자리에서 칼만 휘두르게 된다.
            if (hitBuffer[i].distance <= 0.0001f) continue;

            // 진짜 벽(타일맵)만 돌진을 끊는다.
            // 이 판정을 두는 이유는 "플레이어를 벽 너머로 밀어 넣지 않기 위해"인데,
            // 방에 걸린 쇠사슬 같은 소품까지 벽 레이어에 있어서 보스 옆을 스치기만 해도
            // 돌진이 통째로 취소돼 버린다. 소품은 뚫고 지나가도 문제가 없다.
            if (!IsWallGeometry(hitBuffer[i].collider)) continue;

            return true;
        }
        return false;
    }

    private static bool IsWallGeometry(Collider2D col)
    {
        if (col == null) return false;
        return col is TilemapCollider2D || col is CompositeCollider2D;
    }
}
