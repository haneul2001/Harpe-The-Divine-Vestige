using UnityEngine;
using UnityEngine.UI;

// 타이틀 화면의 시작 버튼. 지금은 메뉴가 "START" 하나뿐이다.
//
// 버튼은 코드로 짠다(체력바·스킬 칸과 같은 방식) — 사신 톤 팔레트(회색·진보라·창백한 연보라)를 한곳에서 맞추기 쉽다.
// 모양: 가운데 글자 + 양옆 가는 선과 마름모 장식 + 뒤에 옅은 보라 빛 띠.
//   · 가만히 있으면 글자가 천천히 숨 쉬듯 밝아졌다 어두워진다 (누를 곳이 여기라는 신호)
//   · 마우스를 올리면 빛 띠가 차오르고 장식이 바깥으로 벌어지며 글자가 밝아진다
//   · 누르면(클릭 / Enter / Space) 한 번 번쩍이고 TitleManager가 화면을 어둡게 닫으며 다음 씬으로 간다
// 옛 기본 버튼(START·OPTIONS·CREDITS·QUIT·TUTORIAL)이 씬에 남아 있으면 치운다.
public class TitleMenu : MonoBehaviour
{
    [Header("배치 (1920x1080 기준)")]
    [Tooltip("화면 아래에서 버튼 가운데까지. 배경 그림의 언덕 위 어두운 띠에 걸리게")]
    [SerializeField] private float bottomOffset = 292f;
    [SerializeField] private string label = "START";
    [Tooltip("갈무리는 11px 격자 글꼴이라 11의 배수에서 선명하다")]
    [SerializeField] private int fontSize = 44;

    [Header("색 (사신 톤)")]
    [SerializeField] private Color textIdle = new Color32(0xA7, 0xA0, 0xBC, 0xFF);
    [SerializeField] private Color textHover = new Color32(0xE6, 0xDD, 0xF6, 0xFF);
    [SerializeField] private Color ornamentIdle = new Color32(0x79, 0x72, 0x8C, 0xFF);
    [SerializeField] private Color ornamentHover = new Color32(0xBD, 0xAA, 0xDD, 0xFF);
    [SerializeField] private Color glow = new Color32(0x5A, 0x4B, 0xA3, 0xFF);
    [SerializeField] private Color outline = new Color32(0x14, 0x12, 0x20, 0xFF);

    [Header("AI 리소스 안내 (오른쪽 아래)")]
    [TextArea] [SerializeField] private string aiNotice = "이 게임에는 생성형 AI를 활용해 제작한 리소스가 일부 포함되어 있습니다.";
    [SerializeField] private int aiNoticeSize = 18;
    [SerializeField] private Color aiNoticeColor = new Color32(0x79, 0x72, 0x8C, 0xFF);
    [SerializeField] private Vector2 aiNoticeMargin = new Vector2(28f, 22f);

    [Header("움직임")]
    [SerializeField] private float introDelay = 0.5f;
    [SerializeField] private float introDuration = 0.8f;
    [Tooltip("숨쉬기 한 번(초)")]
    [SerializeField] private float breathPeriod = 2.4f;
    [SerializeField] private float ornamentSpread = 14f;

    private static readonly string[] LegacyButtons = { "START", "OPTIONS", "CREDITS", "QUIT", "TUTORIAL" };

    private TitleManager manager;
    private CanvasGroup group;
    private RectTransform hit;
    private Text text;
    private Image glowBand;
    private RectTransform leftOrn, rightOrn;
    private Image[] ornaments;
    private float hover;         // 0 → 1 로 부드럽게
    private float flash;         // 눌렀을 때 번쩍임
    private float startTime;
    private bool pressed;

    private void Awake()
    {
        manager = FindObjectOfType<TitleManager>();
        RemoveLegacyButtons();
        Build();
        startTime = Time.unscaledTime;
    }

    private void RemoveLegacyButtons()
    {
        foreach (var b in FindObjectsOfType<Button>(true))
            if (System.Array.IndexOf(LegacyButtons, b.gameObject.name) >= 0) Destroy(b.gameObject);
    }

