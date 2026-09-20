using System.Collections.Generic;
using UnityEngine;

// [처치] 피의 광기 — 적을 처치하면 잠시 공격력·공격속도 증가
[CreateAssetMenu(fileName = "Effect_BloodFrenzy", menuName = "Harpe/Ability/Effect/피의 광기")]
public class BloodFrenzyEffect : AbilityEffect
{
    [SerializeField] private float duration = 3f;
    [SerializeField] private float damage = 1.2f;
    [SerializeField] private float attackSpeed = 0.2f;

    private class S { public float until; }

    public override void OnEnemyKilled(AbilityContext ctx, Enemy enemy)
    {
        ctx.State<S>(this).until = Time.time + duration;
    }

    private bool Active(AbilityContext ctx) { return Time.time < ctx.State<S>(this).until; }

    public override float DamageMultiplier(AbilityContext ctx, DamageQuery q) { return Active(ctx) ? damage : 1f; }
    public override float AttackSpeedBonus(AbilityContext ctx) { return Active(ctx) ? attackSpeed : 0f; }
}
