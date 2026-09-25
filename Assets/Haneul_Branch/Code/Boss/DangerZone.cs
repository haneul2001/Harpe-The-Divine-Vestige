using System.Collections.Generic;
using UnityEngine;

// 공격이 들어올 자리를 미리 보여 주는 빨간 표시.
//
// 테두리가 먼저 그려지고 속이 빨갛게 차오른다. 다 차는 순간이 곧 공격이 터지는 순간이다 —
// "얼마나 남았는지"를 숫자가 아니라 면적으로 읽게 하려는 것이다.
//
// 그림은 코드로 만든다. 상자든 원이든 크기가 패턴마다 달라서 에셋으로 두면 배율이 어긋난다.
public class DangerZone : MonoBehaviour
{
    // 표시 전체의 진하기. 1이면 아래 색 그대로, 낮추면 바닥이 비쳐 보인다 —
    // 예고가 너무 진하면 그 위에 선 캐릭터와 지형이 안 읽힌다.
    private const float Opacity = 0.6f;

    private static readonly Color Edge = Fade(new Color(1f, 0.25f, 0.2f, 0.95f));
    private static readonly Color Base = Fade(new Color(0.8f, 0.1f, 0.1f, 0.22f));
    private static readonly Color Fill = Fade(new Color(1f, 0.2f, 0.15f, 0.45f));

    private static Color Fade(Color c)
    {
        return new Color(c.r, c.g, c.b, c.a * Opacity);
    }

    private const string SortingLayer = "Object";   // 바닥 위, 캐릭터 아래
    private const int SortingOrder = 20;

    private SpriteRenderer fill;
    private Transform follow;       // 보스를 따라다녀야 하는 표시 (회전 베기 등)
    private Vector3 followOffset;
    private float duration;
    private float fillTime;         // 다 차는 시점. 공격보다 한 박자 앞서야 한다
    private float elapsed;
    private bool radial;            // 원형은 가운데서 커지고, 상자는 한쪽에서 밀려 찬다
    private Vector3 fillFullScale = Vector3.one;   // 원이 다 찼을 때의 배율
    private Vector2 worldSize;      // 예고한 자리 — 공격 이펙트를 여기 맞춰 늘린다
    private bool holdWhenFull;      // 다 찬 뒤 실제 타격까지 기다릴지
    private float fullAt;
    private const float maxHold = 2.5f;

    // 상자 모양 위험지역. angle은 도(度), size는 월드 단위
    public static DangerZone Box(Vector2 center, Vector2 size, float angle, float duration, Transform follow = null)
    {
        var go = new GameObject("DangerZone");
        go.transform.position = center;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        var zone = go.AddComponent<DangerZone>();
        zone.SetTiming(duration);
        zone.radial = false;
        zone.Build(size, false);
        zone.SetFollow(follow);
        return zone;
    }

    // 원형 위험지역
    public static DangerZone Circle(Vector2 center, float radius, float duration, Transform follow = null)
    {
        var go = new GameObject("DangerZone");
        go.transform.position = center;

        var zone = go.AddComponent<DangerZone>();
        zone.SetTiming(duration);
        zone.radial = true;
        zone.Build(Vector2.one * radius * 2f, true);
        zone.SetFollow(follow);
        return zone;
    }

    // 표시는 공격보다 먼저 끝나야 한다.
    // 정확히 같은 순간에 채우면 "가득 찬 상태"가 한 프레임도 보이지 않아,
    // 덜 찬 그림이 사라지는 것만 보이고 "다 차기 전에 맞았다"가 된다.
    // 그래서 일찍 채우고, 남은 시간은 꽉 찬 채로 버틴다.
    //
    // 0.18초를 버티게 해 봤더니 그래도 "안 채우고 사라졌다"로 보였다 —
    // 마지막 구간은 면적이 빠르게 늘어서 눈이 채워지는 중으로 읽는 탓이다.
    // 꽉 찬 상태가 한눈에 읽히려면 0.2초는 넘겨야 한다.
    private void SetTiming(float seconds)
    {
        duration = Mathf.Max(0.01f, seconds);
        fillTime = Mathf.Max(0.05f, duration - Mathf.Clamp(duration * 0.25f, 0.2f, 0.5f));
    }

    // 부채꼴 위험지역. 꼭짓점이 center, angle 쪽으로 halfAngle만큼 벌어진다.
    //
    // 베기 전용이다 — 칼이 호를 그리며 지나가는데 네모로 예고하면 모서리가 거짓말을 한다.
    public static DangerZone Sector(Vector2 center, float radius, float halfAngle,
                                    float angle, float duration, Transform follow = null)
    {
        var go = new GameObject("DangerZone");
        go.transform.position = center;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        var zone = go.AddComponent<DangerZone>();
        zone.SetTiming(duration);
        zone.radial = true;
        zone.sectorHalfAngle = Mathf.Clamp(halfAngle, 1f, 180f);
        zone.Build(Vector2.one * radius * 2f, true);
        zone.SetFollow(follow);
        return zone;
    }

