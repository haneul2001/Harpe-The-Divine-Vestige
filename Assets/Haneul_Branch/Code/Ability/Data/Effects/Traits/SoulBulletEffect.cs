using System.Collections.Generic;
using UnityEngine;

// [소울] 영혼의 탄환 — 소울을 얻을 때마다 가까운 적에게 유도 영혼탄
[CreateAssetMenu(fileName = "Effect_SoulBullet", menuName = "Harpe/Ability/Effect/영혼의 탄환")]
public class SoulBulletEffect : AbilityEffect
{
    [SerializeField] private float coefficient = 0.6f;
    [Tooltip("소울 이만큼마다 탄환 1발 추가 (최소 1발)")]
    [SerializeField] private int soulPerExtraBullet = 10;
    [SerializeField] private int maxBullets = 3;
    [SerializeField] private float searchRange = 10f;
    [SerializeField] private float speed = 9f;
    [SerializeField] private GameObject visual;
    [SerializeField] private float visualScale = 0.7f;

    public override void OnSoulGained(AbilityContext ctx, int amount)
    {
        if (ctx.playerTransform == null) return;
        Vector3 from = ctx.BodyCenter;
        Enemy target = AbilityHooks.NearestEnemy(from, searchRange);
        if (target == null) return;

        int n = Mathf.Clamp(1 + amount / Mathf.Max(1, soulPerExtraBullet), 1, maxBullets);
        for (int i = 0; i < n; i++)
        {
            // 위쪽으로 부채꼴로 뿌린 뒤 대상을 향해 휘어 들어간다
            float a = (90f + (i - (n - 1) * 0.5f) * 35f) * Mathf.Deg2Rad;
            PlayerProjectile.Fire(from, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), new PlayerProjectile.Settings
            {
                coefficient = coefficient, speed = speed, range = searchRange * 1.6f, radius = 0.45f,
                homing = 540f, target = target, visual = visual, visualScale = visualScale, useFx = true, fx = FxSprite.SoulBolt,
            }, ctx);
        }
    }
}
