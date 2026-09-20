using System.Collections.Generic;
using UnityEngine;

// 특성이 쏘는 투사체 (사신의 검기, 죽음의 파편, 영혼의 탄환, 반격의 칼날 …).
//
// 물리 엔진을 쓰지 않고 매 프레임 직접 밀면서 원 판정으로 적을 찾는다 — 벽 레이어에 닿으면 사라진다.
// 관통이 켜져 있으면(효과 자체 설정 또는 '꿰뚫는 일격') 적마다 한 번씩 맞히고 계속 날아간다.
public class PlayerProjectile : MonoBehaviour
{
    public struct Settings
    {
        public float coefficient;   // 공격력 대비 피해 배율
        public float speed;
        public float range;
        public float radius;        // 적중 판정 반경
        public bool pierce;
        public float homing;        // 0이면 직진. 초당 회전 각도(도)
        public Enemy target;        // 유도 대상
        public Enemy ignore;        // 처음부터 안 맞힐 적 (파편이 튀어나온 적 등)
        public GameObject visual;   // Vefects 투사체 프리팹 (가로로 날아가는 그림)
        public float visualScale;
        public Material visualMaterial;
        public bool echo;           // 환영 투사체가 만든 복제탄
        public bool useFx;          // 켜면 visual(파티클) 대신 한 장짜리 그림 + 코드 움직임
        public FxSprite fx;
        public float fxScale;       // 그림 배율 (0이면 1)
    }

    private Settings s;
    private Vector2 dir;
    private float travelled;
    private PlayerStatus status;
    private Transform player;
    private readonly HashSet<Enemy> struck = new HashSet<Enemy>();
    private GameObject visualGo;
    private FxProjectileVisual fxVisual;
    private int wallMask;

    private static readonly Collider2D[] buffer = new Collider2D[16];

    public static PlayerProjectile Fire(Vector3 position, Vector2 direction, Settings settings, AbilityContext ctx)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;

        var go = new GameObject("PlayerProjectile");
        go.transform.position = position;
        var p = go.AddComponent<PlayerProjectile>();
        // 세트 시너지(탄막 확장): 크기·사거리
        float size = AbilityHooks.ProjectileSizeMult();
        settings.radius *= size;
        settings.fxScale = (settings.fxScale > 0f ? settings.fxScale : 1f) * size;
        settings.visualScale = (settings.visualScale > 0f ? settings.visualScale : 1f) * size;
        settings.range *= AbilityHooks.ProjectileRangeMult();
        p.s = settings;
        p.dir = direction.normalized;
        p.status = ctx.status;
        p.player = ctx.playerTransform;
        if (settings.ignore != null) p.struck.Add(settings.ignore);
        p.wallMask = LayerMask.GetMask("Wall");
        p.SpawnVisual();
        return p;
    }

    private void SpawnVisual()
    {
        if (s.useFx)
        {
            fxVisual = SpriteFx.AttachProjectile(transform, s.fx, s.fxScale, Angle());
            if (fxVisual != null) return;   // 그림이 없으면 아래 파티클로 대체
        }
        if (s.visual == null) return;
        visualGo = VfxPrefab.Spawn(s.visual, transform.position, Angle(), s.visualScale > 0f ? s.visualScale : 1f,
            transform, s.visualMaterial, 0f);
    }

    private float Angle()
    {
        return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // 유도: 대상 쪽으로 조금씩 꺾는다
        if (s.homing > 0f && s.target != null && !s.target.isDead)
        {
            Vector2 to = (Vector2)PlayerHitSparkVfx.BodyBounds(s.target).center - (Vector2)transform.position;
            if (to.sqrMagnitude > 0.0001f)
            {
                float cur = Angle();
                float want = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
                float next = Mathf.MoveTowardsAngle(cur, want, s.homing * dt);
                dir = new Vector2(Mathf.Cos(next * Mathf.Deg2Rad), Mathf.Sin(next * Mathf.Deg2Rad));
                if (visualGo != null) VfxPrefab.SetRotation(visualGo, next);
                if (fxVisual != null) fxVisual.SetAngle(next);
            }
        }

        float step = s.speed * dt;
        Vector2 pos = transform.position;

        // 벽에 닿으면 끝
        if (Physics2D.Raycast(pos, dir, step, wallMask).collider != null)
        {
            Finish();
            return;
        }

        pos += dir * step;
        transform.position = pos;
        travelled += step;

        HitAround(pos);

        if (travelled >= s.range) Finish();
    }

    private void HitAround(Vector2 pos)
    {
        bool pierce = s.pierce || AbilityHooks.ProjectilesPierce();
        int n = Physics2D.OverlapCircleNonAlloc(pos, s.radius, buffer, LayerMask.GetMask("Enemy"));
        for (int i = 0; i < n; i++)
        {
            Enemy e = buffer[i] != null ? buffer[i].GetComponentInParent<Enemy>() : null;
            if (e == null || e.isDead || !struck.Add(e)) continue;

            bool crit;
            int dmg = AbilityHooks.RollDamage(status, s.coefficient, DamageKind.Projectile, e, out crit);
            AbilityHooks.Deal(e, dmg, crit, DamageKind.Projectile, pos - dir * 0.5f, player, s.echo);

            if (!pierce) { Finish(); return; }
        }
    }

    private void Finish()
    {
        if (fxVisual != null) fxVisual.Release();
        // 그림은 바로 지우지 않고 파티클이 흩어질 시간만 둔다
        if (visualGo != null)
        {
            visualGo.transform.SetParent(null, true);
            foreach (var ps in visualGo.GetComponentsInChildren<ParticleSystem>())
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(visualGo, 0.4f);
        }
        Destroy(gameObject);
    }
}

// Vefects 파티클 프리팹을 원하는 방향·크기·색으로 띄우는 도우미.
// 이 팩의 2D 파티클은 트랜스폼 회전이 아니라 파티클 시작 회전(시계 방향 +)으로 방향을 잡는다.
public static class VfxPrefab
{
    public static GameObject Spawn(GameObject prefab, Vector3 position, float angleDeg, float scale,
        Transform parent, Material material, float lifetime, string sortingLayer = "Skill", int sortingOrder = 40)
    {
        if (prefab == null) return null;

        GameObject go = Object.Instantiate(prefab, position, Quaternion.identity);
        if (parent != null) go.transform.SetParent(parent, true);
        go.transform.localScale = Vector3.one * scale;

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startRotation = -angleDeg * Mathf.Deg2Rad;

            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r == null || r.renderMode == ParticleSystemRenderMode.None) continue;
            r.sortingLayerName = sortingLayer;
            r.sortingOrder = sortingOrder;
            if (material != null) r.sharedMaterial = material;
        }

        var root = go.GetComponentInChildren<ParticleSystem>();
        if (root != null) root.Play(true);
        if (lifetime > 0f) Object.Destroy(go, lifetime);
        return go;
    }

    // 날아가는 도중 방향을 바꿀 때 — 이미 나온 파티클과 앞으로 나올 파티클 모두 돌린다
    public static void SetRotation(GameObject go, float angleDeg)
    {
        float rad = -angleDeg * Mathf.Deg2Rad;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.startRotation = rad;
        }
    }
}
