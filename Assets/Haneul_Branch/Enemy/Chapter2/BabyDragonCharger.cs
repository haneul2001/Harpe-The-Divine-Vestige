using System.Collections;
using UnityEngine;

// 갈색 새끼 용 — 가만히 떠 있다가 돌진할 길을 예고하고, 그 방향으로 날아서 들이받는다.
//
// 예고는 "지나갈 길 전체"를 긴 상자로 깐다. 길이는 벽까지 잰 거리로 잘라서,
// 예고 상자가 벽 너머까지 뻗어 거짓말을 하지 않게 한다. 실제 돌진도 벽에 닿으면 멈춘다.
public class BabyDragonCharger : BabyDragonBase
{
    [Header("돌진")]
    [SerializeField] private float chargeSpeed = 12f;
    [Tooltip("최대 돌진 거리(월드 단위)")]
    [SerializeField] private float chargeDistance = 6f;
    [Tooltip("예고 상자 폭 = 부딪히는 폭")]
    [SerializeField] private float chargeWidth = 1.2f;
    [Tooltip("벽·장애물 레이어. 비우면 Wall")]
    [SerializeField] private LayerMask wallLayer;

    public static bool Trace;
    private Vector2 chargeDir;
    private float chargeLen;
    private DangerZone zone;
    private ContactFilter2D wallFilter;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[8];
    private CollisionDetectionMode2D originalMode;

    protected override void OnStart()
    {
        if (wallLayer.value == 0) wallLayer = LayerMask.GetMask("Wall");
        wallFilter = new ContactFilter2D { useTriggers = false };
        wallFilter.SetLayerMask(wallLayer);
        if (rb != null) originalMode = rb.collisionDetectionMode;
    }

    protected override bool LockPositionDuringActivePhase { get { return false; } }

    protected override void OnAttackWarning(Vector2 dir, float warningDuration)
    {
        chargeDir = DirToPlayer();
        FaceDir(chargeDir);

        chargeLen = Mathf.Max(0.5f, Mathf.Min(DistanceToWall(chargeDir, chargeDistance), DistanceToFloorEdge(chargeDir, chargeDistance)));

        float angle = Mathf.Atan2(chargeDir.y, chargeDir.x) * Mathf.Rad2Deg;
        Vector2 center = (Vector2)transform.position + chargeDir * chargeLen * 0.5f;
        zone = DangerZone.Box(center, new Vector2(chargeLen, chargeWidth), angle, warningDuration + 0.05f);
        if (zone != null) zone.HoldUntilHit().FillIn(Mathf.Max(0.1f, warningDuration - 0.1f));
    }

    protected override IEnumerator AttackActivePhase(Vector2 _)
    {
        if (zone != null) { zone.CompleteNow(); zone = null; }

        PlayState(FlyingHash);
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        Vector2 start = transform.position;
        Vector2 last = start;
        int stuck = 0;
        bool hitPlayer = false;
        float maxTime = chargeLen / Mathf.Max(0.1f, chargeSpeed) + 0.2f;
        float t = 0f;
        while (!isDead && t < maxTime && Vector2.Distance(start, transform.position) < chargeLen)
        {
            t += Time.fixedDeltaTime;
            rb.velocity = chargeDir * chargeSpeed;

            if (!hitPlayer) hitPlayer = HurtPlayerInRadius(transform.position, chargeWidth * 0.5f + 0.15f);

            // 벽에 닿으면 그 자리에서 멈춘다 — 바로 앞이 막혔거나, 물리에 막혀 제자리에 서 있으면
            if (t > 0.03f && DistanceToWall(chargeDir, 0.1f) < 0.1f) break;
            Vector2 now = transform.position;
            stuck = (now - last).sqrMagnitude < 0.0004f ? stuck + 1 : 0;
            if (stuck >= 3) break;
            last = now;
            // 물리 갱신 단위로 돈다 — 화면 프레임마다 재면 물리가 움직이기 전 프레임을 "멈췄다"로 잘못 본다
            yield return new WaitForFixedUpdate();
        }

        if (Trace) Debug.Log($"[Charger] 끝 t={t:F2} 이동={Vector2.Distance(start, transform.position):F2}/{chargeLen:F2} stuck={stuck} cons={rb.constraints}");
        rb.velocity = Vector2.zero;
        if (!isDead) PlayState(IdleHash);
    }

    // 방 바닥 범위 가장자리까지의 거리.
    // 문 자리는 벽에 구멍이 뚫려 있고 잠긴 문의 막이는 구멍 맨 끝(방 바깥쪽)에만 있다 —
    // 벽만 보고 돌진하면 그 구멍 속 통로까지 파고들어 방 밖으로 나간 것처럼 보였다.
    // 그래서 바닥이 깔린 네모(위쪽은 문 밑단 선, 나머지는 벽 한 줄 안쪽)를 넘지 않게 자른다.
    [Tooltip("바닥 가장자리에서 이만큼 안쪽에서 멈춘다")]
    [SerializeField] private float floorEdgePad = 0.4f;

    private Room room;

    private float DistanceToFloorEdge(Vector2 dir, float max)
    {
        if (room == null) room = GetComponentInParent<Room>();
        if (room == null) return max;

        Vector2 c = room.transform.position;
        Vector2 half = room.Size * 0.5f;
        float top = c.y + half.y - 0.5f - 3.5f;   // 문 밑단을 못 찾으면 벽 네 줄
        foreach (Door d in room.Doors)
            if (d != null && d.dir == Dir.Up) { top = d.transform.position.y + d.wallBottom; break; }

        Rect r = Rect.MinMaxRect(c.x - half.x + 1f + floorEdgePad, c.y - half.y + 1f + floorEdgePad,
                                 c.x + half.x - 1f - floorEdgePad, top - floorEdgePad);
        Vector2 p = transform.position;
        float t = max;
        if (dir.x > 0.0001f) t = Mathf.Min(t, (r.xMax - p.x) / dir.x);
        if (dir.x < -0.0001f) t = Mathf.Min(t, (r.xMin - p.x) / dir.x);
        if (dir.y > 0.0001f) t = Mathf.Min(t, (r.yMax - p.y) / dir.y);
        if (dir.y < -0.0001f) t = Mathf.Min(t, (r.yMin - p.y) / dir.y);
        return Mathf.Max(0f, t);
    }

    // 발밑에서 작은 원을 밀어 벽까지 거리를 잰다.
    // 몸 콜라이더를 통째로 밀면, 벽에 바짝 붙어 이미 겹친 발밑 판정 때문에 늘 "거리 0"이 나와
    // 어느 방향으로도 돌진하지 못했다. 이미 겹쳐 있는 것(거리 0)은 무시한다
    private float DistanceToWall(Vector2 dir, float max)
    {
        int n = Physics2D.CircleCast(transform.position, 0.22f, dir, wallFilter, hits, max);
        float d = max;
        for (int i = 0; i < n; i++)
            if (hits[i].distance > 0.001f) d = Mathf.Min(d, hits[i].distance);
        return d;
    }

    protected override void OnAttackFinally()
    {
        if (zone != null) { zone.Cancel(); zone = null; }
        if (rb != null) rb.collisionDetectionMode = originalMode;
    }
}
