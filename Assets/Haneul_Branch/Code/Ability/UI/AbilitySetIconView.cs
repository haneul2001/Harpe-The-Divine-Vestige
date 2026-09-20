using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 왼쪽 칸에 세로로 쌓이는 세트 아이콘 하나.
//
// 디아블로/POE의 세트 표기 방식을 따랐다 —
// 열린 단계는 밝은 초록, 아직 못 연 단계는 회색으로 같이 보여 준다.
// "앞으로 몇 장 더 모으면 뭐가 열리는지"가 보여야 카드를 고를 때 판단이 선다.
public class AbilitySetIconView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private AbilitySetProgress progress;
    private TooltipView tooltip;

    private Image border;
    private Image iconImage;
    private Text countText;

    private static readonly Color ActiveColor   = new Color(0.56f, 0.84f, 0.63f);
    private static readonly Color InactiveColor = new Color(0.42f, 0.44f, 0.50f);

    public void Build(AbilitySetProgress source, TooltipView sharedTooltip, Font font,
        Color panelFill, AbilityUISkin skin)
    {
        progress = source;
        tooltip = sharedTooltip;

        Color accent = progress.IsActive ? ActiveColor : InactiveColor;

        border = gameObject.AddComponent<Image>();
        Sprite slotSprite = skin.SetSlotBackground();
        UIFactory.ApplySprite(border, slotSprite);

        if (slotSprite != null)
        {
            // 픽셀 슬롯 그림은 원래 색 그대로 — 못 연 세트만 어둡게
            border.pixelsPerUnitMultiplier = 1f / Mathf.Max(skin.PixelScale(), 0.01f);
            border.color = progress.IsActive ? Color.white : new Color(0.6f, 0.6f, 0.65f, 1f);
        }
        else
        {
            border.color = new Color(accent.r, accent.g, accent.b, progress.IsActive ? 0.9f : 0.4f);
        }

        RectTransform inner = UIFactory.Stretch("Fill", transform);
        inner.offsetMin = Vector2.one * 2f;
        inner.offsetMax = -Vector2.one * 2f;
        Image fill = inner.gameObject.AddComponent<Image>();
        fill.color = panelFill;
        fill.raycastTarget = false;
        // 슬롯 그림이 있으면 안쪽 단색 판은 필요 없다
        fill.enabled = slotSprite == null;

        // 22px 아이콘을 2배(44)로 — 64 슬롯에서 사방 10씩
        RectTransform iconRect = UIFactory.Stretch("Icon", inner);
        iconRect.offsetMin = Vector2.one * 8f;
        iconRect.offsetMax = -Vector2.one * 8f;
        iconImage = iconRect.gameObject.AddComponent<Image>();
        iconImage.raycastTarget = false;
        iconImage.preserveAspect = true;

        if (progress.set.Icon != null)
        {
            // 슬롯 안에 들어가는 가장 큰 정수배로 가운데 정렬 — 늘려 채우면 픽셀이 뭉개진다
            Sprite s = progress.set.Icon;
            float slot = ((RectTransform)transform).sizeDelta.x;
            float room = Mathf.Max(1f, slot - 12f);   // 슬롯 테두리 안쪽
            float spriteSize = Mathf.Max(s.rect.width, s.rect.height);
            float k = spriteSize <= room ? Mathf.Floor(room / spriteSize) : room / spriteSize;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(s.rect.width, s.rect.height) * k;
            iconRect.anchoredPosition = Vector2.zero;

            iconImage.sprite = progress.set.Icon;
            iconImage.color = progress.IsActive ? Color.white : new Color(0.6f, 0.6f, 0.6f);
        }
        else
        {
            // 아이콘 그림이 아직 없으면 세트 이름 첫 글자로 구분한다
            iconImage.enabled = false;
            Text mark = UIFactory.Label("Mark", inner, font, 26, accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchoredBox(mark.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 4f), Vector2.zero);
            string setName = progress.set.DisplayName;
            mark.text = string.IsNullOrEmpty(setName) ? "?" : setName.Substring(0, 1);
        }

        // 우하단에 보유 장수 배지
        RectTransform countRect = UIFactory.Empty("Count", inner);
        UIFactory.SetAnchoredBox(countRect, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 7f), new Vector2(-10f, 27f));   // 슬롯 테두리(7px×2) 안쪽
        countText = countRect.gameObject.AddComponent<Text>();
        UIFactory.ApplyPixelFont(countText, font, 12, FontStyle.Bold);
        countText.alignment = TextAnchor.LowerRight;
        countText.color = accent;
        countText.raycastTarget = false;
        countText.text = progress.owned.ToString();
        var sh = countRect.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
        sh.effectDistance = new Vector2(1f, -1f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltip == null) return;

        Color titleColor = progress.IsActive ? ActiveColor : InactiveColor;
        tooltip.Show(progress.set.DisplayName, titleColor, BuildBody(), progress.set.Flavor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.Hide();
    }

    private string BuildBody()
    {
        var sb = new StringBuilder();
        sb.Append("<color=#9AA0AE>보유 ").Append(progress.owned).Append("장</color>\n");

        AbilitySet.Tier[] tiers = progress.set.Tiers;
        for (int i = 0; i < tiers.Length; i++)
        {
            AbilitySet.Tier tier = tiers[i];
            if (tier == null) continue;

            bool open = progress.owned >= tier.required;
            string color = open ? "#8FD6A0" : "#6B6E78";

            sb.Append("\n<color=").Append(color).Append(">(")
              .Append(tier.required).Append("장) ");
            if (!string.IsNullOrEmpty(tier.title)) sb.Append(tier.title).Append(" — ");
            sb.Append(tier.effect).Append("</color>");
        }

        if (progress.nextRequirement > 0)
        {
            int need = progress.nextRequirement - progress.owned;
            sb.Append("\n\n<color=#9AA0AE>다음 효과까지 ").Append(need).Append("장</color>");
        }

        return sb.ToString();
    }
}
