using System.Collections.Generic;
using UnityEngine;

// [처형] 잔혹한 집행 — 처형하면 주변 적에게 광역 폭발
// [처형] 연쇄 집행 — 폭발 뒤 처형 체력에 들어온 적을 이어서 자동 처형
[CreateAssetMenu(fileName = "Effect_HarvestExplosion", menuName = "Harpe/Ability/Effect/처형 폭발")]
public class HarvestExplosionEffect : AbilityEffect
{
    [SerializeField] private float radius = 3f;
    [SerializeField] private float coefficient = 1.5f;
    [SerializeField] private GameObject visual;
    [SerializeField] private float visualScale = 2.5f;
    [SerializeField] private Color ringColor = new Color(1f, 0.35f, 0.35f, 0.9f);
    [SerializeField] private float shake = 0.4f;

    [Header("연쇄 집행")]
    [Tooltip("켜면 폭발 뒤 반경 안의 처형 가능한 적을 이어서 처형한다")]
    [SerializeField] private bool chainExecute = false;
    [Tooltip("한 번의 연쇄에서 이어서 처형할 최대 마릿수")]
    [SerializeField] private int maxChain = 4;

    private class S { public Queue<Enemy> queue = new Queue<Enemy>(); public int chained; public bool running; }

    public override void OnHarvestImpact(AbilityContext ctx, HarvestImpact impact)
    {
        int hits = AbilityHooks.AreaDamage(ctx.status, impact.position, radius, coefficient, impact.target, ctx.playerTransform);
        SpriteFx.Burst(FxSprite.SkullBurst, impact.position + Vector3.up * 0.3f, radius);
        SpriteFx.Ring(impact.position, radius, ringColor, 0.35f);
        if (hits > 0 && shake > 0f) CameraShake.Shake(shake);

        if (!chainExecute) return;

        var st = ctx.State<S>(this);
        if (!st.running) st.chained = 0;
        foreach (Enemy e in AbilityHooks.EnemiesInRadius(impact.position, radius))
            if (e != impact.target && e.CanHarvest && !st.queue.Contains(e)) st.queue.Enqueue(e);
        st.running = st.queue.Count > 0;
    }

    public override void OnTick(AbilityContext ctx, float dt)
    {
        if (!chainExecute) return;
        var st = ctx.State<S>(this);
        if (st.queue.Count == 0) { st.running = false; return; }

        HarvestManager hm = HarvestManager.Instance;
        if (hm == null || hm.IsHarvesting || ctx.player == null) return;

        while (st.queue.Count > 0)
        {
            Enemy next = st.queue.Dequeue();
            if (next == null || next.isDead || !next.CanHarvest || next.BackPosition == null) continue;
            if (st.chained >= maxChain) { st.queue.Clear(); break; }

            st.chained++;
            TraitUtil.Text(next.transform.position, "연쇄 집행");
            hm.ExecuteHarvest(next, ctx.playerTransform,
                ctx.player.GetComponentInChildren<Animator>(), ctx.player.GetComponentInChildren<SpriteRenderer>());
            break;
        }
    }
}
