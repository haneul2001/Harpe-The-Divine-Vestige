using System.Collections.Generic;
using UnityEngine;

// [대시] 질주하는 칼날 — 대시(Shift)로 지나간 길과 도착 지점에 공격 판정
[CreateAssetMenu(fileName = "Effect_RunningBlade", menuName = "Harpe/Ability/Effect/질주하는 칼날")]
public class RunningBladeEffect : AbilityEffect
{
    [SerializeField] private float coefficient = 0.6f;
    [SerializeField] private float pathRadius = 0.7f;
    [SerializeField] private float endRadius = 1.3f;
    [SerializeField] private float endReach = 0.8f;

    public override void OnDashEnd(AbilityContext ctx, Vector2 start, Vector2 end)
    {
        Vector2 dir = end - start;
        float len = dir.magnitude;
        if (len < 0.05f) return;
        dir /= len;

        Vector2 lift = Vector2.up * 0.32f;
        var hit = new HashSet<Enemy>();
        for (float d = 0f; d <= len; d += pathRadius * 0.8f)
            foreach (Enemy e in AbilityHooks.EnemiesInRadius(start + lift + dir * d, pathRadius)) hit.Add(e);

        Vector2 slashAt = end + lift + dir * endReach;
        foreach (Enemy e in AbilityHooks.EnemiesInRadius(slashAt, endRadius)) hit.Add(e);

        foreach (Enemy e in hit)
        {
            bool crit;
            int dmg = AbilityHooks.RollDamage(ctx.status, coefficient, DamageKind.Area, e, out crit);
            AbilityHooks.Deal(e, dmg, crit, DamageKind.Area, start + lift, ctx.playerTransform);
        }

        var slash = ctx.player != null ? ctx.player.GetComponent<PlayerSlashVfx>() : null;
        if (slash != null) slash.Play(slashAt, Mathf.Round(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg / 45f) * 45f, false);
    }
}
