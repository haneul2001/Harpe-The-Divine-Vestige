using System.Collections.Generic;
using UnityEngine;

// [소울] 영혼의 그릇 — 보유 소울이 많을수록 피해 증가
[CreateAssetMenu(fileName = "Effect_SoulVessel", menuName = "Harpe/Ability/Effect/영혼의 그릇")]
public class SoulVesselEffect : AbilityEffect
{
    [Tooltip("소울이 가득 찼을 때의 피해 증가 (0.4 = +40%)")]
    [SerializeField] private float maxBonus = 0.4f;

    public override float DamageMultiplier(AbilityContext ctx, DamageQuery q)
    {
        if (ctx.status == null || ctx.status.MaxSoul <= 0) return 1f;
        return 1f + maxBonus * Mathf.Clamp01((float)ctx.status.CurrentSoul / ctx.status.MaxSoul);
    }
}
