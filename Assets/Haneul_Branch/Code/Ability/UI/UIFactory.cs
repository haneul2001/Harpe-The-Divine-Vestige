using UnityEngine;
using UnityEngine.UI;

// 런타임 UI 조립용 헬퍼.
//
// 이 프로젝트는 미니맵처럼 UI를 코드로 만드는 방식을 이미 쓰고 있다.
// 매번 GameObject/RectTransform/Image 세 줄씩 쓰면 뷰 코드가 조립 코드에 묻히므로
// 여기로 몰아 둔다. 뷰 클래스들은 "무엇을 보여줄지"만 신경 쓰면 된다.
public static class UIFactory
{
    private static Font cachedFont;

    // 프로젝트가 쓰는 폰트를 그대로 빌려온다. 씬에 Text가 하나도 없으면 내장 폰트로 떨어진다.
    public static Font ResolveFont(Font preferred)
    {
        if (preferred != null) return preferred;
        if (cachedFont != null) return cachedFont;

        Text any = Object.FindObjectOfType<Text>();
        if (any != null && any.font != null)
        {
            cachedFont = any.font;
            return cachedFont;
        }

        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return cachedFont;
    }

    public static RectTransform Empty(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    // 화면/부모를 꽉 채우는 사각형
    public static RectTransform Stretch(string name, Transform parent)
    {
        RectTransform rt = Empty(name, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    public static Image Panel(string name, Transform parent, Color color, bool raycast = true)
    {
        RectTransform rt = Empty(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    // 테두리 있는 패널. 바깥 이미지가 테두리, 안쪽 이미지가 본체가 된다.
    // 반환값은 "안쪽" — 자식은 여기에 붙이면 된다.
    public static RectTransform BorderedPanel(string name, Transform parent,
        Color fill, Color border, float borderWidth, out Image fillImage)
    {
        Image outer = Panel(name, parent, border);
        RectTransform inner = Stretch("Fill", outer.transform);
        inner.offsetMin = Vector2.one * borderWidth;
        inner.offsetMax = -Vector2.one * borderWidth;

        fillImage = inner.gameObject.AddComponent<Image>();
        fillImage.color = fill;
        fillImage.raycastTarget = false;

        return outer.rectTransform;
    }

    public static Text Label(string name, Transform parent, Font font, int size,
        Color color, TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
    {
        RectTransform rt = Empty(name, parent);
        Text txt = rt.gameObject.AddComponent<Text>();
        txt.font = ResolveFont(font);
        txt.fontSize = size;
        txt.color = color;
        txt.alignment = anchor;
        txt.fontStyle = style;
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return txt;
    }

    // 그림이 있으면 꽂고, 없으면 단색 그대로 둔다.
    // 9-slice 테두리가 설정된 그림은 자동으로 Sliced로 잡아 준다 —
    // 프레임 이미지를 Simple로 늘리면 모서리가 뭉개지는데, 이걸 매번 손으로
    // 챙기게 하면 반드시 빠뜨린다.
    public static void ApplySprite(Image image, Sprite sprite)
    {
        if (image == null || sprite == null) return;

        image.sprite = sprite;
        image.type = sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
    }

    private static Sprite cachedGradient;

    // 위가 진하고 아래로 갈수록 투명해지는 세로 그라데이션.
    // 카드 안쪽에 등급 색을 은은하게 깔 때 쓴다. 텍스처 하나를 전 카드가 공유한다.
    public static Sprite VerticalGradient()
    {
        if (cachedGradient != null) return cachedGradient;

        const int h = 64;
        var tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < h; y++)
        {
            // 위(y=h-1)가 1, 아래가 0. 제곱으로 떨어뜨려 위쪽에 몰리게 한다.
            float k = y / (float)(h - 1);
            tex.SetPixel(0, y, new Color(1f, 1f, 1f, k * k));
        }
        tex.Apply();

        cachedGradient = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 16f);
        return cachedGradient;
    }

    public static void SetAnchoredBox(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}
