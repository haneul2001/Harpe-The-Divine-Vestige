using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 뼈 감옥. 플레이어 둘레에 뼈 기둥이 솟아 좁은 우리를 만든다.
//
// 가두는 것 자체로는 피해가 없다. 대신 갇힌 동안 다른 패턴이 날아오므로
// "먼저 빠져나가라"가 정답이 된다. 기둥 사이에 틈을 하나 남겨 두는 이유이기도 하다.
//
// 가두기만 하고 보스가 아무것도 안 하면 지루하고, 다 막아 놓고 장판을 깔면 불합리하다.
// 그래서 기둥은 부술 수 있고(체력), 시간이 지나면 저절로 무너진다.
public class BossPatternCage : BossPattern
{
    [Header("감옥")]
    [Tooltip("기둥 그림. 세로로 긴 뼈가 어울린다")]
    [SerializeField] private Sprite pillarSprite;

    [Min(3)] [SerializeField] private int pillarCount = 8;
    [Min(1f)] [SerializeField] private float radius = 2.6f;

    [Tooltip("우리에 남겨 둘 틈의 수. 0이면 완전히 가둔다 — 1 이상을 권한다")]
    [Min(0)] [SerializeField] private int gaps = 1;

    [Tooltip("유지 시간(초). 지나면 저절로 무너진다")]
    [Min(0.5f)] [SerializeField] private float duration = 4f;

    [Tooltip("솟아오르기까지의 예고 시간(초)")]
    [Min(0.1f)] [SerializeField] private float riseDelay = 0.6f;

    [Tooltip("기둥 한 개의 체력. 0이면 부술 수 없다")]
    [Min(0)] [SerializeField] private int pillarHp = 0;

    [Header("연출")]
    [SerializeField] private float shake = 0.2f;
    [SerializeField] private string vfxId = "EnemyHit";

    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration) { return null; }

    public override bool IsUsable(BossEnemy boss)
    {
        return pillarSprite != null && base.IsUsable(boss);
    }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        boss.CloseHitBox();

        Vector2 center = boss.transform.position;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) center = p.transform.position;

        // 어디가 막히는지 먼저 보여 준다
        DangerZone.Circle(center, radius + 0.4f, riseDelay);

        float t = riseDelay;
        while (t > 0f && !boss.isDead)
        {
            t -= Time.deltaTime;
            if (boss.rb != null) boss.rb.velocity = Vector2.zero;
            yield return null;
        }
        if (boss.isDead) yield break;

        // 틈을 어디에 둘지 — 매번 다른 곳이라 외워서 뚫을 수 없다
        int gapStart = Random.Range(0, pillarCount);
        var pillars = new List<GameObject>();

        for (int i = 0; i < pillarCount; i++)
        {
            bool isGap = false;
            for (int g = 0; g < gaps; g++)
                if (i == (gapStart + g) % pillarCount) { isGap = true; break; }
            if (isGap) continue;

            float angle = 360f / pillarCount * i;
            Vector2 at = center + new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;

            pillars.Add(CreatePillar(at));
        }

        if (shake > 0f) CameraShake.Shake(shake);
        if (!string.IsNullOrEmpty(vfxId)) PixelVfx.Play(vfxId, center);

        // 보스는 감옥을 세우고 바로 다음 행동으로 넘어간다 — 서서 구경하면 공짜 딜 타임이 된다
        boss.StartCoroutine(Collapse(pillars, duration));
    }

    private GameObject CreatePillar(Vector2 at)
    {
        var go = new GameObject("BonePillar");
        go.transform.position = at;
        go.layer = LayerMask.NameToLayer("Wall");   // 플레이어를 막는 레이어

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = pillarSprite;
        sr.sortingLayerName = "Object";
        sr.sortingOrder = 10;
        // 그림 픽셀을 게임 격자(32px = 1칸)에 맞춘다
        go.transform.localScale = Vector3.one * (pillarSprite.pixelsPerUnit / 32f);

        var col = go.AddComponent<BoxCollider2D>();
        Vector2 size = pillarSprite.bounds.size;
        col.size = new Vector2(Mathf.Max(0.3f, size.x * 0.7f), Mathf.Max(0.3f, size.y * 0.7f));

        if (pillarHp > 0)
        {
            var hp = go.AddComponent<BonePillar>();
            hp.hp = pillarHp;
        }

        // 솟아오르는 맛 — 바닥에서 올라온다
        go.AddComponent<PillarRise>();
        return go;
    }

    private IEnumerator Collapse(List<GameObject> pillars, float after)
    {
        float t = after;
        while (t > 0f) { t -= Time.deltaTime; yield return null; }

        for (int i = 0; i < pillars.Count; i++)
            if (pillars[i] != null) Object.Destroy(pillars[i]);
    }
}

// 솟아오르는 연출. 0.25초 동안 바닥에서 밀려 올라온다.
public class PillarRise : MonoBehaviour
{
    private float t;
    private Vector3 target;

    private void Start()
    {
        target = transform.position;
        transform.position = target + Vector3.down * 0.6f;
    }

    private void Update()
    {
        if (t >= 1f) return;

        t += Time.deltaTime / 0.25f;
        transform.position = Vector3.Lerp(target + Vector3.down * 0.6f, target, Mathf.Clamp01(t));
    }
}

// 부술 수 있는 기둥. 플레이어 공격은 Enemy를 찾으므로, 맞히려면 이쪽도 Enemy 레이어가 필요하다 —
// 지금은 벽으로만 쓰고, 부수기를 켜고 싶으면 레이어를 Enemy로 바꾸고 Enemy 컴포넌트를 붙이면 된다.
public class BonePillar : MonoBehaviour
{
    public int hp = 3;

    public void Damage(int amount)
    {
        hp -= amount;
        if (hp <= 0) Destroy(gameObject);
    }
}
