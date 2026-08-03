using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 카드 한 장의 뷰.
//
// 자기가 그릴 카드와 툴팁만 안다.
//   · 그리드 배치·스크롤 → AbilityPanel
//   · 등급별 프레임 장식 → CardFrameBuilder
//   · 툴팁 표시         → TooltipView
//
// 하데스/슬레이 더 스파이어처럼 마우스를 올리면 살짝 떠오르고 테두리가 밝아진다.
public class AbilityCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private AbilityCard card;
    private TooltipView tooltip;
    private CardFrameBuilder.Frame frame;

    // 카드 내용물 전체를 담는 자식.
    // 호버로 띄울 때 이 자식만 움직인다 — 루트는 GridLayoutGroup이 소유하므로
    // 루트를 직접 옮기면 레이아웃과 매 프레임 싸우다 위치가 뭉개진다.
    private RectTransform lift;

    private Text nameText;

    private Color idleBorder;
    private Color hoverBorder;
    private float hoverLift;

    private bool hovering;
    private float t;   // 0=기본, 1=올라온 상태

    private AbilityUISkin skin;

    public void Build(AbilityCard source, TooltipView sharedTooltip, Font font,
        Color panelFill, float lift, AbilityUISkin uiSkin)
    {
        card = source;
        tooltip = sharedTooltip;
        hoverLift = lift;
        skin = uiSkin;

        Color rarity = card.Rarity.Color();
        idleBorder = new Color(rarity.r, rarity.g, rarity.b, 0.72f);
        hoverBorder = Color.Lerp(rarity, Color.white, 0.35f);

        this.lift = UIFactory.Stretch("Lift", transform);
        frame = CardFrameBuilder.Build(this.lift, card.Rarity, panelFill, skin);
        frame.border.color = idleBorder;

        BuildIcon(rarity, font);
        BuildName(rarity, font);
    }

    private void BuildIcon(Color rarity, Font font)
    {
        RectTransform iconRect = UIFactory.Empty("Icon", frame.content);
        UIFactory.SetAnchoredBox(iconRect, new Vector2(0f, 0.26f), new Vector2(1f, 1f),
            new Vector2(20f, 6f), new Vector2(-20f, -22f));

        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;

        if (card.Icon != null)
        {
            icon.sprite = card.Icon;
            icon.color = Color.white;
            return;
        }

        // 그림이 없을 때 — 등급 색 마름모를 세워 빈 칸처럼 보이지 않게 한다
        icon.color = new Color(rarity.r, rarity.g, rarity.b, 0.16f);
        iconRect.localRotation = Quaternion.Euler(0f, 0f, 45f);

        Text mark = UIFactory.Label("Mark", frame.content, font, 46,
            new Color(rarity.r, rarity.g, rarity.b, 0.55f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(mark.rectTransform, new Vector2(0f, 0.26f), new Vector2(1f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, -16f));
        mark.text = card.DisplayName.Length > 0 ? card.DisplayName.Substring(0, 1) : "?";
    }

    private void BuildName(Color rarity, Font font)
    {
        // 이름 뒤에 어두운 띠를 깔아 그림 위에서도 읽히게 한다
        Image plate = UIFactory.Panel("NamePlate", frame.content, new Color(0f, 0f, 0f, 0.45f), false);
        UIFactory.ApplySprite(plate, skin.NamePlate());
        UIFactory.SetAnchoredBox(plate.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.26f),
            Vector2.zero, Vector2.zero);

        nameText = UIFactory.Label("Name", frame.content, font, 21, rarity, TextAnchor.MiddleCenter);
        UIFactory.SetAnchoredBox(nameText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.26f),
            new Vector2(8f, 4f), new Vector2(-8f, -2f));
        nameText.verticalOverflow = VerticalWrapMode.Truncate;
        nameText.text = card.DisplayName;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        if (tooltip != null)
            tooltip.Show(card.DisplayName, card.Rarity.Color(), BuildBody(), card.Flavor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        if (tooltip != null) tooltip.Hide();
    }

    private string BuildBody()
    {
        string setLine = card.Set != null
            ? "\n<color=#8FD6A0>세트 · " + card.Set.DisplayName + "</color>"
            : "";

        return "<color=#9AA0AE>" + card.Rarity.Label() + "</color>\n" + card.Description + setLine;
    }

    private void Update()
    {
        // 시간정지 중에도 반응해야 하므로 unscaled
        float target = hovering ? 1f : 0f;
        t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime / 0.09f);

        // 늘어난(stretch) 사각형이라 offset 양쪽을 같이 밀어야 명확하게 움직인다
        float y = hoverLift * t;
        lift.offsetMin = new Vector2(0f, y);
        lift.offsetMax = new Vector2(0f, y);

        frame.border.color = Color.Lerp(idleBorder, hoverBorder, t);

        UpdateGlow();
    }

    // 후광은 호버에 반응하고, 전설 등급은 가만히 둬도 은은하게 맥동한다
    private void UpdateGlow()
    {
        if (frame.glow.Count == 0) return;

        float pulse = frame.shimmer > 0f
            ? 1f + frame.shimmer * 0.35f * Mathf.Sin(Time.unscaledTime * 2.4f)
            : 1f;

        float boost = (1f + t * 1.1f) * pulse;

        for (int i = 0; i < frame.glow.Count; i++)
        {
            Image g = frame.glow[i];
            Color c = g.color;
            // 조립 시점의 알파를 기준으로 곱한다. 누적되지 않게 원본을 따로 안 들고
            // 레이어 순서로 역산한다 (0번이 가장 바깥 = 가장 옅음).
            g.color = new Color(c.r, c.g, c.b, BaseGlowAlpha(i) * boost);
        }
    }

    private float BaseGlowAlpha(int index)
    {
        RarityStyle style = card.Rarity.Style();
        int layer = frame.glow.Count - index;   // 바깥(0)일수록 layer 값이 크다
        return style.glowAlpha / Mathf.Max(1, layer);
    }
}
