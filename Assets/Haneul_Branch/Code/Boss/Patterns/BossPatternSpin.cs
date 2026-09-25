using System.Collections;
using UnityEngine;

// 회전 베기. 보스 둘레를 한 번에 쓸어 낸다.
//
// "붙어서 계속 때리기"를 처벌하는 패턴이라, 정답은 한 박자 떨어지는 것이다.
// 히트박스를 빙빙 돌리지 않는다 — 돌리면 같은 원 안에서도 칼이 어디쯤 있느냐에 따라
// 맞고 안 맞고가 갈려서, 예고한 원과 실제로 맞는 자리가 계속 어긋난다.
// 원이 다 차는 순간 그 안을 통째로 한 번 친다.
public class BossPatternSpin : BossPattern
{
    [Header("휘두르기")]
    [Tooltip("쓸어 낸 뒤 제자리에서 버티는 시간(초). 이 틈이 반격 구간이 된다")]
    [Min(0f)] [SerializeField] private float recoverTime = 0.5f;

    [Header("연출")]
    [SerializeField] private float shake = 0.2f;

    [Tooltip("휘두르는 순간 예고 원을 꽉 채우며 한 번 나가는 이펙트 id.\n"
           + "작은 베기를 여러 번 뿌리면 원이 얼마나 위험한지가 안 읽힌다 — 한 방으로 원을 덮는다")]
    [SerializeField] private string sweepVfxId = "BossSpinSlash";

    [Tooltip("이펙트 크기 배율. 1이면 칼이 닿는 거리와 같다")]
    [Min(0.1f)] [SerializeField] private float sweepScale = 1f;

    [Tooltip("예고 원을 이펙트보다 이만큼 작게 그린다. 0.9면 10% 작게.\n"
           + "둥근 이펙트는 가장자리가 흐려서, 같은 크기로 그리면 예고가 더 커 보인다")]
    [Range(0.5f, 1f)] [SerializeField] private float dangerScale = 0.9f;

    // 둘레 전체가 위험하다 — 상자가 아니라 원이다.
    // 예고(그리고 판정)는 이펙트보다 한 뼘 작게 잡는다.
    public override DangerShape[] DangerShapes(BossEnemy boss)
    {
        // 히트박스가 닿는 거리라 보스 몸 크기를 따라간다
        return new DangerShape[] { DangerShape.Circle(DangerRadius(boss) * dangerScale, DangerOrigin.Boss).ScaledWithBoss() };
    }

    // 원을 덮는 한 방을 직접 띄우므로 보스의 기본 베기는 필요 없다
    public override bool DrawsOwnSlash { get { return !string.IsNullOrEmpty(sweepVfxId); } }

    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration)
    {
        DangerShape s = DangerShapes(boss)[0];
        return DangerZone.Circle(boss.transform.position, s.Radius, duration, boss.transform);
    }

    private float DangerRadius(BossEnemy boss)
    {
        GameObject hb = boss.HitBoxObject;
        if (hb == null) return 2f;

        float reach = Vector2.Distance(hb.transform.position, boss.transform.position);

        // 콜라이더에서 재면 안 된다 — 런타임에는 상자가 지워지고 부채꼴로 바뀌어 있어
        // 그때부터 반지름이 반 토막 난다. 보이는 크기를 쓴다.
        float length = boss.HitBoxDisplaySize.x;
        if (length <= 0.001f)
        {
            var box = hb.GetComponent<BoxCollider2D>();
            length = box != null ? box.size.x * Mathf.Abs(hb.transform.lossyScale.x) : 1f;
        }

        return reach + length * 0.5f;
    }

    // 근접 상자를 쓰지 않는다 — 원 안쪽 전체가 판정이라 직접 굴린다
    public override bool UsesHitBox { get { return false; } }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        boss.CloseHitBox();

        float radius = DangerShapes(boss)[0].Radius;

        if (shake > 0f) CameraShake.Shake(shake);

        // 이펙트는 줄이기 전 크기로 — 예고보다 조금 크게 돈다
        if (!string.IsNullOrEmpty(sweepVfxId))
        {
            float d = DangerRadius(boss) * 2f * sweepScale;
            PixelVfx.PlayStretched(sweepVfxId, boss.transform.position, 0f, new Vector2(d, d))
                    ?.Follow(boss.transform);
        }

        // 원 안이면 맞는다. 실제 판정은 보여 준 원보다 조금 작다(판정 여유).
        boss.DamagePlayerInRadius(boss.transform.position, radius);

        // 한 번 치고 나서 잠깐 선다 — 곧바로 다음 패턴으로 넘어가면 반격할 틈이 없다
        float t = recoverTime;
        while (t > 0f && !boss.isDead)
        {
            t -= Time.deltaTime;
            if (boss.rb != null) boss.rb.velocity = Vector2.zero;
            yield return null;
        }
    }
}
