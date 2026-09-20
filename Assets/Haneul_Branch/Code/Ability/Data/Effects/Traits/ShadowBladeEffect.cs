using System.Collections.Generic;
using UnityEngine;

// [은신] 그림자 칼날 — 은신에서 나온 첫 공격은 무조건 치명타 + 큰 피해
[CreateAssetMenu(fileName = "Effect_ShadowBlade", menuName = "Harpe/Ability/Effect/그림자 칼날")]
public class ShadowBladeEffect : AbilityEffect
{
    [SerializeField] private float damage = 2.5f;
    [Tooltip("은신이 풀린 뒤 이 시간 안에 공격해야 한다")]
    [SerializeField] private float keep = 1.5f;

    private class S { public float until; }

    private bool Armed(AbilityContext ctx) { return PlayerStealth.IsHidden || Time.time < ctx.State<S>(this).until; }

    public override void OnStealthExit(AbilityContext ctx) { ctx.State<S>(this).until = Time.time + keep; }

    public override bool ForcesCrit(AbilityContext ctx, DamageKind kind) { return kind.IsAttack() && Armed(ctx); }

    public override float DamageMultiplier(AbilityContext ctx, DamageQuery q)
    {
        if (!q.kind.IsAttack() || !Armed(ctx)) return 1f;
        if (q.consume) ctx.State<S>(this).until = 0f;
        return damage;
    }
}
