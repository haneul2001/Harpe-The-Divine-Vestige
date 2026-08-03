using UnityEngine;
using UnityEngine.UI;

// 마우스를 따라다니는 공용 툴팁.
//
// 카드와 세트 아이콘이 같은 툴팁 하나를 돌려 쓴다. 각자 툴팁을 들고 있으면
// 동시에 두 개가 뜨거나, 마우스가 빠르게 지나갈 때 잔상이 남는다.
// 그래서 패널이 하나만 만들어 두고 Show/Hide만 호출하게 했다.
public class TooltipView : MonoBehaviour
{
    private RectTransform root;
    private RectTransform canvasRect;
    private Text titleText;
    private Text bodyText;
    private Text footerText;
    private ContentSizeFitter fitter;

    private float maxWidth;
    private Vector2 cursorOffset;

    public bool IsShowing { get; private set; }

    // 패널이 조립 시점에 한 번만 부른다.
    public void Build(RectTransform parent, Font font, float width,
        Color fill, Color border, Vector2 offset, AbilityUISkin skin)
    {
        canvasRect = parent;
        maxWidth = width;
        cursorOffset = offset;

        Image fillImage;
        root = UIFactory.BorderedPanel("Tooltip", parent, fill, border, 2f, out fillImage);
        root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
        root.pivot = new Vector2(0f, 1f);   // 커서 오른쪽 아래로 펼쳐지는 게 기본
        root.sizeDelta = new Vector2(width, 100f);

        // 툴팁이 자기 밑의 카드에 마우스 이벤트를 뺏으면 hover가 깜빡인다
        Image frameImage = root.GetComponent<Image>();
        frameImage.raycastTarget = false;
        UIFactory.ApplySprite(frameImage, skin.TooltipBackground());
        // 배경 그림이 들어오면 안쪽 단색 판은 겹치므로 끈다
        if (skin.TooltipBackground() != null) fillImage.enabled = false;

        RectTransform inner = (RectTransform)root.GetChild(0);
        var layout = inner.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        titleText  = UIFactory.Label("Title", inner, font, 30, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
        bodyText   = UIFactory.Label("Body", inner, font, 22, new Color(0.86f, 0.87f, 0.90f));
        footerText = UIFactory.Label("Footer", inner, font, 20, new Color(0.55f, 0.56f, 0.62f), TextAnchor.UpperLeft, FontStyle.Italic);

        // 내용 길이에 따라 세로로 늘어난다
        fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var innerFitter = inner.gameObject.AddComponent<ContentSizeFitter>();
        innerFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Hide();
    }

    public void Show(string title, Color titleColor, string body, string footer)
    {
        if (root == null) return;

        titleText.text = title;
        titleText.color = titleColor;

        bodyText.text = body;
        bodyText.gameObject.SetActive(!string.IsNullOrEmpty(body));

        footerText.text = footer;
        footerText.gameObject.SetActive(!string.IsNullOrEmpty(footer));

        root.gameObject.SetActive(true);
        IsShowing = true;

        // 켠 직후 한 프레임은 크기가 갱신되기 전이라 위치가 튄다. 즉시 강제 계산.
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        Follow(Input.mousePosition);
    }

    public void Hide()
    {
        if (root == null) return;
        root.gameObject.SetActive(false);
        IsShowing = false;
    }

    private void Update()
    {
        if (IsShowing) Follow(Input.mousePosition);
    }

    // 화면 밖으로 나가면 반대쪽으로 접어 준다
    private void Follow(Vector2 screenPos)
    {
        if (canvasRect == null) return;

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, null, out local))
            return;

        Vector2 size = root.rect.size;
        Vector2 canvasSize = canvasRect.rect.size;

        // canvasRect의 피벗 기준 좌표를 좌하단 원점으로 변환
        Vector2 pos = local + new Vector2(canvasSize.x * canvasRect.pivot.x,
                                          canvasSize.y * canvasRect.pivot.y);

        pos += cursorOffset;

        if (pos.x + size.x > canvasSize.x) pos.x -= size.x + cursorOffset.x * 2f;
        if (pos.y - size.y < 0f) pos.y = size.y;

        root.anchoredPosition = pos;
    }
}
