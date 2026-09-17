using UnityEngine;

// 몬스터 피격 이펙트 (Vefects Pixel Craft — 2D Critical Attack 01).
//
// 가시가 "플레이어 → 몬스터" 방향, 즉 몬스터 너머로 뻗게 8방향으로 돌려 띄운다.
//
// 팩의 8종은 슬래시와 마찬가지로 플립북 한 장에 파티클 시작 회전(0/90/180/270)과 좌우 반전만 다르다.
// Mask_180의 가시가 ↘를 향한다는 걸 기준으로 역산하면 (Unity 파티클 회전은 시계 방향이 +):
//   · Mask 계열      : 가시 방향 = 135° - 회전  → Mask ↖, _90 ↗, _180 ↘, _270 ↙
//   · Mask_Flip 계열 : 좌우 반전이라 45° - 회전  → Flip ↗, _90 ↘, _180 ↙, _270 ↖
// 대각선은 Mask 계열이 딱 맞고, 상하좌우는 45도가 남으므로 시작 회전을 직접 덮어 맞춘다.
// 매번 같은 그림이면 단조로워서 두 계열을 번갈아 쓴다 (좌우 반전된 모양이 섞인다).
public class PlayerHitSparkVfx : MonoBehaviour
{
    [Tooltip("순서 고정: [0]Mask [1]Mask_90 [2]Mask_180 [3]Mask_270")]
    [SerializeField] private GameObject[] normal = new GameObject[4];
    [Tooltip("순서 고정: [0]Mask_Flip [1]Mask_Flip_90 [2]Mask_Flip_180 [3]Mask_Flip_270")]
    [SerializeField] private GameObject[] flipped = new GameObject[4];

    [Tooltip("크기 배율. 파티클 기본 크기가 1유닛")]
    [SerializeField] private float scale = 1.56f;
    [Tooltip("몬스터 몸통(피격 콜라이더) 외곽에서 가시 방향으로 얼마나 더 밀어낼지. 0이면 외곽선 위, 1이면 외곽선 한 번 더 바깥")]
    [SerializeField] private float edgePush = 0f;
    [Tooltip("몬스터 외곽선에서 가시 방향으로 더 띄우는 거리 (월드 단위, 몬스터 크기와 무관)")]
    [SerializeField] private float edgeGap = 0.7f;
    [SerializeField] private float lifeTime = 0.45f;
    [Tooltip("번갈아 쓸지. 끄면 Mask 계열만 쓴다")]
    [SerializeField] private bool alternateFlip = true;
    [Tooltip("몬스터 그림보다 이만큼 앞에 그린다 — 이펙트가 몬스터를 덮어야 한다")]
    [SerializeField] private int sortingOrderOffset = 20;
    [Tooltip("색을 바꾼 재질. 원본 Mask 셰이더는 텍스처 R/G/B 채널을 _R/_G/_B 색으로 칠하므로 그 세 색만 바꾼 복제본을 쓴다. 비우면 원래 색(청록·보라)")]
    [SerializeField] private Material colorMaterial;

    private int count;

    // 몬스터만 알 때 (패링 반격·처형처럼 판정 콜라이더를 직접 들고 있지 않은 경우).
    // 몸통 콜라이더 범위를 찾아 위 Play로 넘긴다.
    public void Play(Enemy enemy, Vector3 from)
    {
        if (enemy == null) return;
        Play(BodyBounds(enemy), from, enemy);
    }

    // 몬스터 몸통 범위 — Enemy 레이어의 트리거 아닌 콜라이더가 피격 판정 몸통이다 (발밑·BackPosition 제외)
    public static Bounds BodyBounds(Enemy enemy)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        Collider2D fallback = null;
        foreach (var c in enemy.GetComponentsInChildren<Collider2D>())
        {
            if (!c.enabled) continue;
            if (c.gameObject.layer == enemyLayer && !c.isTrigger) return c.bounds;
            if (fallback == null && c.gameObject.layer == enemyLayer) fallback = c;
        }
        return fallback != null ? fallback.bounds : new Bounds(enemy.transform.position + Vector3.up * 0.6f, new Vector3(0.7f, 1.2f, 0f));
    }

    // 플레이어에 붙은 컴포넌트를 찾는다 (처형 매니저처럼 플레이어를 Transform으로만 받는 곳용)
    public static PlayerHitSparkVfx On(Component player)
    {
        return player != null ? player.GetComponent<PlayerHitSparkVfx>() : null;
    }

    // body: 맞은 콜라이더(몸통 범위) / from: 때린 쪽(플레이어) 위치 / enemy: 맞은 몬스터
    public void Play(Bounds body, Vector3 from, Component enemy)
    {
        SpriteRenderer enemySprite = FrontSprite(enemy);

        Vector2 d = body.center - from;
        if (d.sqrMagnitude < 0.0001f) d = Vector2.right;

        // 8방향으로 반올림한 가시 방향 (→ 0°, 반시계 +)
        float angle = Mathf.Round(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg / 45f) * 45f;
        float rad = angle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        // 몸통 타원의 가시 방향 외곽점 — 옆에서 맞으면 옆구리, 위에서 맞으면 머리 쪽에 터진다
        Vector3 ext = body.extents * (1f + edgePush);
        Vector3 pos = body.center + new Vector3(dir.x * ext.x, dir.y * ext.y, 0f)
                    + (Vector3)(dir * edgeGap);   // 외곽선에서 가시 방향으로 일정 거리 더 띄운다

        int layerId = enemySprite != null ? enemySprite.sortingLayerID : 0;
        int order = (enemySprite != null ? enemySprite.sortingOrder : 0) + sortingOrderOffset;

        bool flip = alternateFlip && (count++ % 2 == 1);
        float rotation = Mathf.Repeat((flip ? 45f : 135f) - angle, 360f);
        int quadrant = Mathf.RoundToInt(rotation / 90f) % 4;

        GameObject prefab = (flip ? flipped : normal)[quadrant];
        if (prefab == null) return;

        GameObject go = Instantiate(prefab, pos, Quaternion.identity);
        go.transform.localScale = Vector3.one * scale;

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            if (ps.emission.burstCount > 0)
                main.startRotation = rotation * Mathf.Deg2Rad;   // 상하좌우는 여기서 45도가 더해진다
        }

        foreach (var r in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            r.sortingLayerID = layerId;
            r.sortingOrder = order;
            if (colorMaterial != null && r.renderMode != ParticleSystemRenderMode.None)
                r.sharedMaterial = colorMaterial;
        }

        go.GetComponentInChildren<ParticleSystem>().Play(true);
        Destroy(go, lifeTime);
    }

    // 몬스터 자식 그림 중 가장 앞에 그려지는 것 (그림자·발밑 표시보다 몸통이 앞이다).
    // 같은 정렬 레이어 안에서 그보다 앞에 두면 이펙트가 몬스터를 덮는다.
    private static SpriteRenderer FrontSprite(Component enemy)
    {
        if (enemy == null) return null;

        SpriteRenderer best = null;
        foreach (var sr in enemy.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!sr.enabled || sr.sprite == null || DebugVisual.Owns(sr)) continue;
            if (best == null
                || SortingLayer.GetLayerValueFromID(sr.sortingLayerID) > SortingLayer.GetLayerValueFromID(best.sortingLayerID)
                || (sr.sortingLayerID == best.sortingLayerID && sr.sortingOrder > best.sortingOrder))
                best = sr;
        }
        return best;
    }
}
