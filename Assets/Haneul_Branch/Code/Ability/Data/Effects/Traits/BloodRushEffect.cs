using System.Collections.Generic;
using UnityEngine;

// [대시] 피의 돌진 — 대시 직후 첫 공격이 더 멀리 파고들며 피해 증가
[CreateAssetMenu(fileName = "Effect_BloodRush", menuName = "Harpe/Ability/Effect/피의 돌진")]
public class BloodRushEffect : AbilityEffect
{
    [SerializeField] private float keep = 1.5f;
    [SerializeField] private float damage = 1.5f;
    [Tooltip("추가 전진 거리(유닛)")]
    [SerializeField] private float lunge = 0.8f;

    private class S { public float until; }

    private bool Armed(AbilityContext ctx) { return Time.time < ctx.State<S>(this).until; }

    public override void OnDashEnd(AbilityContext ctx, Vector2 start, Vector2 end) { ctx.State<S>(this).until = Time.time + keep; }

    public override float LungeBonus(AbilityContext ctx, bool consume) { return Armed(ctx) ? lunge : 0f; }

    public override float DamageMultiplier(AbilityContext ctx, DamageQuery q)
    {
        if (!q.kind.IsAttack() || !Armed(ctx)) return 1f;
        if (q.consume) ctx.State<S>(this).until = 0f;
        return damage;
    }
}
