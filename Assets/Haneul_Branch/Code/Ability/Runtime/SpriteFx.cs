using System.Collections.Generic;
using UnityEngine;

// 특성 효과용 한 장짜리 그림(AISprite에서 줄인 Fx_*)에 움직임을 붙여 애니메이션처럼 보이게 한다.
//
// GPT 그림은 프레임이 한 장뿐이라 그냥 띄우면 뻣뻣하다. 대신 종류마다 정해진 움직임을 코드로 준다:
//   · 투사체 : 튀어나오듯 등장 → 늘었다 줄었다 + 밝기 깜빡임 + 뒤로 잔상 → 사라질 때 퍼지며 흩어짐
//   · 폭발   : 흰 섬광이 먼저 번쩍 → 빠르게 커지며 터짐 → 살짝 돌며 옅어짐
//   · 링     : 가운데서 퍼지며 옅어짐 (바닥 원근으로 세로를 누름)
//   · 장판   : 바닥에 눌려 깔린 채 천천히 돌고, 안쪽 원은 반대로 돌며 맥박치듯 밝아졌다 어두워짐
//   · 표식   : 적 머리 위를 따라다니며 둥실거림
public enum FxSprite { Wave, Shard, SoulBolt, Phantom, Mark, EchoZone, Ring, SkullBurst, SoulBurst }

public static class SpriteFx
{
    public const string SortingLayer = "Skill";

    public static SpriteRenderer MakeRenderer(string name, Sprite sprite, Transform parent, int order)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = SortingLayer;
        sr.sortingOrder = order;
        return sr;
    }

    // 투사체 그림을 투사체 오브젝트에 붙인다
    public static FxProjectileVisual AttachProjectile(Transform projectile, FxSprite kind, float scale, float angle)
    {
        Sprite s = AbilityFxLibrary.Get(kind);
        if (s == null) return null;
        var v = projectile.gameObject.AddComponent<FxProjectileVisual>();
        v.Init(s, scale, angle);
        return v;
    }

    // 폭발 그림이 커질 수 있는 최대 반지름(칸). 그 이상 키우면 화면을 덮고 픽셀이 뭉개진다 —
    // 피해 범위가 더 넓으면 바깥은 퍼지는 링(Ring)으로 보여 준다
    public const float MaxBurstRadius = 2.5f;

    // 폭발 그림은 피해 반경보다 작게 그린다 — 반경 그대로 그리면 화면을 덮어 전투가 안 보인다
    public const float BurstVisualScale = 0.5f;

    // 폭발 — radius에 그림 지름을 맞춘다 (BurstVisualScale을 곱하고 MaxBurstRadius로 자른다)
    public static void Burst(FxSprite kind, Vector3 position, float radius, float duration = 0.55f)
    {
        Sprite s = AbilityFxLibrary.Get(kind);
        if (s == null) return;
        var go = new GameObject("Fx_" + kind);
        go.transform.position = position;
        float r = Mathf.Min(radius, MaxBurstRadius) * BurstVisualScale;
        go.AddComponent<FxBurst>().Init(s, r * 2f / Mathf.Max(0.01f, s.bounds.size.x), duration);
    }

    public static void Ring(Vector3 position, float radius, Color color, float duration = 0.35f)
    {
        Sprite s = AbilityFxLibrary.Get(FxSprite.Ring);
        if (s == null) { ShockwaveRing.Spawn(position, radius, color, duration); return; }
        var go = new GameObject("Fx_Ring");
        go.transform.position = position;
        go.AddComponent<FxRing>().Init(s, radius * 2f / Mathf.Max(0.01f, s.bounds.size.x), color, duration);
    }

    public static void Zone(Vector3 position, float radius, float duration)
    {
        Sprite s = AbilityFxLibrary.Get(FxSprite.EchoZone);
        if (s == null) { AreaMarker.Show(position, radius, new Color(0.75f, 0.1f, 0.25f, 0.45f), duration); return; }
        var go = new GameObject("Fx_EchoZone");
        go.transform.position = position;
        go.AddComponent<FxZone>().Init(s, radius * 2f / Mathf.Max(0.01f, s.bounds.size.x), duration);
    }

    public static FxMark Mark(Enemy enemy, float duration)
    {
        Sprite s = AbilityFxLibrary.Get(FxSprite.Mark);
        if (s == null || enemy == null) return null;
        var go = new GameObject("Fx_Mark");
        var m = go.AddComponent<FxMark>();
        m.Init(s, enemy, duration);
        return m;
    }

    public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
}

// ─── 투사체 ───
public class FxProjectileVisual : MonoBehaviour
{
    private SpriteRenderer body;
    private SpriteRenderer glow;
    private float baseScale;
    private float born;
    private float nextGhost;
    private Vector3 lastPos;
    private readonly float seed = Random.value * 10f;

