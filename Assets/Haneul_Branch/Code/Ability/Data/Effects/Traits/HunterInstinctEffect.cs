using System.Collections.Generic;
using UnityEngine;

// [처치] 사냥꾼의 본능 — 주변 적이 많을수록 피해 증가
[CreateAssetMenu(fileName = "Effect_HunterInstinct", menuName = "Harpe/Ability/Effect/사냥꾼의 본능")]
public class HunterInstinctEffect : AbilityEffect
{
    [SerializeField] private float radius = 4f;
    [SerializeField] private float perEnemy = 0.06f;
    [SerializeField] private float maxBonus = 0.3f;

    public override float DamageMultiplier(AbilityContext ctx, DamageQuery q)
    {
        if (ctx.playerTransform == null) return 1f;
        int n = AbilityHooks.EnemiesInRadius(ctx.playerTransform.position, radius).Count;
        return 1f + Mathf.Min(maxBonus, n * perEnemy);
    }
}
