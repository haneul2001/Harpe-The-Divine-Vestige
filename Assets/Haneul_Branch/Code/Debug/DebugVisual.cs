using UnityEngine;

// 디버그 표시의 공통 뼈대.
//
// 등록/해제와 "언제 다시 반영할지"만 담당한다. 무엇을 어떻게 숨길지는 파생이 정한다.
// 런타임에 스폰되는 적도 OnEnable에서 현재 상태를 즉시 반영하므로
// 매니저를 껐다 켠 뒤에 태어난 몹도 규칙을 따른다.
public abstract class DebugVisual : MonoBehaviour
{
    protected virtual void OnEnable()
    {
        DebugBoxManager.Changed += Sync;
        Sync();
    }

    protected virtual void OnDisable()
    {
        DebugBoxManager.Changed -= Sync;
    }

    private void Sync()
    {
        Apply(DebugBoxManager.Visible);
    }

    protected abstract void Apply(bool visible);

    // 이 렌더러가 디버그 표시인지.
    //
    // 캐릭터 연출(피격 깜빡임, 은신 반투명)은 자식 SpriteRenderer를 싹 훑어
    // enabled나 color를 건드리는데, 그때 디버그 박스까지 같이 켜 버린다.
    // 그런 쪽에서 이 함수로 걸러 내면 디버그 표시의 주인은 매니저 하나로 유지된다.
    //
    // ★ 부모까지 거슬러 올라가면 안 된다.
    //   PlayerAttackDebugBox는 플레이어 루트에 붙으므로, 부모를 보면
    //   캐릭터 스프라이트까지 전부 디버그로 판정돼 연출이 통째로 죽는다.
    //   반드시 그 오브젝트 자신만 본다.
    public static bool Owns(Component target)
    {
        if (target == null) return false;

        return target.GetComponent<DebugVisualPart>() != null
            || target.GetComponent<DebugVisual>() != null;
    }

    // ─────────────────────────────────────────────
    // 파생들이 함께 쓰는 도형 스프라이트 (에셋 없이 코드로 생성)
    // ─────────────────────────────────────────────

    private static Sprite boxSprite;
    private static Sprite circleSprite;

    // 테두리만 있는 사각형 — 안을 꽉 채우면 화면이 가려져 플레이를 못 본다
    public static Sprite BoxOutline()
    {
        if (boxSprite != null) return boxSprite;

        const int size = 64;
        const int edge = 3;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool border = x < edge || y < edge || x >= size - edge || y >= size - edge;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, border ? 1f : 0.10f));
            }
        tex.Apply();

        // 9-slice로 늘려야 테두리 두께가 일정하게 유지된다
        boxSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size,
            0, SpriteMeshType.FullRect, new Vector4(edge + 1, edge + 1, edge + 1, edge + 1));
        return boxSprite;
    }

    public static Sprite CircleOutline()
    {
        if (circleSprite != null) return circleSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r;
                float dy = (y + 0.5f - r) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.95f) / 0.05f);
                float fill = d < 0.95f ? 0.08f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(ring + fill)));
            }
        tex.Apply();

        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return circleSprite;
    }

    // 디버그 표시는 항상 위에 보여야 한다
    protected static SpriteRenderer MakeRenderer(GameObject go, Sprite sprite, Color color)
    {
        go.AddComponent<DebugVisualPart>();   // 캐릭터 연출이 건드리지 않도록 표식

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = Vector2.one;
        sr.sortingLayerName = "Skill";
        sr.sortingOrder = 200;
        return sr;
    }
}
