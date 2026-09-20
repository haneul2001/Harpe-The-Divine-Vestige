using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [소울 세트] 과충전 — 보유 소울이 일정 이상인 동안 주기적으로 영혼탄 자동 발사 (소울 소모 없음)
[CreateAssetMenu(fileName = "Synergy_Overcharge", menuName = "Harpe/Ability/Synergy/과충전")]
public class OverchargeEffect : AbilityEffect
{
    [SerializeField] private int soulThreshold = 60;
    [SerializeField] private float interval = 2f;
    [SerializeField] private float coefficient = 0.6f;
    [SerializeField] private float searchRange = 9f;
    [SerializeField] private float speed = 9f;
    [SerializeField] private GameObject visual;
    [SerializeField] private float visualScale = 0.7f;

    private class S { public float next; }

    public override void OnTick(AbilityContext ctx, float dt)
    {
        if (ctx.status == null || ctx.status.CurrentSoul < soulThreshold) return;
        var st = ctx.State<S>(this);
        if (Time.time < st.next) return;

        Vector3 from = ctx.BodyCenter;
        Enemy target = AbilityHooks.NearestEnemy(from, searchRange);
        if (target == null) return;   // 쏠 대상이 없으면 기다렸다가 생기는 순간 쏜다

        st.next = Time.time + interval;
        PlayerProjectile.Fire(from, Vector2.up, new PlayerProjectile.Settings
        {
            coefficient = coefficient, speed = speed, range = searchRange * 1.6f, radius = 0.45f,
            homing = 540f, target = target, visual = visual, visualScale = visualScale, useFx = true, fx = FxSprite.SoulBolt,
        }, ctx);
    }
}
