using System.Collections.Generic;
using UnityEngine;

// [처형] 죽음의 잔향 — 처형한 자리에 지속 피해 장판
[CreateAssetMenu(fileName = "Effect_DeathEcho", menuName = "Harpe/Ability/Effect/죽음의 잔향")]
public class DeathEchoEffect : AbilityEffect
{
    [SerializeField] private float radius = 2f;
    [SerializeField] private float duration = 3f;
    [SerializeField] private float tickInterval = 0.5f;
    [SerializeField] private float coefficient = 0.25f;
    [SerializeField] private Color areaColor = new Color(0.75f, 0.1f, 0.25f, 0.45f);
    [SerializeField] private GameObject visual;
    [SerializeField] private float visualScale = 1.5f;

    private class Zone { public Vector3 pos; public float end; public float next; }
    private class S { public List<Zone> zones = new List<Zone>(); }

    public override void OnHarvestImpact(AbilityContext ctx, HarvestImpact impact)
    {
        ctx.State<S>(this).zones.Add(new Zone { pos = impact.position, end = Time.time + duration, next = Time.time + tickInterval });
        SpriteFx.Zone(impact.position, radius, duration);
    }

    public override void OnTick(AbilityContext ctx, float dt)
    {
        var st = ctx.State<S>(this);
        for (int i = st.zones.Count - 1; i >= 0; i--)
        {
            Zone z = st.zones[i];
            if (Time.time >= z.end) { st.zones.RemoveAt(i); continue; }
            if (Time.time < z.next) continue;
            z.next += tickInterval;
            AbilityHooks.AreaDamage(ctx.status, z.pos, radius, coefficient, null, ctx.playerTransform);
        }
    }
}
