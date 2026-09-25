using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 무덤 균열. 바닥 여러 곳에 금이 가고, 잠시 뒤 솟구친다.
//
// 서 있을 자리를 뺏는 유일한 패턴이다 — 거리를 벌리는 것만으로는 안 풀리고,
// "어디에 설 것인가"를 매번 다시 고르게 만든다.
// 히트박스를 쓰지 않으므로 피해는 이 패턴이 직접 굴린다.
public class BossPatternCrack : BossPattern
{
    [Header("균열")]
    [Min(1)] [SerializeField] private int crackCount = 4;
    [Min(0.5f)] [SerializeField] private float crackRadius = 1.4f;

    [Tooltip("첫 균열이 플레이어 발밑에 생긴다. 나머지는 그 주위로 흩어진다")]
    [SerializeField] private bool firstOnPlayer = true;

    [Tooltip("흩어지는 범위(월드 단위)")]
    [Min(1f)] [SerializeField] private float spread = 4.5f;

    [Tooltip("금이 간 뒤 솟구치기까지의 시간(초). 이 동안 빨간 표시가 차오른다")]
    [Min(0.1f)] [SerializeField] private float riseDelay = 0.9f;

    [Tooltip("균열 사이의 시간차(초). 0이면 동시에 터진다")]
    [Min(0f)] [SerializeField] private float stagger = 0.12f;

    [Header("연출")]
    [SerializeField] private float shake = 0.25f;
    [SerializeField] private string vfxId = "EnemyHit";

    // 플레이어 발밑을 중심으로 흩어지는 원 여러 개
    public override DangerShape[] DangerShapes(BossEnemy boss)
    {
        return new DangerShape[] {
            DangerShape.Circle(crackRadius, DangerOrigin.Player).Scattered(crackCount, spread) };
    }

    // 예고는 균열 자체가 낸다 (터지기 직전까지 차오르는 표시)
    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration) { return null; }

    // 근접 판정을 안 쓴다 — 바닥 균열이 직접 피해를 굴린다
    public override bool UsesHitBox { get { return false; } }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        // 히트박스는 안 쓴다 — 켜 두면 옆에 선 플레이어가 균열과 무관하게 맞는다
        boss.CloseHitBox();

        var spots = PickSpots(boss);
        for (int i = 0; i < spots.Count; i++)
            boss.StartCoroutine(RunOne(boss, spots[i], i * stagger));

        // 마지막 균열이 터질 때까지 보스는 제자리에서 버틴다
        float wait = riseDelay + stagger * Mathf.Max(0, spots.Count - 1) + 0.15f;
        while (wait > 0f && !boss.isDead)
        {
            wait -= Time.deltaTime;
            if (boss.rb != null) boss.rb.velocity = Vector2.zero;
            yield return null;
        }
    }

    private List<Vector2> PickSpots(BossEnemy boss)
    {
        var spots = new List<Vector2>();

        Vector2 center = boss.transform.position;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) center = p.transform.position;

        if (firstOnPlayer) spots.Add(center);

        while (spots.Count < crackCount)
        {
            Vector2 offset = Random.insideUnitCircle * spread;
            Vector2 at = center + offset;

            // 너무 겹치면 같은 자리를 두 번 치는 셈이라 다시 뽑는다
            bool tooClose = false;
            for (int i = 0; i < spots.Count; i++)
                if (Vector2.Distance(spots[i], at) < crackRadius) { tooClose = true; break; }

            if (!tooClose) spots.Add(at);
            else if (spots.Count > 0 && Random.value < 0.3f) spots.Add(at);   // 무한 루프 방지
        }
        return spots;
    }

    private IEnumerator RunOne(BossEnemy boss, Vector2 at, float delay)
    {
        if (delay > 0f) yield return Wait(delay);
        if (boss.isDead) yield break;

        float radius = DangerShapes(boss)[0].Radius;

        DangerZone.Circle(at, radius, riseDelay);
        yield return Wait(riseDelay);
        if (boss.isDead) yield break;

        if (!string.IsNullOrEmpty(vfxId)) PixelVfx.Play(vfxId, at);
        if (shake > 0f) CameraShake.Shake(shake);

        boss.DamagePlayerInRadius(at, radius);
    }

    private static IEnumerator Wait(float seconds)
    {
        float t = seconds;
        while (t > 0f) { t -= Time.deltaTime; yield return null; }
    }
}
