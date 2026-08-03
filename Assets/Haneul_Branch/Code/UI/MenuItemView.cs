using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 세로 메뉴의 한 줄.
//
// 마우스와 키보드가 같은 "선택" 개념을 공유해야 한다 —
// 마우스를 올리면 그 항목이 선택되고, 키보드로 옮기면 마우스가 놓인 것과 같아진다.
// 그래서 호버를 따로 두지 않고 Selected 하나로 통일했다.
//
// 할로우 나이트/하데스처럼 선택된 항목만 밝아지며 살짝 밀려나고 표식이 붙는다.
public class MenuItemView : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    private Text label;
    private RectTransform labelRect;
    private Image marker;

    private Color idleColor;
    private Color activeColor;
    private float slide;

    private bool selected;
    private float t;

    public System.Action onSelected;   // 마우스가 올라왔을 때 (커서 동기화용)
    public System.Action onSubmit;     // 클릭/엔터

    public void Build(string text, Font font, int fontSize,
        Color idle, Color active, Color markerColor, float slideDistance)
    {
        idleColor = idle;
        activeColor = active;
        slide = slideDistance;

        // 클릭 판정을 줄 전체 영역 (투명)
        Image hit = gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);

        // 선택 표식 — 왼쪽에 세로 막대
        marker = UIFactory.Panel("Marker", transform, markerColor, false);
        UIFactory.SetAnchoredBox(marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, -14f), new Vector2(5f, 14f));
        marker.rectTransform.pivot = new Vector2(0f, 0.5f);

        label = UIFactory.Label("Label", transform, font, fontSize, idle, TextAnchor.MiddleLeft);
        labelRect = label.rectTransform;
        UIFactory.SetAnchoredBox(labelRect, new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(22f, 0f), new Vector2(0f, 0f));
        label.text = text;

        Apply(0f);
    }

    public void SetSelected(bool value)
    {
        selected = value;
    }

    public void Submit()
    {
        if (onSubmit != null) onSubmit();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (onSelected != null) onSelected();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Submit();
    }

    private void Update()
    {
        // 일시정지 중이므로 unscaled
        t = Mathf.MoveTowards(t, selected ? 1f : 0f, Time.unscaledDeltaTime / 0.08f);
        Apply(t);
    }

    private void Apply(float k)
    {
        label.color = Color.Lerp(idleColor, activeColor, k);
        labelRect.anchoredPosition = new Vector2(22f + slide * k, 0f);

        Color m = marker.color;
        marker.color = new Color(m.r, m.g, m.b, k);
        marker.rectTransform.sizeDelta = new Vector2(5f, 28f * Mathf.Max(0.2f, k));
    }
}
