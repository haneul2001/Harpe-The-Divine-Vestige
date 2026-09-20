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

        // 카드 그림은 이미 등급 색으로 칠해져 있다 — 등급 색을 곱하거나 반투명으로 두면 탁해진다
        if (frame.pictureFrame)
        {
            idleBorder = Color.white;
            hoverBorder = new Color(1f, 1f, 0.92f, 1f);
        }
        frame.border.color = idleBorder;

        BuildIcon(rarity, font);
        BuildName(rarity, font);
    }

    // 픽셀 카드 그림(100x155)의 아치 창 · 양피지 이름칸 위치를 비율로 잰 값
    private static readonly Vector2 PicIconMin = new Vector2(0.22f, 0.42f);
    private static readonly Vector2 PicIconMax = new Vector2(0.78f, 0.86f);
    private static readonly Vector2 PicNameMin = new Vector2(0.14f, 0.08f);
    private static readonly Vector2 PicNameMax = new Vector2(0.86f, 0.29f);
    private const float PicIconScale = 4f;   // 22px → 88 (카드 200x310 기준 아치 창 안쪽)

    private void BuildIcon(Color rarity, Font font)
    {
        if (frame.pictureFrame)
        {
            BuildPictureIcon(rarity, font);
            return;
        }

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

    // 카드 그림 모드: 아치 창 안에 아이콘, 없으면 첫 글자만 (마름모 장식은 그림과 겹쳐 생략)
    private void BuildPictureIcon(Color rarity, Font font)
    {
        if (card.Icon != null)
        {
            // 아치 창 가운데에 아이콘 원본(22px)의 정수배로 — 창을 꽉 채우면 아치 장식을 덮는다
            Image icon = UIFactory.Panel("Icon", frame.content, Color.white, false);
            icon.sprite = card.Icon;
            icon.preserveAspect = true;
            Vector2 center = (PicIconMin + PicIconMax) * 0.5f;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = center;
            // 아치 창 크기(22px × 4 = 88)에 들어가는 가장 큰 정수배 — 22px 타일은 ×4, 44px AI 아이콘은 ×2
            float target = 22f * PicIconScale;
            float spriteSize = Mathf.Max(card.Icon.rect.width, card.Icon.rect.height);
            float k = spriteSize <= target ? Mathf.Floor(target / spriteSize) : target / spriteSize;
            icon.rectTransform.sizeDelta = new Vector2(card.Icon.rect.width, card.Icon.rect.height) * k;
            icon.rectTransform.anchoredPosition = Vector2.zero;
            return;
        }

        Text mark = UIFactory.Label("Mark", frame.content, font, 44,
            Color.Lerp(rarity, Color.white, 0.25f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(mark.rectTransform, PicIconMin, PicIconMax, Vector2.zero, Vector2.zero);
        mark.text = card.DisplayName.Length > 0 ? card.DisplayName.Substring(0, 1) : "?";
        var sh = mark.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
        sh.effectDistance = new Vector2(2f, -2f);
    }

    private void BuildName(Color rarity, Font font)
    {
        if (frame.pictureFrame)
        {
            // 이름칸이 밝은 양피지라 어두운 갈색 글씨여야 읽힌다
            // 갈무리 12px — 24px면 다섯 글자 이름이 양피지 가장자리에 닿는다
            nameText = UIFactory.Label("Name", frame.content, font, 12,
                new Color(0.24f, 0.16f, 0.10f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchoredBox(nameText.rectTransform, PicNameMin, PicNameMax, Vector2.zero, Vector2.zero);
            nameText.verticalOverflow = VerticalWrapMode.Truncate;
            nameText.text = card.DisplayName;
            return;
        }

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

        return "<color=#9AA0AE>" + card.Rarity.Label() + " · " + card.Category.Label() + "</color>\n" + card.Description + setLine;
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
