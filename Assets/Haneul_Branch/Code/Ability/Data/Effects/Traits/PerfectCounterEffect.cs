using System.Collections.Generic;
using UnityEngine;

// [패링] 완벽한 반격 — 반격 피해 증가 + 반격 지점 충격파
[CreateAssetMenu(fileName = "Effect_PerfectCounter", menuName = "Harpe/Ability/Effect/완벽한 반격")]
public class PerfectCounterEffect : AbilityEffect
{
    [SerializeField] private float counterDamage = 2f;
    [SerializeField] private float radius = 2.5f;
    [SerializeField] private float coefficient = 0.8f;
    [SerializeField] private Color ringColor = new Color(1f, 0.9f, 0.5f, 0.9f);
    [SerializeField] private GameObject visual;
    [SerializeField] private float visualScale = 1.5f;

    public override float DamageMultiplier(AbilityContext ctx, DamageQuery q)
    {
        return q.kind == DamageKind.Parry ? counterDamage : 1f;
    }

    public override void OnParrySuccess(AbilityContext ctx, Enemy attacker)
    {
        Vector3 at = attacker != null ? attacker.transform.position : ctx.playerTransform.position;
        AbilityHooks.AreaDamage(ctx.status, at, radius, coefficient, attacker, ctx.playerTransform);
        SpriteFx.Ring(at, radius, ringColor, 0.35f);
        if (visual != null) TraitUtil.Explosion(visual, at, visualScale);
        CameraShake.Shake(0.2f);
    }
}
