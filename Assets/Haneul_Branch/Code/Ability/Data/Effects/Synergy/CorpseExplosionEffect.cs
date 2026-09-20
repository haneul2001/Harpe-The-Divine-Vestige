using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [처치 세트] 시체 폭발 — 처치한 적 자리에서 폭발
// 폭발로 죽은 적은 다시 터지지 않는다 (연쇄 폭발로 방이 한 번에 비는 것 방지)
[CreateAssetMenu(fileName = "Synergy_CorpseExplosion", menuName = "Harpe/Ability/Synergy/시체 폭발")]
public class CorpseExplosionEffect : AbilityEffect
{
    [SerializeField] private float radius = 2f;
    [SerializeField] private float coefficient = 0.4f;
    [SerializeField] private GameObject visual;
    [SerializeField] private float visualScale = 1.5f;

    private class S { public bool exploding; }

    public override void OnEnemyKilled(AbilityContext ctx, Enemy enemy)
    {
        var st = ctx.State<S>(this);
        if (st.exploding || enemy == null) return;

        st.exploding = true;
        try
        {
            Vector3 at = PlayerHitSparkVfx.BodyBounds(enemy).center;
            AbilityHooks.AreaDamage(ctx.status, at, radius, coefficient, enemy, ctx.playerTransform);
            SpriteFx.Burst(FxSprite.SkullBurst, at, radius * 0.8f, 0.45f);
        }
        finally { st.exploding = false; }
    }
}
