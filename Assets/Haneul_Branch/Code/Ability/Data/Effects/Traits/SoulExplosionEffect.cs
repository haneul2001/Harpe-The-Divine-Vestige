using System.Collections.Generic;
using UnityEngine;

// [소울] 영혼 폭발 — 소울을 일정량 모을 때마다 주변 대규모 폭발
[CreateAssetMenu(fileName = "Effect_SoulExplosion", menuName = "Harpe/Ability/Effect/영혼 폭발")]
public class SoulExplosionEffect : AbilityEffect
{
    [SerializeField] private int soulPerBurst = 50;
    [SerializeField] private float radius = 5f;
    [SerializeField] private float coefficient = 2.5f;
    [SerializeField] private GameObject visual;
    [SerializeField] private float visualScale = 4f;
    [SerializeField] private Color ringColor = new Color(0.5f, 0.95f, 1f, 0.9f);

    private class S { public int gathered; }

    public override void OnSoulGained(AbilityContext ctx, int amount)
    {
        var st = ctx.State<S>(this);
        st.gathered += amount;
        while (st.gathered >= soulPerBurst)
        {
            st.gathered -= soulPerBurst;
            Vector3 at = ctx.BodyCenter;
            AbilityHooks.AreaDamage(ctx.status, at, radius, coefficient, null, ctx.playerTransform);
            SpriteFx.Burst(FxSprite.SoulBurst, at, radius, 0.7f);
            SpriteFx.Ring(at, radius, ringColor, 0.5f);
            CameraShake.Shake(0.35f);
            TraitUtil.Text(at, "영혼 폭발");
        }
    }
}
