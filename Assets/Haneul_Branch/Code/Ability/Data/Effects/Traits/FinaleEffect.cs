using System.Collections.Generic;
using UnityEngine;

// [특수] 피날레 — N연속 처치/처형을 이으면 주변 모든 적에게 대피해
[CreateAssetMenu(fileName = "Effect_Finale", menuName = "Harpe/Ability/Effect/피날레")]
public class FinaleEffect : AbilityEffect
{
    [SerializeField] private int streakNeeded = 10;
    [Tooltip("처치 사이가 이 시간보다 벌어지면 연속이 끊긴다")]
    [SerializeField] private float gap = 3f;
    [SerializeField] private float coefficient = 3f;
    [Tooltip("이 거리 안의 모든 적 (방 하나가 18x10이라 20이면 방 전체)")]
    [SerializeField] private float radius = 20f;
    [SerializeField] private GameObject visual;
    [SerializeField] private float visualScale = 2.5f;

    private class S { public int streak; public float last; }

    public override void OnEnemyKilled(AbilityContext ctx, Enemy enemy)
    {
        var st = ctx.State<S>(this);
        st.streak = Time.time - st.last <= gap ? st.streak + 1 : 1;
        st.last = Time.time;
        if (st.streak < streakNeeded) return;

        st.streak = 0;
        Vector3 at = ctx.BodyCenter;
        TraitUtil.Text(at, "피날레");
        CameraShake.Shake(0.5f);
        foreach (Enemy e in AbilityHooks.EnemiesInRadius(at, radius))
        {
            bool crit;
            int dmg = AbilityHooks.RollDamage(ctx.status, coefficient, DamageKind.Area, e, out crit);
            AbilityHooks.Deal(e, dmg, crit, DamageKind.Area, e.transform.position + Vector3.up, ctx.playerTransform);
            SpriteFx.Burst(FxSprite.SkullBurst, PlayerHitSparkVfx.BodyBounds(e).center, 1.2f, 0.5f);
        }
    }
}
