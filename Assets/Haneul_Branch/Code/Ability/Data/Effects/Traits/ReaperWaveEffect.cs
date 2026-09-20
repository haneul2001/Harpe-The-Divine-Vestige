using System.Collections.Generic;
using UnityEngine;

// [원거리] 사신의 검기 / [패링] 반격의 칼날 — 전방으로 관통 검기를 날린다
[CreateAssetMenu(fileName = "Effect_ReaperWave", menuName = "Harpe/Ability/Effect/검기 발사")]
public class ReaperWaveEffect : AbilityEffect
{
    public enum Trigger { BasicAttack, ParrySuccess }

    [SerializeField] private Trigger trigger = Trigger.BasicAttack;
    [SerializeField] private float coefficient = 0.6f;
    [SerializeField] private float speed = 14f;
    [SerializeField] private float range = 6f;
    [SerializeField] private float radius = 0.6f;
    [SerializeField] private bool pierce = true;
    [SerializeField] private GameObject visual;
    [SerializeField] private float visualScale = 1f;
    [SerializeField] private Material visualMaterial;

    public override void OnSwing(AbilityContext ctx, SwingInfo swing)
    {
        if (trigger != Trigger.BasicAttack) return;
        float a = swing.angle * Mathf.Deg2Rad;
        Fire(ctx, swing.origin, new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
    }

    public override void OnParrySuccess(AbilityContext ctx, Enemy attacker)
    {
        if (trigger != Trigger.ParrySuccess) return;
        Vector3 from = ctx.BodyCenter;
        Vector2 dir = attacker != null ? (Vector2)(PlayerHitSparkVfx.BodyBounds(attacker).center - from) : Vector2.right;
        Fire(ctx, from, dir);
    }

    private void Fire(AbilityContext ctx, Vector3 from, Vector2 dir)
    {
        PlayerProjectile.Fire(from, dir, new PlayerProjectile.Settings
        {
            coefficient = coefficient, speed = speed, range = range, radius = radius, pierce = pierce,
            visual = visual, visualScale = visualScale, visualMaterial = visualMaterial,
            useFx = true, fx = FxSprite.Wave, fxScale = trigger == Trigger.ParrySuccess ? 1.2f : 1f,
        }, ctx);
    }
}
