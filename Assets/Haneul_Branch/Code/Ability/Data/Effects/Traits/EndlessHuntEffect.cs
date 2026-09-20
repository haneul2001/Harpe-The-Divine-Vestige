using System.Collections.Generic;
using UnityEngine;

// [처치] 끝없는 사냥 — 짧은 시간에 연속 처치하면 대시 쿨타임 초기화
[CreateAssetMenu(fileName = "Effect_EndlessHunt", menuName = "Harpe/Ability/Effect/끝없는 사냥")]
public class EndlessHuntEffect : AbilityEffect
{
    [SerializeField] private int kills = 2;
    [SerializeField] private float window = 2.5f;

    private class S { public List<float> times = new List<float>(); }

    public override void OnEnemyKilled(AbilityContext ctx, Enemy enemy)
    {
        var st = ctx.State<S>(this);
        st.times.Add(Time.time);
        TraitUtil.Trim(st.times, window);
        if (st.times.Count < kills) return;

        st.times.Clear();
        var move = ctx.player != null ? ctx.player.GetComponent<PlayerMove>() : null;
        if (move != null && move.DashCooldownRemaining > 0f)
        {
            move.ResetDashCooldown();
            TraitUtil.Text(ctx.playerTransform.position, "대시 초기화");
        }
    }
}
