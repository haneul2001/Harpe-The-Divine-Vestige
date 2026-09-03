using UnityEngine;
using UnityEngine.UI;

// 2D Pixel Quest Vol.3 의 Dynamic Bars 프레임을 쓰는 막대 하나.
//
// 적 체력바와 플레이어 체력바가 같은 그림을 쓰므로 조립을 여기로 모았다.
// 이 클래스는 "어떻게 그리는가"만 알고, "무엇을 보여주는가"는 쓰는 쪽이 정한다.
// (CardFrameBuilder와 같은 발상)
//
// ★ 아래 수치는 프레임 원본(160x32)의 픽셀을 직접 읽어서 잰 값이다.
//   그림을 다른 것으로 바꾸면 반드시 다시 재야 한다. 눈대중으로 넣으면
//   체력이 프레임 테두리를 덮거나 홈 밖으로 삐져나온다.
public class PixelBar
{
    public const float SrcHeight  = 32f;   // 프레임 원본 높이
    public const float TrackLeft  = 55f;   // 왼쪽 끝 ~ 체력 홈 시작 (소켓 포함)
    public const float TrackRight = 26f;   // 체력 홈 끝 ~ 오른쪽 끝
    public const float TrackVert  = 14f;   // 홈은 y14~17 의 4픽셀뿐. 나머지는 전부 장식이다
    public const float SocketX    = 32f;   // 초상화 소켓 왼쪽
    public const float SocketSize = 16f;   // 소켓 한 변

    public RectTransform Root { get; private set; }
    public Image Frame { get; private set; }
    public Image Portrait { get; private set; }
    public Image Fill { get; private set; }
    public Image Trail { get; private set; }
    public Text Label { get; private set; }
    public RectTransform Segments { get; private set; }

    private RectTransform portraitBox;
    private RectTransform track;