    private const float PopIn = 0.08f;
    private const float GhostInterval = 0.03f;
    private const float GhostLife = 0.16f;

    public void Init(Sprite sprite, float scale, float angle)
    {
        baseScale = scale;
        born = Time.time;
        lastPos = transform.position;

        // 뒤에 같은 그림을 크고 흐리게 한 장 더 — 빛 번짐 대신
        glow = SpriteFx.MakeRenderer("Glow", sprite, transform, 39);
        glow.color = new Color(1f, 1f, 1f, 0.3f);
        body = SpriteFx.MakeRenderer("Body", sprite, transform, 40);
        SetAngle(angle);
        Apply(0f);
    }

    public void SetAngle(float angle)
    {
        Quaternion q = Quaternion.Euler(0f, 0f, angle);
        if (body != null) body.transform.rotation = q;
        if (glow != null) glow.transform.rotation = q;
    }

    private void Update()
    {
        Apply(Time.time - born);

        // 잔상 — 지나온 자리에 옅어지는 복사본을 남긴다
        if (Time.time >= nextGhost && (transform.position - lastPos).sqrMagnitude > 0.0001f)
        {
            nextGhost = Time.time + GhostInterval;
            var g = SpriteFx.MakeRenderer("Ghost", body.sprite, null, 38);
            g.transform.position = transform.position;
            g.transform.rotation = body.transform.rotation;
            g.transform.localScale = body.transform.lossyScale;
            g.color = new Color(1f, 1f, 1f, 0.45f);
            g.gameObject.AddComponent<FxFade>().Init(GhostLife, 0.75f);
        }
        lastPos = transform.position;
    }

    private void Apply(float t)
    {
        float pop = t < PopIn ? Mathf.Lerp(0.4f, 1.15f, t / PopIn) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - PopIn) / 0.08f));
        float wobble = Mathf.Sin((Time.time + seed) * 28f) * 0.08f;
        float sx = baseScale * pop * (1f + wobble);
        float sy = baseScale * pop * (1f - wobble * 0.6f);
        body.transform.localScale = new Vector3(sx, sy, 1f);
        glow.transform.localScale = new Vector3(sx * 1.35f, sy * 1.6f, 1f);

        // 밝기 깜빡임 — 색은 그대로 두고 밝기만 살짝 오르내린다
        float v = 0.82f + 0.18f * (0.5f + 0.5f * Mathf.Sin((Time.time + seed) * 40f));
        body.color = new Color(v, v, v, 1f);
        glow.color = new Color(1f, 1f, 1f, 0.22f + 0.12f * Mathf.Sin((Time.time + seed) * 18f));
    }

    // 투사체가 끝날 때 — 그림을 떼어 내 퍼지며 사라지게 한다
    public void Release()
    {
        foreach (var sr in new[] { body, glow })
        {
            if (sr == null) continue;
            sr.transform.SetParent(null, true);
            sr.gameObject.AddComponent<FxFade>().Init(0.14f, 1.5f);
        }
    }
}

// 옅어지며 커지거나 작아지다 사라지는 공용 조각
public class FxFade : MonoBehaviour
{
    private SpriteRenderer sr;
    private float life, t, endScale;
    private Vector3 start;
    private Color c0;

    public void Init(float life, float endScale)
    {
        sr = GetComponent<SpriteRenderer>();
        this.life = Mathf.Max(0.01f, life);
        this.endScale = endScale;
        start = transform.localScale;
        c0 = sr.color;
    }

    private void Update()
    {
        t += Time.deltaTime / life;
        if (t >= 1f) { Destroy(gameObject); return; }
        transform.localScale = start * Mathf.Lerp(1f, endScale, t);
        sr.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - t));
    }
}

// ─── 폭발 ───
public class FxBurst : MonoBehaviour
{
    private SpriteRenderer main, flash;
    private float scale, duration, t, spin;

    public void Init(Sprite sprite, float scale, float duration)
    {
        this.scale = scale;
        this.duration = Mathf.Max(0.1f, duration);
        spin = Random.Range(-25f, 25f);
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        main = SpriteFx.MakeRenderer("Main", sprite, transform, 45);
        flash = SpriteFx.MakeRenderer("Flash", sprite, transform, 46);
        Update();
    }

