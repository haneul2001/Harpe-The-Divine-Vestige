using System.Collections.Generic;
using UnityEngine;

// [스탯] 광전사의 심장 — 잃은 체력에 비례해 모든 피해 증가
[CreateAssetMenu(fileName = "Effect_BerserkerHeart", menuName = "Harpe/Ability/Effect/광전사의 심장")]
public class BerserkerHeartEffect : AbilityEffect
{
    [Tooltip("체력이 0에 가까울 때 최대 피해 증가 (1 = +100%)")]
    [SerializeField] private float maxBonus = 1f;

    public override float DamageMultiplier(AbilityContext ctx, DamageQuery q)
    {
        if (ctx.status == null || ctx.status.MaxHp <= 0) return 1f;
        float lost = 1f - Mathf.Clamp01((float)ctx.status.CurrentHp / ctx.status.MaxHp);
        return 1f + maxBonus * AbilityHooks.StatAmplify() * lost;   // 스탯 증폭이 최대 증가폭을 키운다
    }
}
