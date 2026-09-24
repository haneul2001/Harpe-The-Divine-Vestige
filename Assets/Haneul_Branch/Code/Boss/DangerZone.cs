using UnityEngine;

// 공격이 들어올 자리를 미리 보여 주는 빨간 표시.
//
// 테두리가 먼저 그려지고 속이 빨갛게 차오른다. 다 차는 순간이 곧 공격이 터지는 순간이다 —
// "얼마나 남았는지"를 숫자가 아니라 면적으로 읽게 하려는 것이다.
//
// 그림은 코드로 만든다. 상자든 원이든 크기가 패턴마다 달라서 에셋으로 두면 배율이 어긋난다.
public class DangerZone : MonoBehaviour
{
    private static readonly Color Edge = new Color(1f, 0.25f, 0.2f, 0.95f);
    private static readonly Color Base = new Color(0.8f, 0.1f, 0.1f, 0.22f);
    private static readonly Color Fill = new Color(1f, 0.2f, 0.15f, 0.45f);

    private const string SortingLayer = "Object";   // 바닥 위, 캐릭터 아래
    private const int SortingOrder = 20;

    private SpriteRenderer fill;
    private Transform follow;       // 보스를 따라다녀야 하는 표시 (회전 베기 등)
    private Vector3 followOffset;
    private float duration;
    private float elapsed;
    private bool radial;            // 원형은 가운데서 커지고, 상자는 한쪽에서 밀려 찬다
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
        zone.duration = Mathf.Max(0.01f, duration);
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
        zone.duration = Mathf.Max(0.01f, duration);
        zone.radial = true;
        zone.Build(Vector2.one * radius * 2f, true);
        zone.SetFollow(follow);
        return zone;
    }

    private void SetFollow(Transform target)
    {
        follow = target;
        if (target != null) followOffset = transform.position - target.position;
    }

    private void Build(Vector2 size, bool round)
    {
        // ① 바탕 — 옅게 깔아 "여기가 범위"임을 보여 준다
        MakeLayer("Base", round ? RoundSprite() : BoxSprite(), Base, size, 0);

        // ② 차오르는 속
        fill = MakeLayer("Fill", round ? RoundSprite() : SolidSprite(), Fill, size, 1);
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
        MakeLayer("Edge", round ? RoundEdgeSprite() : EdgeSprite(), Edge, size, 2);
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
        float k = Mathf.Clamp01(elapsed / duration);

        if (fill != null)
        {
            if (radial) fill.transform.localScale = Vector3.one * k;
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

    // 타격이 들어왔다. 아직 덜 찼으면 즉시 채우고 걷는다 —
    // "아직 안 찼으니 안전하다"고 거짓말을 남기지 않기 위해서다.
    public void CompleteNow()
    {
        elapsed = duration;
        if (fill != null)
        {
            if (radial) fill.transform.localScale = Vector3.one;
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
