using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [원거리 세트] 환영 투사체 — 투사체 적중 시 확률로 다른 적에게 복제탄 (복제탄은 다시 복제 안 됨)
[CreateAssetMenu(fileName = "Synergy_PhantomProjectile", menuName = "Harpe/Ability/Synergy/환영 투사체")]
public class PhantomProjectileEffect : AbilityEffect
{
    [Range(0f, 1f)] [SerializeField] private float chance = 0.3f;
    [SerializeField] private float coefficient = 0.5f;
    [SerializeField] private float searchRange = 6f;
    [SerializeField] private float speed = 12f;
    [SerializeField] private GameObject visual;
    [SerializeField] private float visualScale = 0.6f;

    public override void OnEnemyHit(AbilityContext ctx, HitInfo hit)
    {
        if (hit.kind != DamageKind.Projectile || hit.echo || Random.value > chance) return;
        Enemy target = AbilityHooks.NearestEnemy(hit.point, searchRange, hit.enemy);
        if (target == null) return;

        Vector2 dir = (Vector2)(PlayerHitSparkVfx.BodyBounds(target).center - hit.point);
        PlayerProjectile.Fire(hit.point, dir, new PlayerProjectile.Settings
        {
            coefficient = coefficient, speed = speed, range = searchRange * 1.3f, radius = 0.4f,
            homing = 360f, target = target, ignore = hit.enemy, visual = visual, visualScale = visualScale, echo = true, useFx = true, fx = FxSprite.Phantom,
        }, ctx);
    }
}
