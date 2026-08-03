using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 카드 프레임(후광·테두리·등급 띠·그라데이션·모서리 장식)만 조립한다.
//
// AbilityCardView에서 떼어낸 이유:
//  · 뷰는 "무슨 카드를 보여줄지"에 집중하고, 프레임은 "등급을 어떻게 꾸밀지"만 안다
//  · 나중에 보상 선택 화면에서도 같은 프레임을 그대로 재사용할 수 있다
//
// 그림 에셋 없이 사각형 조합만으로 등급을 구분한다.
public static class CardFrameBuilder
{
    // 조립 결과. 뷰가 호버/반짝임에 쓰는 것만 넘긴다.
    public class Frame
    {
        public Image border;
        public RectTransform content;     // 아이콘·이름은 여기에 붙인다
        public List<Image> glow = new List<Image>();
        public List<Image> accents = new List<Image>();
        public float shimmer;
    }

    // skin이 null이면 전부 단색 사각형으로 그린다 (그림이 없어도 등급 구분은 유지된다).
    public static Frame Build(RectTransform root, AbilityRarity rarity, Color fillColor, AbilityUISkin skin)
    {
        Color color = rarity.Color();
        RarityStyle style = rarity.Style();

        var frame = new Frame { shimmer = style.shimmer };

        // ── 후광 — 카드보다 큰 사각형을 겹쳐 깐다 ──────────────
        // 자식은 나중에 추가될수록 위에 그려지므로, 큰 것부터 먼저 만들어야 뒤로 간다.
        for (int i = style.glowLayers; i >= 1; i--)
        {
            float pad = 5f * i;
            float alpha = style.glowAlpha / i;

            Image g = UIFactory.Panel("Glow" + i, root,
                new Color(color.r, color.g, color.b, alpha), false);
            UIFactory.ApplySprite(g, skin.CardGlow());
            StretchWithPadding(g.rectTransform, pad);
            frame.glow.Add(g);
        }

        // ── 테두리 ────────────────────────────────────────────
        Sprite frameSprite = skin.Frame(rarity);

        Image border = UIFactory.Panel("Border", root, color, true);
        UIFactory.ApplySprite(border, frameSprite);
        StretchWithPadding(border.rectTransform, 0f);
        frame.border = border;

        // 프레임 그림에는 이미 테두리가 그려져 있으므로 안쪽을 깎지 않는다.
        // 단색 폴백일 때만 borderWidth만큼 줄여 테두리처럼 보이게 한다.
        float inset = frameSprite != null ? 0f : style.borderWidth;

        // ── 본체 ──────────────────────────────────────────────
        RectTransform fill = UIFactory.Stretch("Fill", border.transform);
        fill.offsetMin = Vector2.one * inset;
        fill.offsetMax = -Vector2.one * inset;

        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;
        UIFactory.ApplySprite(fillImage, skin.CardBackground());

        // 프레임 그림이 들어오면 직접 그린 장식은 겹쳐서 지저분해진다
        bool decorate = frameSprite == null;

        // 안쪽 등급 색 그라데이션 — 위에서 아래로 옅어진다
        if (decorate && style.tint > 0f)
        {
            Image tint = UIFactory.Panel("Tint", fill,
                new Color(color.r, color.g, color.b, style.tint), false);
            tint.sprite = UIFactory.VerticalGradient();
            StretchWithPadding(tint.rectTransform, 0f);
        }

        // 상단 등급 띠 — 카드 게임에서 흔한 등급 표기
        if (decorate || skin.RarityStrip() != null)
        {
            Image strip = UIFactory.Panel("RarityStrip", fill, color, false);
            UIFactory.ApplySprite(strip, skin.RarityStrip());
            UIFactory.SetAnchoredBox(strip.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -5f), new Vector2(0f, 0f));
        }

        // ── 모서리 장식 ───────────────────────────────────────
        if (decorate && style.corners)
        {
            const float len = 16f;
            const float thick = 3f;

            // (앵커, 가로막대 방향, 세로막대 방향)
            AddCorner(fill, color, new Vector2(0f, 1f), new Vector2(len, thick), new Vector2(thick, len),  1f, -1f, frame);
            AddCorner(fill, color, new Vector2(1f, 1f), new Vector2(len, thick), new Vector2(thick, len), -1f, -1f, frame);
            AddCorner(fill, color, new Vector2(0f, 0f), new Vector2(len, thick), new Vector2(thick, len),  1f,  1f, frame);
            AddCorner(fill, color, new Vector2(1f, 0f), new Vector2(len, thick), new Vector2(thick, len), -1f,  1f, frame);
        }

        frame.content = fill;
        return frame;
    }

    // ㄱ자 모양 — 가로 막대 + 세로 막대
    private static void AddCorner(RectTransform parent, Color color, Vector2 anchor,
        Vector2 hSize, Vector2 vSize, float dirX, float dirY, Frame frame)
    {
        const float inset = 5f;

        Image h = UIFactory.Panel("CornerH", parent, color, false);
        h.rectTransform.anchorMin = h.rectTransform.anchorMax = anchor;
        h.rectTransform.pivot = new Vector2(dirX > 0 ? 0f : 1f, dirY > 0 ? 0f : 1f);
        h.rectTransform.sizeDelta = hSize;
        h.rectTransform.anchoredPosition = new Vector2(inset * dirX, inset * dirY);
        frame.accents.Add(h);

        Image v = UIFactory.Panel("CornerV", parent, color, false);
        v.rectTransform.anchorMin = v.rectTransform.anchorMax = anchor;
        v.rectTransform.pivot = new Vector2(dirX > 0 ? 0f : 1f, dirY > 0 ? 0f : 1f);
        v.rectTransform.sizeDelta = vSize;
        v.rectTransform.anchoredPosition = new Vector2(inset * dirX, inset * dirY);
        frame.accents.Add(v);
    }

    private static void StretchWithPadding(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-pad, -pad);
        rt.offsetMax = new Vector2(pad, pad);
    }
}
