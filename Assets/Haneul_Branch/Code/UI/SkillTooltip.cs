using UnityEngine;
using UnityEngine.UI;

// 화면 아래 스킬 칸에 마우스를 올리면 뜨는 설명 패널.
//
// 숫자는 전부 실제 컴포넌트에서 그때그때 읽는다. 설명에 숫자를 적어 두면
// 밸런스를 만질 때마다 설명이 거짓말이 되고, 그걸 눈치채는 건 한참 뒤다.
public class SkillTooltip
{
    private readonly RectTransform root;
    private readonly Text titleText;
    private readonly Text keyText;
    private readonly Text bodyText;
    private readonly float pad;

    // 칸 위 키캡을 비켜 가는 높이
    private const float GapAboveSlot = 48f;

    public RectTransform Root { get { return root; } }

    public SkillTooltip(Transform parent, Font font, int scale)
    {
        int s = Mathf.Max(1, scale);
        pad = 8f * s;

        Image fill;
        root = UIFactory.BorderedPanel("SkillTooltip", parent,
            new Color(0.07f, 0.065f, 0.09f, 0.96f), new Color(0.29f, 0.26f, 0.35f, 1f), Mathf.Max(2, s), out fill);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0f);

        titleText = UIFactory.Label("Title", root, font, 20,
            new Color(0.93f, 0.90f, 0.96f, 1f), TextAnchor.UpperLeft, FontStyle.Bold);

        // 키 이름은 제목 오른쪽 끝에 회색으로 — 제목과 같은 줄에 두면 폭 계산이 흔들린다
        keyText = UIFactory.Label("Key", root, font, 16,
            new Color(0.62f, 0.58f, 0.72f, 1f), TextAnchor.UpperRight, FontStyle.Normal);

        bodyText = UIFactory.Label("Body", root, font, 16,
            new Color(0.78f, 0.75f, 0.85f, 1f), TextAnchor.UpperLeft, FontStyle.Normal);

        foreach (var t in new Text[] { titleText, keyText, bodyText })
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
        }

        Hide();
    }

    public void Hide()
    {
        if (root != null) root.gameObject.SetActive(false);
    }

    // 패널을 채우고 hoveredSlot 위에 띄운다. 화면 밖으로 나가지 않게 가로로 민다.
    public void Show(string title, string key, string body, RectTransform over, float maxWidth)
    {
        if (root == null) return;
        root.gameObject.SetActive(true);

        titleText.text = title;
        keyText.text = key;
        bodyText.text = body;

        float inner = maxWidth - pad * 2f;
        SetBox(titleText.rectTransform, inner);
        SetBox(keyText.rectTransform, inner);
        SetBox(bodyText.rectTransform, inner);

        float titleH = titleText.preferredHeight;
        float bodyH = bodyText.preferredHeight;

        // 한 줄짜리 설명이면 패널도 같이 좁아져야 한다. 늘 최대 폭이면 빈 자리가 커 보인다
        float need = Mathf.Max(titleText.preferredWidth + keyText.preferredWidth + pad, bodyText.preferredWidth);
        float w = Mathf.Min(maxWidth, need + pad * 2f);

        inner = w - pad * 2f;
        SetBox(titleText.rectTransform, inner);
        SetBox(keyText.rectTransform, inner);
        SetBox(bodyText.rectTransform, inner);
        bodyH = bodyText.preferredHeight;

        float h = pad * 2f + titleH + 4f + bodyH;
        root.sizeDelta = new Vector2(w, h);

        titleText.rectTransform.anchoredPosition = new Vector2(pad, -pad);
        keyText.rectTransform.anchoredPosition = new Vector2(pad, -pad);
        bodyText.rectTransform.anchoredPosition = new Vector2(pad, -(pad + titleH + 4f));

        PlaceAbove(over);
    }

    private void SetBox(RectTransform rt, float width)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(width, 0f);
    }

    // 칸 바로 위, 가운데 맞춤. 부모(줄) 기준 좌표로 옮긴다.
    private void PlaceAbove(RectTransform over)
    {
        if (over == null) return;

        // 칸은 피벗이 (0,0)이라 가운데는 폭의 절반만큼 오른쪽이다
        float centerX = over.anchoredPosition.x + over.sizeDelta.x * 0.5f;
        float topY = over.anchoredPosition.y + over.sizeDelta.y;

        var parent = root.parent as RectTransform;
        float half = parent != null ? parent.sizeDelta.x * 0.5f : 0f;

        // 줄 기준 좌표로 바꾼 뒤 화면 밖으로 나가지 않게 민다.
        // 화면 폭은 캔버스 기준(1920 환산)으로 재야 한다 — Screen.width를 쓰면
        // 해상도가 다를 때 엉뚱한 곳에서 잘린다.
        float x = centerX - half;

        var canvas = root.GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        float screenHalf = canvasRect != null ? canvasRect.rect.width * 0.5f : 960f;

        float limit = Mathf.Max(0f, screenHalf - root.sizeDelta.x * 0.5f - 12f);
        x = Mathf.Clamp(x, -limit, limit);

        // 칸 위에는 키캡(마우스 그림이 제일 크다)이 있으므로 그만큼 띄운다
        root.anchoredPosition = new Vector2(x, topY + GapAboveSlot);
    }
}
