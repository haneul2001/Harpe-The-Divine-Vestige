using System.Collections.Generic;
using UnityEngine;

// [원거리] 죽음의 파편 — 근접 공격이 적중하면 맞은 적에게서 파편이 사방으로 튄다
[CreateAssetMenu(fileName = "Effect_DeathShards", menuName = "Harpe/Ability/Effect/죽음의 파편")]
public class DeathShardsEffect : AbilityEffect
{
    [SerializeField] private int count = 3;
    [SerializeField] private float coefficient = 0.3f;
    [SerializeField] private float speed = 11f;
    [SerializeField] private float range = 3.5f;
    [Tooltip("한 번 튄 뒤 다시 튀기까지(초). 한 번 휘둘러 여러 마리를 맞혀도 파편은 한 번만")]
    [SerializeField] private float cooldown = 0.25f;
    [SerializeField] private GameObject visual;
    [SerializeField] private float visualScale = 0.5f;

    private class S { public float next; }

    public override void OnEnemyHit(AbilityContext ctx, HitInfo hit)
    {
        if (!hit.kind.IsMelee()) return;
        var st = ctx.State<S>(this);
        if (Time.time < st.next) return;
        st.next = Time.time + cooldown;

        float start = Random.Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            float a = (start + 360f * i / Mathf.Max(1, count)) * Mathf.Deg2Rad;
            PlayerProjectile.Fire(hit.point, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), new PlayerProjectile.Settings
            {
                coefficient = coefficient, speed = speed, range = range, radius = 0.35f,
                ignore = hit.enemy, visual = visual, visualScale = visualScale, useFx = true, fx = FxSprite.Shard,
            }, ctx);
        }
    }
}
