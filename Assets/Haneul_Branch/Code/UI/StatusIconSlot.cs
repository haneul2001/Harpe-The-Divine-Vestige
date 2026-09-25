using UnityEngine;
using UnityEngine.UI;

// 체력바 위에 줄지어 놓이는 상태 아이콘 한 칸 (대시 쿨타임, 처형 버프 등).
//
// 칸 그림(StatusSlot.png)은 체력바 프레임의 물약 소켓을 그대로 떠 온 26x26 픽셀이고,
// 아이콘은 그 안쪽 16x16 자리에 들어간다. 체력바와 같은 배율(frameScale)로 정수 배 확대한다.
// 아이콘 그림은 48x48(안쪽 16칸 × 3배 = 화면 1080p 기준 48px과 1:1)로 만들어 둔다.
//
// PixelBar처럼 "어떻게 그리는가"만 알고, 무엇을 보여줄지는 PlayerStatusBar가 정한다.
public class StatusIconSlot
{
    public const float SlotPixels = 26f;   // 칸 그림 한 변
    public const float IconPixels = 16f;   // 안쪽 아이콘 자리 한 변 (가운데 정렬, 사방 5픽셀 테두리)

    public RectTransform Root { get; private set; }

    private Image icon;
    private Image flame;
    private Text placeholder;
    private RectTransform shade;
    private Text timer;
    private Text badge;
    private int scale;

