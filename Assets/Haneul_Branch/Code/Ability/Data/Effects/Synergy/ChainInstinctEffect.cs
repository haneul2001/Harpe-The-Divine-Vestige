using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [처치 세트] 연쇄 본능 — 처치가 짧은 간격으로 이어지는 동안 공격속도 증가
[CreateAssetMenu(fileName = "Synergy_ChainInstinct", menuName = "Harpe/Ability/Synergy/연쇄 본능")]
public class ChainInstinctEffect : AbilityEffect
{
    [SerializeField] private float attackSpeed = 0.15f;
    [Tooltip("처치 사이가 이 시간 안이면 연속으로 친다")]
    [SerializeField] private float gap = 3f;

    private class S { public int streak; public float last = -99f; }

    public override void OnEnemyKilled(AbilityContext ctx, Enemy enemy)
    {
        var st = ctx.State<S>(this);
        st.streak = Time.time - st.last <= gap ? st.streak + 1 : 1;
        st.last = Time.time;
    }

    public override float AttackSpeedBonus(AbilityContext ctx)
    {
        var st = ctx.State<S>(this);
        return st.streak >= 2 && Time.time - st.last <= gap ? attackSpeed : 0f;
    }
}