    private void Build()
    {
        Font font = Resources.Load<Font>("Galmuri11-Bold");

        var canvasGo = new GameObject("TitleMenuCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1;      // 씬 캔버스의 암전(FadeImage)이 이 위를 덮어야 한다
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = canvasGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        var root = NewRect("Start", canvasGo.transform);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = new Vector2(0f, bottomOffset);
        root.sizeDelta = new Vector2(520f, 90f);
        hit = root;

        // 뒤에 옅은 보라 빛 띠 (가운데가 짙고 양끝·위아래로 사라지는 타원)
        glowBand = NewImage("Glow", root, glow);
        glowBand.sprite = MakeGlowSprite();
        glowBand.rectTransform.sizeDelta = new Vector2(520f, 96f);

        text = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        text.transform.SetParent(root, false);
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = label;
        text.color = textIdle;
        text.rectTransform.sizeDelta = new Vector2(400f, 80f);
        var ol = text.gameObject.AddComponent<Outline>();
        ol.effectColor = outline;
        ol.effectDistance = new Vector2(3f, -3f);
        var sh = text.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
        sh.effectDistance = new Vector2(0f, -6f);

        // AI 리소스 안내 — 오른쪽 아래 작게. 버튼과 함께 떠오른다
        if (!string.IsNullOrEmpty(aiNotice))
        {
            var note = new GameObject("AiNotice", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            note.transform.SetParent(canvasGo.transform, false);
            note.font = font;
            note.fontSize = aiNoticeSize;
            note.color = aiNoticeColor;
            note.alignment = TextAnchor.LowerRight;
            note.horizontalOverflow = HorizontalWrapMode.Overflow;
            note.verticalOverflow = VerticalWrapMode.Overflow;
            note.raycastTarget = false;
            note.text = aiNotice;
            var nr = note.rectTransform;
            nr.anchorMin = nr.anchorMax = nr.pivot = new Vector2(1f, 0f);
            nr.anchoredPosition = new Vector2(-aiNoticeMargin.x, aiNoticeMargin.y);
            nr.sizeDelta = new Vector2(900f, 30f);
            var ns = note.gameObject.AddComponent<Shadow>();
            ns.effectColor = new Color(0f, 0f, 0f, 0.8f);
            ns.effectDistance = new Vector2(2f, -2f);
        }

        // 양옆 장식: 가는 선 + 마름모 (옛 그림의 ◇ START ◇ 모양을 사신 톤으로)
        float textHalf = text.preferredWidth * 0.5f;
        leftOrn = Ornament("Left", root, -1f, textHalf);
        rightOrn = Ornament("Right", root, 1f, textHalf);
        ornaments = new[] {
            leftOrn.GetChild(0).GetComponent<Image>(), leftOrn.GetChild(1).GetComponent<Image>(),
            rightOrn.GetChild(0).GetComponent<Image>(), rightOrn.GetChild(1).GetComponent<Image>() };
    }

    // side: -1 왼쪽, 1 오른쪽. 마름모가 글자 쪽, 선이 바깥쪽
    private RectTransform Ornament(string name, RectTransform parent, float side, float textHalf)
    {
        var o = NewRect(name, parent);
        o.anchoredPosition = new Vector2(side * (textHalf + 34f), 2f);
        o.sizeDelta = Vector2.zero;

        var diamond = NewImage("Diamond", o, ornamentIdle);
        diamond.rectTransform.sizeDelta = new Vector2(12f, 12f);
        diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        var line = NewImage("Line", o, ornamentIdle);
        line.rectTransform.pivot = new Vector2(side > 0 ? 0f : 1f, 0.5f);
        line.rectTransform.anchoredPosition = new Vector2(side * 16f, 0f);
        line.rectTransform.sizeDelta = new Vector2(64f, 3f);
        return o;
    }

    private void Update()
    {
        float t = Time.unscaledTime - startTime;
        float intro = Mathf.Clamp01((t - introDelay) / Mathf.Max(0.01f, introDuration));
        group.alpha = intro * intro * (3f - 2f * intro);
        bool ready = intro >= 1f && !pressed;

        bool over = ready && RectTransformUtility.RectangleContainsScreenPoint(hit, Input.mousePosition, null);
        hover = Mathf.MoveTowards(hover, over ? 1f : 0f, Time.unscaledDeltaTime * 6f);

        if (ready && ((over && Input.GetMouseButtonDown(0))
            || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
            Press();

        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 2.5f);

        // 가만히 있을 때만 숨쉬기 — 마우스를 올리면 밝게 고정
        float breath = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.1f, breathPeriod));
        float idleLevel = Mathf.Lerp(0.72f, 1f, breath);
        Color c = Color.Lerp(textIdle * new Color(1f, 1f, 1f, idleLevel), textHover, hover);
        text.color = Color.Lerp(c, Color.white, flash);

        Color oc = Color.Lerp(ornamentIdle, ornamentHover, Mathf.Max(hover, flash));
        foreach (var o in ornaments) o.color = oc;

        float spread = ornamentSpread * hover;
        float textHalf = text.preferredWidth * 0.5f;
        leftOrn.anchoredPosition = new Vector2(-(textHalf + 34f + spread), 2f);
        rightOrn.anchoredPosition = new Vector2(textHalf + 34f + spread, 2f);

        glowBand.color = new Color(glow.r, glow.g, glow.b, Mathf.Clamp01(0.45f + 0.35f * hover + 0.5f * flash));
    }

    private void Press()
    {
        pressed = true;
        flash = 1f;
        hover = 1f;
        if (manager != null) manager.StartGame();
        else Debug.LogWarning("[TitleMenu] 씬에 TitleManager가 없어 시작할 수 없다");
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // 가로로 긴 부드러운 타원 빛. 가운데 불투명 → 가장자리 투명
    private static Sprite glowSprite;
    private static Sprite MakeGlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int w = 128, h = 32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return glowSprite;
    }
}
