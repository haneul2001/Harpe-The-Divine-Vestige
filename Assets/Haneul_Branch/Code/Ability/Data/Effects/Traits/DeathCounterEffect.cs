using System.Collections.Generic;
using UnityEngine;

// [패링] 죽음의 반격 — 패링 성공 후 다음 차징/막타의 속도·피해 극대화
[CreateAssetMenu(fileName = "Effect_DeathCounter", menuName = "Harpe/Ability/Effect/죽음의 반격")]
public class DeathCounterEffect : AbilityEffect
{
    [SerializeField] private float keep = 3f;
    [SerializeField] private float damage = 3f;
    [SerializeField] private float attackSpeed = 0.5f;

    private class S { public float until; }

    private bool Armed(AbilityContext ctx) { return Time.time < ctx.State<S>(this).until; }

    public override void OnParrySuccess(AbilityContext ctx, Enemy attacker)
    {
        ctx.State<S>(this).until = Time.time + keep;
        TraitUtil.Text(ctx.playerTransform.position, "죽음의 반격");
    }

    public override float AttackSpeedBonus(AbilityContext ctx) { return Armed(ctx) ? attackSpeed : 0f; }

    public override float DamageMultiplier(AbilityContext ctx, DamageQuery q)
    {
        if (!Armed(ctx) || (q.kind != DamageKind.Charged && q.kind != DamageKind.Finisher)) return 1f;
        if (q.consume) ctx.State<S>(this).until = 0f;
        return damage;
    }
}
