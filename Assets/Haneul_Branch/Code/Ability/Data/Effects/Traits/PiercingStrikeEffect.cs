using System.Collections.Generic;
using UnityEngine;

// [원거리] 꿰뚫는 일격 — 모든 투사체가 관통하고 피해가 늘어난다
[CreateAssetMenu(fileName = "Effect_PiercingStrike", menuName = "Harpe/Ability/Effect/꿰뚫는 일격")]
public class PiercingStrikeEffect : AbilityEffect
{
    [SerializeField] private float projectileDamage = 1.3f;

    public override bool GrantsProjectilePierce(AbilityContext ctx) { return true; }

    public override float DamageMultiplier(AbilityContext ctx, DamageQuery q)
    {
        return q.kind == DamageKind.Projectile ? projectileDamage : 1f;
    }
}
