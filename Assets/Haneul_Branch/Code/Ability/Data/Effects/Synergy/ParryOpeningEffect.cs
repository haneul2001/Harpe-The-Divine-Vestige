using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [패링 세트] 반격의 기회 — 패링 후 조작 불가 시간 단축 + 반격 시 주변 적 경직
[CreateAssetMenu(fileName = "Synergy_ParryOpening", menuName = "Harpe/Ability/Synergy/반격의 기회")]
public class ParryOpeningEffect : AbilityEffect
{
    [SerializeField] private float lockReduction = 0.25f;
    [SerializeField] private float staggerRadius = 2.5f;
    [SerializeField] private float staggerDuration = 1.5f;
    [Tooltip("경직을 다시 거는 간격 — 피격 경직 한 번은 짧아서 이 간격으로 이어 붙인다")]
    [SerializeField] private float restaggerInterval = 0.3f;

    private class Held { public Enemy enemy; public float until; public float next; }
    private class S { public List<Held> held = new List<Held>(); }

    public override float ParryLockReduction(AbilityContext ctx) { return lockReduction; }

    public override void OnParrySuccess(AbilityContext ctx, Enemy attacker)
    {
        Vector3 at = attacker != null ? attacker.transform.position : ctx.playerTransform.position;
        var st = ctx.State<S>(this);
        foreach (Enemy e in AbilityHooks.EnemiesInRadius(at, staggerRadius))
        {
            e.Stagger();
            st.held.Add(new Held { enemy = e, until = Time.time + staggerDuration, next = Time.time + restaggerInterval });
        }
    }

    public override void OnTick(AbilityContext ctx, float dt)
    {
        var st = ctx.State<S>(this);
        for (int i = st.held.Count - 1; i >= 0; i--)
        {
            Held h = st.held[i];
            if (h.enemy == null || h.enemy.isDead || Time.time >= h.until) { st.held.RemoveAt(i); continue; }
            if (Time.time < h.next) continue;
            h.next = Time.time + restaggerInterval;
            h.enemy.Stagger();
        }
    }
}
