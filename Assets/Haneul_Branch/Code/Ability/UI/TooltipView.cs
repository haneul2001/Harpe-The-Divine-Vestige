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
    private RectTransform innerRect;

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
        bool pictured = skin.TooltipBackground() != null;
        if (pictured)
        {
            // 배경 그림이 들어오면 안쪽 단색 판은 겹치므로 끄고, 테두리 두께를 패널과 같은 배율로
            fillImage.enabled = false;
            frameImage.color = Color.white;
            frameImage.pixelsPerUnitMultiplier = 1f / Mathf.Max(skin.PixelScale(), 0.01f);
        }

        RectTransform inner = (RectTransform)root.GetChild(0);
        var layout = inner.gameObject.AddComponent<VerticalLayoutGroup>();
        // 픽셀 테두리(8px × 배율) 안으로 글자가 들어가게 여백을 넉넉히
        layout.padding = pictured ? new RectOffset(24, 24, 20, 22) : new RectOffset(14, 14, 12, 12);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 픽셀 폰트(12px 격자) 기준: 제목·본문 24, 한 줄 설명 12
        titleText  = UIFactory.Label("Title", inner, font, 24, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
        bodyText   = UIFactory.Label("Body", inner, font, 22, new Color(0.86f, 0.87f, 0.90f));
        footerText = UIFactory.Label("Footer", inner, font, 12, new Color(0.55f, 0.56f, 0.62f), TextAnchor.UpperLeft, FontStyle.Italic);

        // 높이는 Show에서 내용물 기준으로 직접 잡는다.
        // 바깥에 ContentSizeFitter를 달면 자식 레이아웃이 아니라 배경 Image의 크기(9-slice 최소 크기)를 따라가
        // 툴팁이 16px로 쪼그라들고 글자가 배경 밖으로 흘러나온다.
        innerRect = inner;

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
        LayoutRebuilder.ForceRebuildLayoutImmediate(innerRect);
        float inset = -innerRect.offsetMax.y + innerRect.offsetMin.y;   // 테두리 두께(위+아래)
        root.sizeDelta = new Vector2(maxWidth, LayoutUtility.GetPreferredHeight(innerRect) + inset);
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

        // 커서가 화면 구석이거나 창 밖이어도 툴팁 전체가 화면 안에 남게 최종 고정
        pos.x = Mathf.Clamp(pos.x, 0f, Mathf.Max(0f, canvasSize.x - size.x));
        pos.y = Mathf.Clamp(pos.y, Mathf.Min(size.y, canvasSize.y), canvasSize.y);

        root.anchoredPosition = pos;
    }
}
