using System.Collections;
using UnityEngine;

// 사신의 초승달 베기. 앞쪽 큰 반원을 한 번에 긋는다.
// 4페이즈부터는 앞을 벤 뒤 곧바로 몸을 돌려 뒤까지 벤다 — 등 뒤로 돌아 피한 자리를 노린다.
public class BossPatternReaperCrescent : BossPattern
{
    [Header("초승달")]
    [SerializeField] private float radius = 6f;
    [Range(30f, 90f)] [SerializeField] private float halfAngle = 90f;
    [SerializeField] private int damage = 18;
    [Tooltip("이 페이즈(0부터)부터 앞→뒤 2연속")]
    [SerializeField] private int doubleFromPhase = 3;
    [SerializeField] private float backWarn = 0.55f;

    public override bool UsesHitBox { get { return false; } }
    public override bool DrawsOwnSlash { get { return true; } }

    public override DangerShape[] DangerShapes(BossEnemy boss)
    {
        return new DangerShape[] { DangerShape.Sector(radius, halfAngle, DangerOrigin.Boss) };
    }

    private static float FacingAngle(BossEnemy boss) { return boss.IsFacingRight ? 0f : 180f; }

    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration)
    {
        return DangerZone.Sector(boss.transform.position, radius, halfAngle, FacingAngle(boss), duration, boss.transform);
    }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        ReaperFx.CrescentHit(boss, boss.transform.position, radius, halfAngle, FacingAngle(boss), damage);

        var r = boss as ReaperBoss;
        if (r == null || boss.PhaseIndex < doubleFromPhase) yield break;

        // 뒤로 한 번 더
        yield return ReaperFx.Wait(boss, 0.12f);
        r.Turn();
        float back = FacingAngle(boss);
        DangerZone zone = DangerZone.Sector(boss.transform.position, radius, halfAngle, back, backWarn);
        yield return ReaperFx.Wait(boss, backWarn * 0.6f);
        boss.PlayAttackAnimation(0);
        yield return boss.WaitForNextAnimHit(backWarn);
        if (zone != null) zone.CompleteNow();
        ReaperFx.CrescentHit(boss, boss.transform.position, radius, halfAngle, back, damage);
    }
}