    private float sectorHalfAngle;   // 0이면 원, 아니면 부채꼴

    private void SetFollow(Transform target)
    {
        follow = target;
        if (target != null) followOffset = transform.position - target.position;
    }

    // 예고가 덮은 자리. 공격 이펙트를 이 크기로 늘려야 "예고한 만큼 맞는다"가 눈에도 지켜진다.
    public Vector2 WorldSize { get { return worldSize; } }
    public bool IsBox { get { return !radial; } }

    private void Build(Vector2 size, bool round)
    {
        worldSize = size;

        bool fan = sectorHalfAngle > 0.01f;
        Sprite body = fan ? WedgeSprite(sectorHalfAngle) : (round ? RoundSprite() : BoxSprite());
        Sprite outline = fan ? WedgeEdgeSprite(sectorHalfAngle) : (round ? RoundEdgeSprite() : EdgeSprite());

        // ① 바탕 — 옅게 깔아 "여기가 범위"임을 보여 준다
        MakeLayer("Base", body, Base, size, 0);

        // ② 차오르는 속
        fill = MakeLayer("Fill", fan ? body : (round ? RoundSprite() : SolidSprite()), Fill, size, 1);

        // 원은 가운데서 커진다. 다 찼을 때의 배율을 기억해 둬야 한다 —
        // 여기서 Vector3.one을 기준으로 잡으면 칸이 아무리 커도 1칸까지만 차오른다.
        fillFullScale = fill.transform.localScale;
        if (radial) fill.transform.localScale = Vector3.zero;
        else
        {
            // 왼쪽 끝을 기준으로 밀려 차오르게 — 부모를 옮겨 피벗을 옮긴 효과를 낸다
            var pivot = new GameObject("FillPivot");
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = new Vector3(-size.x * 0.5f, 0f, 0f);
            fill.transform.SetParent(pivot.transform, false);
            fill.transform.localPosition = new Vector3(size.x * 0.5f, 0f, 0f);
            pivot.transform.localScale = new Vector3(0f, 1f, 1f);
        }

        // ③ 테두리 — 가장 또렷해야 눈에 먼저 들어온다
        MakeLayer("Edge", outline, Edge, size, 2);
    }

    private SpriteRenderer MakeLayer(string name, Sprite sprite, Color color, Vector2 size, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingLayerName = SortingLayer;
        sr.sortingOrder = SortingOrder + order;

        // 9-slice로 늘려야 테두리 두께가 크기와 무관하게 일정하다
        if (sprite.border != Vector4.zero)
        {
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
        }
        else go.transform.localScale = new Vector3(size.x, size.y, 1f);

        return sr;
    }

    private void LateUpdate()
    {
        if (follow != null) transform.position = follow.position + followOffset;

        elapsed += Time.deltaTime;
        float k = Mathf.Clamp01(elapsed / fillTime);

        if (fill != null)
        {
            if (radial) fill.transform.localScale = fillFullScale * k;
            else fill.transform.parent.localScale = new Vector3(k, 1f, 1f);
        }

        if (elapsed < duration) return;

        // 다 찬 순간을 먼저 기록한다 — 기록 전에 검사하면 첫 프레임에 바로 지워진다
        if (fullAt <= 0f) fullAt = Time.time;

        // 다 찼는데도 아직 안 맞았으면 꽉 찬 채로 기다린다.
        // 표시가 먼저 사라지면 "예고가 끝났는데 왜 지금 맞지?"가 된다.
        if (!holdWhenFull) Destroy(gameObject);
        else if (Time.time - fullAt > maxHold) Destroy(gameObject);   // 타격이 영영 안 와도 남지 않게
    }

    // 실제 타격이 들어올 때까지 꽉 찬 채로 기다리게 한다
    public DangerZone HoldUntilHit()
    {
        holdWhenFull = true;
        return this;
    }

    // 다 차는 시점을 직접 정한다.
    // 보스는 예고가 끝나면 칼을 휘두르기 시작하고, 판정은 그보다 한참 뒤에 열린다.
    // 판정 시각에 맞춰 채우면 "칼은 벌써 나가는데 표시는 아직 차는 중"이 되므로,
    // 휘두르기 시작 전에 다 차게 만들고 나머지는 꽉 찬 채로 버틴다.
    public DangerZone FillIn(float seconds)
    {
        fillTime = Mathf.Clamp(seconds, 0.05f, duration);
        return this;
    }

