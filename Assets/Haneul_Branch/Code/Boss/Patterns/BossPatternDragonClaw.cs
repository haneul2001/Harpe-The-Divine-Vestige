using System.Collections;
using UnityEngine;

// 용의 전방 할퀴기. 앞쪽 반원을 한 번 크게 긁는다.
//
// 근접 판정 상자를 쓰지 않고 예고한 반원 그대로 피해를 굴린다 —
// 용은 옆모습이라 발밑에서 앞으로 벌어지는 반원이 발톱이 지나가는 자리와 가장 가깝다.
public class BossPatternDragonClaw : BossPattern
{
    [Header("할퀴기")]
    [Min(0.5f)] [SerializeField] private float radius = 3.6f;
    [Range(10f, 90f)] [SerializeField] private float halfAngle = 80f;
    [Min(1)] [SerializeField] private int damage = 16;

    [Tooltip("반원의 꼭짓점을 발밑에서 앞으로 얼마나 밀지 (용 몸통 가운데가 발밑보다 뒤에 있다)")]
    [SerializeField] private float apexForward = 0.2f;

    [SerializeField] private float shake = 0.3f;

    public override bool UsesHitBox { get { return false; } }
    public override bool DrawsOwnSlash { get { return true; } }

    public override DangerShape[] DangerShapes(BossEnemy boss)
    {
        return new DangerShape[] { DangerShape.Sector(radius, halfAngle, DangerOrigin.Boss) };
    }

    private Vector2 Apex(BossEnemy boss, Vector2 dir)
    {
        return (Vector2)boss.transform.position + dir * apexForward;
    }

    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration)
    {
        Vector2 dir = Facing(boss, dirToPlayer);
        return DangerZone.Sector(Apex(boss, dir), radius, halfAngle, Angle(dir), duration, boss.transform);
    }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        Vector2 dir = Facing(boss, dirToPlayer);
        Vector2 apex = Apex(boss, dir);

        // 그림은 예고 칸을 덮는다 — 반원을 가로로 긴 베기 한 장으로 채운다
        PixelVfx.PlayStretched("BossSlash", apex + dir * radius * 0.5f + Vector2.up * 0.8f, Angle(dir),
                               new Vector2(radius, radius * 1.6f));
        if (shake > 0f) CameraShake.Shake(shake);

        boss.DamagePlayerInSector(apex, radius, halfAngle, Angle(dir), damage);
        yield return null;
    }

    // 옆모습 용은 좌우로만 할퀸다
    private static Vector2 Facing(BossEnemy boss, Vector2 dir)
    {
        if (Mathf.Abs(dir.x) < 0.01f) return boss.IsFacingRight ? Vector2.right : Vector2.left;
        return dir.x > 0f ? Vector2.right : Vector2.left;
    }

    private static float Angle(Vector2 d) { return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; }
}