    public static StatusIconSlot Build(string name, Transform parent, Sprite slotSprite, Sprite iconSprite,
        string placeholderText, int pixelScale, Font font)
    {
        var slot = new StatusIconSlot();
        int s = Mathf.Max(1, pixelScale);
        slot.scale = s;

        slot.Root = UIFactory.Empty(name, parent);
        slot.Root.anchorMin = Vector2.zero;
        slot.Root.anchorMax = Vector2.zero;
        slot.Root.pivot = Vector2.zero;
        slot.Root.sizeDelta = new Vector2(SlotPixels * s, SlotPixels * s);

        Image frame = UIFactory.PixelImage("Frame", slot.Root, slotSprite, s);
        UIFactory.SetAnchoredBox(frame.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        if (slotSprite == null) frame.color = new Color(0.11f, 0.10f, 0.15f, 1f);

        // 안쪽 16x16 자리
        RectTransform inner = UIFactory.Empty("Inner", slot.Root);
        inner.anchorMin = inner.anchorMax = new Vector2(0.5f, 0.5f);
        inner.pivot = new Vector2(0.5f, 0.5f);
        inner.sizeDelta = new Vector2(IconPixels * s, IconPixels * s);
        inner.anchoredPosition = Vector2.zero;

        slot.icon = UIFactory.PixelImage("Icon", inner, null, s);
        UIFactory.SetAnchoredBox(slot.icon.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 그림이 아직 없을 때 쓰는 임시 글자
        slot.placeholder = UIFactory.Label("Placeholder", inner, font, 12, new Color(0.62f, 0.58f, 0.70f, 1f),
            TextAnchor.MiddleCenter, FontStyle.Bold);
        slot.placeholder.fontSize = 12;
        slot.placeholder.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIFactory.SetAnchoredBox(slot.placeholder.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        slot.placeholder.text = placeholderText;

        // 쿨타임 가림막: 위에서부터 남은 비율만큼 덮는다. 한 줄씩(원본 1픽셀 단위) 줄어들게 끊는다
        Image shadeImg = UIFactory.Panel("Shade", inner, new Color(0f, 0f, 0f, 0.62f), false);
        slot.shade = shadeImg.rectTransform;
        slot.shade.pivot = new Vector2(0.5f, 1f);

        slot.timer = UIFactory.Label("Timer", inner, font, 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        slot.timer.fontSize = 18;
        slot.timer.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIFactory.SetAnchoredBox(slot.timer.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        PixelBar.AddOutline(slot.timer.gameObject);

        // 오른쪽 아래 작은 숫자 (버프 단계)
        slot.badge = UIFactory.Label("Badge", slot.Root, font, 18, new Color(1f, 0.92f, 0.7f, 1f),
            TextAnchor.LowerRight, FontStyle.Bold);
        slot.badge.fontSize = 18;
        slot.badge.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIFactory.SetAnchoredBox(slot.badge.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(0f, 2f * s), new Vector2(-2f * s, 0f));
        PixelBar.AddOutline(slot.badge.gameObject);

        slot.SetIcon(iconSprite);
        slot.SetShade(0f);
        slot.SetTimer(0f);
        slot.SetBadge(null);
        return slot;
    }

    // 칸 위에 발동 키를 키캡 모양으로 띄운다 (스킬 칸 전용)
    public void SetKeyLabel(string keyName, Sprite capSprite, Font font, int capScale)
    {
        if (string.IsNullOrEmpty(keyName)) return;
        int cs = Mathf.Max(1, capScale);

        Image cap = UIFactory.PixelImage("KeyCap", Root, capSprite, cs);
        if (capSprite == null) cap.color = new Color(0.26f, 0.24f, 0.31f, 1f);
        RectTransform rt = cap.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 2f * scale);

        Text label = UIFactory.Label("Key", rt, font, 18, new Color(0.93f, 0.90f, 0.96f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        label.fontSize = 18;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.text = keyName;
        // 키캡 아랫단 음영(원본 2픽셀)만큼 글자를 위로 올려 윗면 가운데에 앉힌다
        UIFactory.SetAnchoredBox(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 2f * cs), Vector2.zero);

        // 폭은 글자에 맞춰 늘리고, 원본 픽셀 격자(cs)에 맞춰 반올림한다
        float h = KeyCapPixels * cs;
        float w = Mathf.Max(h, label.preferredWidth + 6f * cs);
        w = Mathf.Ceil(w / cs) * cs;
        rt.sizeDelta = new Vector2(w, h);
    }

    // 마우스 버튼은 키캡에 글자를 적는 대신 그림을 올린다.
    // "좌클"이라고 적어 두면 읽어야 알지만, 버튼 한쪽이 칠해진 마우스 그림은 보면 바로 안다.
    public void SetKeyIcon(Sprite glyph, int capScale)
    {
        if (glyph == null) return;
        int cs = Mathf.Max(1, capScale);

        Image img = UIFactory.PixelImage("KeyIcon", Root, glyph, cs);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 2f * scale);
        rt.sizeDelta = new Vector2(glyph.rect.width * cs, glyph.rect.height * cs);
    }

    public const float KeyCapPixels = 14f;   // 키캡 그림 높이

    // 키 이름을 짧게 (LeftShift → Shift, Alpha1 → 1)
    public static string KeyName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.LeftShift: case KeyCode.RightShift: return "Shift";
            case KeyCode.LeftControl: case KeyCode.RightControl: return "Ctrl";
            case KeyCode.LeftAlt: case KeyCode.RightAlt: return "Alt";
            case KeyCode.Space: return "Space";
            // 마우스는 약자보다 우리말이 빨리 읽힌다. 키캡 글꼴(Galmuri)이 한글을 그린다
            case KeyCode.Mouse0: return "좌클";
            case KeyCode.Mouse1: return "우클";
        }
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString();
        return key.ToString();
    }

    // 아이콘 뒤에서 타오르는 불꽃 (몬스터 처형 표시의 붉은 불꽃을 프레임별로 떠 둔 그림).
    // 파티클은 UI 캔버스에 안 그려지므로 스프라이트를 갈아 끼워 재생한다.
    // 프레임 그림은 1080p 기준 1:1 크기로 구워 두었으므로 픽셀 크기 그대로 놓는다
    public void SetBackFlame(bool on, Sprite[] frames, float fps, Vector2 offset, float sizeScale)
    {
        if (flame == null)
        {
            if (!on || frames == null || frames.Length == 0) return;
            flame = UIFactory.Panel("Flame", Root, Color.white, false);
            flame.transform.SetSiblingIndex(1);   // 칸 그림(0번) 위, 아이콘 아래
            RectTransform rt = flame.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        flame.enabled = on;
        if (!on || frames == null || frames.Length == 0) return;

        int i = Mathf.FloorToInt(Time.unscaledTime * Mathf.Max(0.1f, fps)) % frames.Length;
        Sprite f = frames[i];
        flame.sprite = f;
        float k = (scale / 3f) * Mathf.Max(0.1f, sizeScale);
        if (f != null) flame.rectTransform.sizeDelta = f.rect.size * k;
        flame.rectTransform.anchoredPosition = offset * k;
    }

    // 쓸 수 없을 때 아이콘을 어둡게
    public void SetDimmed(bool dimmed)
    {
        Color c = dimmed ? new Color(0.45f, 0.45f, 0.5f, 1f) : Color.white;
        icon.color = c;
        placeholder.color = dimmed ? new Color(0.40f, 0.37f, 0.46f, 1f) : new Color(1f, 0.85f, 0.85f, 1f);
    }

    public void SetVisible(bool visible)
    {
        if (Root.gameObject.activeSelf != visible) Root.gameObject.SetActive(visible);
    }

    public void SetIcon(Sprite sprite)
    {
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        placeholder.enabled = sprite == null;
    }

    // 0 = 가림막 없음, 1 = 전부 덮음
    public void SetShade(float ratio)
    {
        float rows = Mathf.Ceil(Mathf.Clamp01(ratio) * IconPixels);
        shade.gameObject.SetActive(rows > 0f);
        shade.anchorMin = new Vector2(0f, 1f);
        shade.anchorMax = new Vector2(1f, 1f);
        shade.sizeDelta = new Vector2(0f, rows * scale);
        shade.anchoredPosition = Vector2.zero;
    }

    // 남은 초. 0이면 숨긴다. 10초 미만은 소수 한 자리
    public void SetTimer(float seconds)
    {
        if (seconds <= 0f) { timer.enabled = false; return; }
        timer.enabled = true;
        timer.text = seconds < 10f ? seconds.ToString("0.0") : Mathf.CeilToInt(seconds).ToString();
    }

    public void SetBadge(string text)
    {
        badge.enabled = !string.IsNullOrEmpty(text);
        badge.text = text ?? "";
    }
}
