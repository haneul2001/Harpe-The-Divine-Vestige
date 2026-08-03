using UnityEngine;

// 카드 등급.
// 순서를 바꾸면 이미 만들어 둔 에셋의 직렬화 값이 어긋나므로 뒤에만 추가할 것.
public enum AbilityRarity
{
    Common = 0,
    Rare = 1,
    Epic = 2,
    Legendary = 3,
}

// 등급 색은 여기 한 곳에서만 정한다.
// 카드 테두리 / 툴팁 제목 / 세트 아이콘이 전부 이걸 참조하므로 색을 바꾸려면 여기만 고치면 된다.
public static class AbilityRarityUtil
{
    public static Color Color(this AbilityRarity rarity)
    {
        switch (rarity)
        {
            case AbilityRarity.Rare:      return new Color(0.36f, 0.66f, 0.96f);
            case AbilityRarity.Epic:      return new Color(0.72f, 0.42f, 0.94f);
            case AbilityRarity.Legendary: return new Color(0.98f, 0.72f, 0.24f);
            default:                      return new Color(0.78f, 0.80f, 0.84f);
        }
    }

    public static string Label(this AbilityRarity rarity)
    {
        switch (rarity)
        {
            case AbilityRarity.Rare:      return "레어";
            case AbilityRarity.Epic:      return "에픽";
            case AbilityRarity.Legendary: return "전설";
            default:                      return "일반";
        }
    }

    // 등급이 올라갈수록 프레임이 화려해지는 정도.
    // 카드 그림이 없어도 등급이 한눈에 구분되도록 테두리·후광·모서리 장식을 단계적으로 준다.
    public static RarityStyle Style(this AbilityRarity rarity)
    {
        switch (rarity)
        {
            case AbilityRarity.Rare:
                return new RarityStyle(3f, 0.16f, 2, true, 0f, 0.10f);
            case AbilityRarity.Epic:
                return new RarityStyle(4f, 0.24f, 3, true, 0f, 0.16f);
            case AbilityRarity.Legendary:
                return new RarityStyle(5f, 0.34f, 3, true, 1f, 0.22f);
            default:
                return new RarityStyle(2f, 0f, 0, false, 0f, 0.05f);
        }
    }
}

// 등급별 프레임 표현 값. CardFrameBuilder가 이걸 그대로 읽어 조립한다.
public struct RarityStyle
{
    public float borderWidth;   // 테두리 두께
    public float glowAlpha;     // 후광 시작 알파 (0이면 후광 없음)
    public int glowLayers;      // 후광을 몇 겹 깔지
    public bool corners;        // 네 모서리 장식
    public float shimmer;       // 반짝임 세기 (0이면 고정)
    public float tint;          // 카드 안쪽 등급색 그라데이션 진하기

    public RarityStyle(float borderWidth, float glowAlpha, int glowLayers,
        bool corners, float shimmer, float tint)
    {
        this.borderWidth = borderWidth;
        this.glowAlpha = glowAlpha;
        this.glowLayers = glowLayers;
        this.corners = corners;
        this.shimmer = shimmer;
        this.tint = tint;
    }
}