    // 타격이 들어왔다. 아직 덜 찼으면 즉시 채우고 걷는다 —
    // "아직 안 찼으니 안전하다"고 거짓말을 남기지 않기 위해서다.
    public void CompleteNow()
    {
        elapsed = duration;
        if (fill != null)
        {
            if (radial) fill.transform.localScale = fillFullScale;
            else fill.transform.parent.localScale = Vector3.one;
        }
        Destroy(gameObject);
    }

    // 예고가 끝나기 전에 공격이 취소됐을 때 (보스 사망 등)
    public void Cancel()
    {
        Destroy(gameObject);
    }

    // ─── 코드로 만드는 그림 (한 번만 만들어 돌려 쓴다) ───

    private static Sprite solid, box, edge, round, roundEdge;

    private static Sprite SolidSprite()
    {
        if (solid == null) solid = Make(8, 8, delegate(int x, int y, int w, int h) { return 1f; }, Vector4.zero);
        return solid;
    }

    // 속이 빈 상자 — 바탕용으로 아주 옅게 깔린다
    private static Sprite BoxSprite()
    {
        if (box == null) box = SolidSprite();
        return box;
    }

    private static Sprite EdgeSprite()
    {
        if (edge == null)
            edge = Make(16, 16, delegate(int x, int y, int w, int h)
            {
                bool border = x < 2 || y < 2 || x >= w - 2 || y >= h - 2;
                return border ? 1f : 0f;
            }, new Vector4(4f, 4f, 4f, 4f));
        return edge;
    }

    private static Sprite RoundSprite()
    {
        if (round == null)
            round = Make(32, 32, delegate(int x, int y, int w, int h)
            {
                float dx = x - (w - 1) * 0.5f, dy = y - (h - 1) * 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / (w * 0.5f);
                return r <= 1f ? 1f : 0f;
            }, Vector4.zero);
        return round;
    }

    private static Sprite RoundEdgeSprite()
    {
        if (roundEdge == null)
            roundEdge = Make(32, 32, delegate(int x, int y, int w, int h)
            {
                float dx = x - (w - 1) * 0.5f, dy = y - (h - 1) * 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / (w * 0.5f);
                return (r <= 1f && r > 0.86f) ? 1f : 0f;
            }, Vector4.zero);
        return roundEdge;
    }

    // 부채꼴. 꼭짓점이 그림 한가운데, +x 쪽으로 벌어진다 —
    // 그래야 꼭짓점을 보스 발밑에 두고 겨눈 각도로 돌리기만 하면 된다.
    private static readonly Dictionary<int, Sprite> wedges = new Dictionary<int, Sprite>();
    private static readonly Dictionary<int, Sprite> wedgeEdges = new Dictionary<int, Sprite>();

    public static Sprite WedgeSprite(float halfAngle)
    {
        int key = Mathf.RoundToInt(halfAngle);
        Sprite s;
        if (wedges.TryGetValue(key, out s) && s != null) return s;

        float limit = key * Mathf.Deg2Rad;
        s = Make(96, 96, delegate(int x, int y, int w, int h)
        {
            float dx = x - (w - 1) * 0.5f, dy = y - (h - 1) * 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) / (w * 0.5f);
            if (r > 1f) return 0f;
            return Mathf.Abs(Mathf.Atan2(dy, dx)) <= limit ? 1f : 0f;
        }, Vector4.zero);

        wedges[key] = s;
        return s;
    }

    public static Sprite WedgeEdgeSprite(float halfAngle)
    {
        int key = Mathf.RoundToInt(halfAngle);
        Sprite s;
        if (wedgeEdges.TryGetValue(key, out s) && s != null) return s;

        float limit = key * Mathf.Deg2Rad;
        s = Make(96, 96, delegate(int x, int y, int w, int h)
        {
            float dx = x - (w - 1) * 0.5f, dy = y - (h - 1) * 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) / (w * 0.5f);
            if (r > 1f) return 0f;

            float a = Mathf.Abs(Mathf.Atan2(dy, dx));
            bool arc = r > 0.9f && a <= limit;                  // 바깥 호
            bool side = a > limit - 0.055f && a <= limit;       // 양 옆 변
            return (arc || side) ? 1f : 0f;
        }, Vector4.zero);

        wedgeEdges[key] = s;
        return s;
    }

    private static Sprite Make(int w, int h, System.Func<int, int, int, int, float> alpha, Vector4 border)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x, y, w, h)) * 255f));

        tex.SetPixels32(px);
        tex.Apply();

        // ppu를 폭과 같게 두면 스프라이트 한 장이 정확히 1 월드 단위가 된다 — 크기 계산이 단순해진다
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w, 0,
            SpriteMeshType.FullRect, border);
    }
}
