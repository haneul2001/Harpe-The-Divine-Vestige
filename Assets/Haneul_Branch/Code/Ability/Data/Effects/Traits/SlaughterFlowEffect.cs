using System.Collections.Generic;
using UnityEngine;

// [처치] 학살의 흐름 — 처치할 때마다 스택, 스택 단계마다 공격 능력 강화
[CreateAssetMenu(fileName = "Effect_SlaughterFlow", menuName = "Harpe/Ability/Effect/학살의 흐름")]
public class SlaughterFlowEffect : AbilityEffect
{
    [SerializeField] private int maxStacks = 10;
    [Tooltip("마지막 처치 후 이 시간이 지나면 스택이 전부 사라진다")]
    [SerializeField] private float expire = 5f;
    [Tooltip("스택당 피해 증가")]
    [SerializeField] private float damagePerStack = 0.04f;
    [Tooltip("이 스택부터 공격속도 증가")]
    [SerializeField] private int speedStacks = 5;
    [SerializeField] private float attackSpeed = 0.15f;
    [Tooltip("최대 스택에서 치명타 확률 증가 %p")]
    [SerializeField] private float maxStackCrit = 20f;

    private class S { public int stacks; public float last; }

    private int Stacks(AbilityContext ctx)
    {
        var st = ctx.State<S>(this);
        if (st.stacks > 0 && Time.time - st.last > expire) st.stacks = 0;
        return st.stacks;
    }

    public override void OnEnemyKilled(AbilityContext ctx, Enemy enemy)
    {
        var st = ctx.State<S>(this);
        int before = Stacks(ctx);
        st.stacks = Mathf.Min(maxStacks, before + 1);
        st.last = Time.time;
        if (st.stacks != before && (st.stacks == speedStacks || st.stacks == maxStacks))
            TraitUtil.Text(ctx.playerTransform.position, "학살 " + st.stacks);
    }

    public override float DamageMultiplier(AbilityContext ctx, DamageQuery q) { return 1f + Stacks(ctx) * damagePerStack; }
    public override float AttackSpeedBonus(AbilityContext ctx) { return Stacks(ctx) >= speedStacks ? attackSpeed : 0f; }
    public override float CritRateBonus(AbilityContext ctx) { return Stacks(ctx) >= maxStacks ? maxStackCrit : 0f; }
}
