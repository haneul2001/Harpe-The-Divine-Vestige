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
    private static Font cachedBoldFont;
    private static bool boldLookedUp;

    // 기본 UI 폰트: 갈무리11 (한글 픽셀 폰트, SIL OFL — Fonts/Galmuri-LICENSE.txt).
    // Resources에 두어 빌드에서도 인스펙터 연결 없이 로드된다.
    public const string PixelFontName = "Galmuri11";
    public const string PixelBoldFontName = "Galmuri11-Bold";
    // 갈무리11은 12px 격자로 그려진 폰트라 12의 배수 크기에서만 픽셀이 반듯하다
    public const int PixelFontGrid = 12;

    // 인스펙터에서 폰트를 직접 넣었으면 그걸, 아니면 갈무리11을 쓴다.
    public static Font ResolveFont(Font preferred)
    {
        if (preferred != null) return preferred;
        if (cachedFont != null) return cachedFont;

        cachedFont = Resources.Load<Font>(PixelFontName);
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

    // 굵은 글씨용. 픽셀 폰트에 합성 볼드를 걸면 획이 번져서, 따로 그려진 굵은 폰트 파일을 쓴다.
    public static Font ResolveBoldFont()
    {
        if (!boldLookedUp)
        {
            cachedBoldFont = Resources.Load<Font>(PixelBoldFontName);
            boldLookedUp = true;
        }
        return cachedBoldFont;
    }

    // 픽셀 폰트는 격자 배수 크기로 반올림한다 (14 → 12, 16~29 → 24, 30~41 → 36 …).
    // 살짝 올려 반올림해서 16~17 같은 본문 크기가 12로 쪼그라들지 않게 했다.
    public static int PixelFontSize(int size)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt((size + 2f) / PixelFontGrid));
        return n * PixelFontGrid;
    }

    // 폰트·크기·스타일을 한 번에 픽셀 규칙대로 맞춘다. 씬에 미리 놓인 Text에도 쓸 수 있다.
    public static void ApplyPixelFont(Text txt, Font preferred, int size, FontStyle style)
    {
        Font regular = ResolveFont(preferred);
        bool isPixelFont = preferred == null && regular != null && regular.name == PixelFontName;

        if (!isPixelFont)
        {
            txt.font = regular;
            txt.fontSize = size;
            txt.fontStyle = style;
            return;
        }

        Font bold = ResolveBoldFont();
        bool wantBold = style == FontStyle.Bold || style == FontStyle.BoldAndItalic;

        txt.font = wantBold && bold != null ? bold : regular;
        txt.fontSize = PixelFontSize(size);
        // 기울임도 합성이라 픽셀 폰트에선 계단이 깨진다 — 색으로만 구분한다
        txt.fontStyle = wantBold && bold == null ? FontStyle.Bold : FontStyle.Normal;
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
        ApplyPixelFont(txt, font, size, style);
        txt.color = color;
        txt.alignment = anchor;
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

    // 픽셀아트 그림을 pixelScale배로 그리는 이미지. 9-slice 테두리 두께도 같이 커진다 —
    // 배율을 안 맞추면 카드는 2배인데 패널 테두리만 1배라 가늘고 어색해진다.
    public static Image PixelImage(string name, Transform parent, Sprite sprite, float pixelScale, bool raycast = false)
    {
        Image img = Panel(name, parent, Color.white, raycast);
        ApplySprite(img, sprite);
        img.pixelsPerUnitMultiplier = 1f / Mathf.Max(pixelScale, 0.01f);
        return img;
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