    // withPortrait를 끄면 소켓 자리를 비워 둔다 (소울 게이지처럼 초상화가 필요 없는 막대).
    public static PixelBar Build(string name, Transform parent, Font font, int labelSize, bool withPortrait)
    {
        var bar = new PixelBar();

        bar.Root = UIFactory.Empty(name, parent);

        // 프레임 그림이 맨 아래. 체력은 그 위에 얹혀 홈을 덮는다 —
        // 프레임을 위에 두면 홈의 장식이 체력을 가려 버린다.
        bar.Frame = UIFactory.Panel("FrameArt", bar.Root, Color.white, false);
        UIFactory.SetAnchoredBox(bar.Frame.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bar.Frame.type = Image.Type.Sliced;

        if (withPortrait)
        {
            bar.portraitBox = UIFactory.Empty("PortraitBox", bar.Root);
            bar.portraitBox.anchorMin = new Vector2(0f, 0.5f);
            bar.portraitBox.anchorMax = new Vector2(0f, 0.5f);
            bar.portraitBox.pivot = new Vector2(0f, 0.5f);

            bar.Portrait = UIFactory.Panel("Portrait", bar.portraitBox, Color.white, false);
            UIFactory.SetAnchoredBox(bar.Portrait.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bar.Portrait.preserveAspect = true;
            bar.Portrait.enabled = false;
        }

        bar.track = UIFactory.Empty("Track", bar.Root);
        bar.track.anchorMin = Vector2.zero;
        bar.track.anchorMax = Vector2.one;

        // 잔상이 먼저(뒤에), 실제 값이 나중에(앞에) 그려져야 겹쳐 보인다
        bar.Trail = UIFactory.Panel("Trail", bar.track, new Color(0.98f, 0.88f, 0.55f, 0.9f), false);
        UIFactory.SetAnchoredBox(bar.Trail.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        bar.Fill = UIFactory.Panel("Fill", bar.track, Color.white, false);
        UIFactory.SetAnchoredBox(bar.Fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        bar.Segments = UIFactory.Empty("Segments", bar.track);
        UIFactory.SetAnchoredBox(bar.Segments, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 숫자는 홈 안이 아니라 프레임 전체의 한가운데에 얹는다.
        // 홈이 4픽셀뿐이라 그 안에 넣으면 글자가 잘린다.
        bar.Label = UIFactory.Label("Value", bar.Root, font, labelSize, Color.white,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(bar.Label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddOutline(bar.Label.gameObject);

        return bar;
    }

    // 프레임 그림은 정수 배로만 키운다. 9-slice 배율을 1/scale로 맞춰야
    // 원본 픽셀이 정확히 scale x scale 로 찍힌다. 소수 배율은 픽셀을 뭉갠다.
    public void Layout(float width, int scale, Sprite frameSprite, Color fallbackColor)
    {
        int s = Mathf.Max(1, scale);
        Root.sizeDelta = new Vector2(width, SrcHeight * s);

        Frame.sprite = frameSprite;
        Frame.pixelsPerUnitMultiplier = 1f / s;
        Frame.color = frameSprite != null ? Color.white : fallbackColor;

        // 그림이 있으면 원본에서 잰 홈 위치를, 없으면 단순 여백을 쓴다
        float l = frameSprite != null ? TrackLeft  * s : 8f;
        float r = frameSprite != null ? TrackRight * s : 8f;
        float v = frameSprite != null ? TrackVert  * s : 6f;
        UIFactory.SetAnchoredBox(track, Vector2.zero, Vector2.one, new Vector2(l, v), new Vector2(-r, -v));

        if (portraitBox != null)
        {
            bool show = frameSprite != null;
            portraitBox.gameObject.SetActive(show);
            if (show)
            {
                portraitBox.sizeDelta = new Vector2(SocketSize * s, SocketSize * s);
                portraitBox.anchoredPosition = new Vector2(SocketX * s, 0f);
            }
        }

        // 글자는 소켓을 피해 오른쪽 영역의 한가운데에 오게 한다
        UIFactory.SetAnchoredBox(Label.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(l, 0f), new Vector2(-r, 0f));
    }

    public void SetRatio(float ratio)  { SetX(Fill.rectTransform, ratio); }
    public void SetTrail(float ratio)  { SetX(Trail.rectTransform, ratio); }

    private static void SetX(RectTransform rt, float ratio)
    {
        Vector2 max = rt.anchorMax;
        max.x = Mathf.Clamp01(ratio);
        rt.anchorMax = max;
        rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
    }

    public void SetPortrait(Sprite sprite)
    {
        if (Portrait == null) return;
        Portrait.sprite = sprite;
        Portrait.enabled = sprite != null;
    }

    // 남은 양을 눈으로 세기 쉽도록 칸을 나눈다. 1이면 나누지 않는다.
    public void BuildSegments(int count)
    {
        for (int i = Segments.childCount - 1; i >= 0; i--)
            Object.Destroy(Segments.GetChild(i).gameObject);

        if (count <= 1) return;

        for (int i = 1; i < count; i++)
        {
            float t = (float)i / count;
            Image line = UIFactory.Panel("Seg" + i, Segments, new Color(0f, 0f, 0f, 0.5f), false);
            RectTransform rt = line.rectTransform;
            rt.anchorMin = new Vector2(t, 0f);
            rt.anchorMax = new Vector2(t, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(2f, 0f);
            rt.anchoredPosition = Vector2.zero;
        }
    }

    // 숫자가 어떤 색 위에서도 읽히도록. 그림자 하나로는 밝은 체력색 위에서 뭉개진다.
    public static void AddOutline(GameObject go)
    {
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(2f, 2f);
    }

    // "1,600 / 2,400 (67%)" 형태. 체력·소울 모두 같은 형식을 쓴다.
    public static string Format(int current, int max)
    {
        int pct = max > 0 ? Mathf.CeilToInt(current * 100f / max) : 0;
        return current.ToString("N0") + " / " + max.ToString("N0") + " (" + pct + "%)";
    }
}
