using System.Collections;
using UnityEngine;

// 파란 새끼 용 — 멀리서 플레이어 발밑에 벼락을 내린다.
//
// 겨누는 순간의 플레이어 자리에 원형 예고가 깔리고, 원이 다 차는 순간 벼락이 떨어진다.
// 자리는 예고를 띄울 때 정해 둔다 — 뒤늦게 다시 겨누면 "피했는데 따라와서 맞는다"가 된다.
public class BabyDragonStriker : BabyDragonBase
{
    [Header("벼락")]
    [SerializeField] private float strikeRadius = 1.2f;
    [SerializeField] private string boltVfxId = "DragonLightning";
    [SerializeField] private float boltScale = 0.8f;
    [SerializeField] private string crackVfxId = "DragonCrack";

    private Vector2 target;
    private DangerZone zone;

    protected override void OnAttackWarning(Vector2 dir, float warningDuration)
    {
        target = PlayerPos;
        FaceDir(target - (Vector2)transform.position);

        // 경고 시간 + 공격 동작이 벼락을 부르기까지 걸리는 시간 동안 차오른다
        zone = DangerZone.Circle(target, strikeRadius, warningDuration + hitEventTimeout);
        if (zone != null) zone.HoldUntilHit().FillIn(Mathf.Max(0.15f, warningDuration));
    }

    protected override IEnumerator AttackActivePhase(Vector2 _)
    {
        if (rb != null) rb.velocity = Vector2.zero;
        if (zone != null) { zone.CompleteNow(); zone = null; }

        PixelVfx bolt = PixelVfx.Play(boltVfxId, target);
        if (bolt != null) bolt.transform.localScale *= boltScale;
        PixelVfx crack = PixelVfx.Play(crackVfxId, target);
        if (crack != null) crack.transform.localScale *= strikeRadius / 2.6f;
        CameraShake.Shake(0.12f);

        HurtPlayerInRadius(target, strikeRadius);
        yield break;
    }

    protected override void OnAttackFinally()
    {
        if (zone != null) { zone.Cancel(); zone = null; }
    }
}