    private void Update()
    {
        t += Time.deltaTime;
        float k = t / duration;
        if (k >= 1f) { Destroy(gameObject); return; }

        // 0~30%: 빠르게 커지며 터짐 / 이후: 조금 더 커지며 옅어짐
        float grow = k < 0.3f ? SpriteFx.EaseOut(k / 0.3f) * 1.0f : 1f + (k - 0.3f) * 0.25f;
        main.transform.localScale = Vector3.one * scale * Mathf.Lerp(0.3f, 1f, grow);
        float alpha = k < 0.45f ? 1f : 1f - (k - 0.45f) / 0.55f;
        main.color = new Color(1f, 1f, 1f, alpha);
        transform.Rotate(0f, 0f, spin * Time.deltaTime);

        // 흰 섬광: 처음 0.12초 동안만 번쩍
        float f = Mathf.Clamp01(1f - t / 0.12f);
        flash.transform.localScale = Vector3.one * scale * Mathf.Lerp(0.5f, 0.85f, 1f - f);
        flash.color = new Color(1f, 1f, 1f, f * 0.9f);
    }
}

// ─── 링 ───
public class FxRing : MonoBehaviour
{
    private SpriteRenderer sr;
    private float scale, duration, t;
    private Color color;
    private const float Squash = 0.6f;

    public void Init(Sprite sprite, float scale, Color color, float duration)
    {
        this.scale = scale;
        this.color = color;
        this.duration = Mathf.Max(0.05f, duration);
        sr = SpriteFx.MakeRenderer("Ring", sprite, transform, 44);
        Update();
    }

    private void Update()
    {
        t += Time.deltaTime;
        float k = t / duration;
        if (k >= 1f) { Destroy(gameObject); return; }
        float s = scale * Mathf.Lerp(0.25f, 1f, SpriteFx.EaseOut(k));
        transform.localScale = new Vector3(s, s * Squash, 1f);
        sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k));
    }
}

// ─── 장판 ───
public class FxZone : MonoBehaviour
{
    private SpriteRenderer outer, inner;
    private Transform outerPivot, innerPivot;
    private float scale, duration, t;
    private const float Squash = 0.55f;

    public void Init(Sprite sprite, float scale, float duration)
    {
        this.scale = scale;
        this.duration = Mathf.Max(0.2f, duration);
        transform.localScale = new Vector3(1f, Squash, 1f);   // 바닥 원근 — 세로를 누른 부모 아래에서 돌린다

        outerPivot = new GameObject("Outer").transform; outerPivot.SetParent(transform, false);
        innerPivot = new GameObject("Inner").transform; innerPivot.SetParent(transform, false);
        outer = SpriteFx.MakeRenderer("Sprite", sprite, outerPivot, -30);
        inner = SpriteFx.MakeRenderer("Sprite", sprite, innerPivot, -29);
        outer.sortingLayerName = inner.sortingLayerName = "Object";
        Update();
    }

    private void Update()
    {
        t += Time.deltaTime;
        if (t >= duration) { Destroy(gameObject); return; }

        float appear = SpriteFx.EaseOut(t / 0.2f);
        float fade = t > duration - 0.4f ? (duration - t) / 0.4f : 1f;
        float pulse = 0.75f + 0.25f * Mathf.Sin(t * 7f);

        outerPivot.localScale = Vector3.one * scale * Mathf.Lerp(0.6f, 1f, appear);
        innerPivot.localScale = Vector3.one * scale * 0.55f * Mathf.Lerp(0.6f, 1f, appear);
        outerPivot.localRotation = Quaternion.Euler(0f, 0f, t * 30f);
        innerPivot.localRotation = Quaternion.Euler(0f, 0f, -t * 55f);
        outer.color = new Color(1f, 1f, 1f, appear * fade * pulse);
        inner.color = new Color(1f, 1f, 1f, appear * fade * pulse * 0.7f);
    }
}

// ─── 표식 ───
public class FxMark : MonoBehaviour
{
    private SpriteRenderer sr;
    private Enemy target;
    private float until, t;

    public void Init(Sprite sprite, Enemy enemy, float duration)
    {
        target = enemy;
        until = Time.time + duration;
        sr = SpriteFx.MakeRenderer("Mark", sprite, transform, 60);
        sr.sortingLayerName = "UI";
        Update();
    }

    public void Extend(float duration) { until = Mathf.Max(until, Time.time + duration); }

    private void Update()
    {
        t += Time.deltaTime;
        if (target == null || target.isDead || Time.time >= until) { Destroy(gameObject); return; }

        Bounds b = PlayerHitSparkVfx.BodyBounds(target);
        float bob = Mathf.Sin(t * 4f) * 0.07f;
        transform.position = new Vector3(b.center.x, b.max.y + 0.35f + bob, 0f);

        float pop = t < 0.12f ? Mathf.Lerp(1.6f, 1f, t / 0.12f) : 1f + Mathf.Sin(t * 6f) * 0.06f;
        transform.localScale = Vector3.one * pop;
        float fade = until - Time.time < 0.5f ? (until - Time.time) / 0.5f : 1f;
        sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / 0.1f) * fade);
    }
}
