using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [처형 세트] 처형의 여파 — 처형 지점 주변 적을 밀쳐내며 피해
[CreateAssetMenu(fileName = "Synergy_HarvestAftermath", menuName = "Harpe/Ability/Synergy/처형의 여파")]
public class HarvestAftermathEffect : AbilityEffect
{
    [SerializeField] private float radius = 2f;
    [SerializeField] private float coefficient = 0.6f;
    [SerializeField] private float pushDistance = 1.2f;
    [SerializeField] private Color ringColor = new Color(0.9f, 0.85f, 1f, 0.8f);

    public override void OnHarvestImpact(AbilityContext ctx, HarvestImpact impact)
    {
        foreach (Enemy e in AbilityHooks.EnemiesInRadius(impact.position, radius))
        {
            if (e == impact.target) continue;
            bool crit;
            int dmg = AbilityHooks.RollDamage(ctx.status, coefficient, DamageKind.Area, e, out crit);
            AbilityHooks.Deal(e, dmg, crit, DamageKind.Area, impact.position, ctx.playerTransform);
            TraitUtil.Push(ctx, e, impact.position, pushDistance, 0.15f);
        }
        SpriteFx.Ring(impact.position, radius, ringColor, 0.3f);
    }
}
