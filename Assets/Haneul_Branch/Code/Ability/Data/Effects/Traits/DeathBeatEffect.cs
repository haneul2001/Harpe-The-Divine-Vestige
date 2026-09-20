using System.Collections.Generic;
using UnityEngine;

// [처치] 죽음의 박자 — N초 안에 연속 처치하면 다음 공격 피해 급증
[CreateAssetMenu(fileName = "Effect_DeathBeat", menuName = "Harpe/Ability/Effect/죽음의 박자")]
public class DeathBeatEffect : AbilityEffect
{
    [SerializeField] private int kills = 3;
    [SerializeField] private float window = 4f;
    [SerializeField] private float damage = 2.5f;
    [Tooltip("충전된 뒤 이 시간 안에 공격하지 않으면 사라진다")]
    [SerializeField] private float keep = 5f;

    private class S { public List<float> times = new List<float>(); public float armedUntil; }

    public override void OnEnemyKilled(AbilityContext ctx, Enemy enemy)
    {
        var st = ctx.State<S>(this);
        st.times.Add(Time.time);
        TraitUtil.Trim(st.times, window);
        if (st.times.Count < kills) return;

        st.times.Clear();
        st.armedUntil = Time.time + keep;
        TraitUtil.Text(ctx.playerTransform.position, "죽음의 박자");
    }

    public override float DamageMultiplier(AbilityContext ctx, DamageQuery q)
    {
        var st = ctx.State<S>(this);
        if (!q.kind.IsAttack() || Time.time >= st.armedUntil) return 1f;
        if (q.consume) st.armedUntil = 0f;
        return damage;
    }
}
