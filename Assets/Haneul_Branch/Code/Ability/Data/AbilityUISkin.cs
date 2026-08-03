using UnityEngine;
using UnityEngine.UI;

// 능력 UI의 그림 묶음.
//
// 전부 비워 두면 지금처럼 단색 사각형으로 그려진다. 그림이 준비되는 대로
// 여기에 하나씩 꽂으면 코드를 건드리지 않고 그 부분만 바뀐다.
//
// 그림이 없을 때의 모습도 "임시방편"이 아니라 정식 폴백으로 취급한다 —
// 아트가 늦어져도 게임은 계속 돌아가야 하고, 등급 구분도 유지돼야 한다.
[CreateAssetMenu(fileName = "AbilityUISkin", menuName = "Harpe/Ability/UI Skin")]
public class AbilityUISkin : ScriptableObject
{
    [Header("카드")]
    [Tooltip("카드 안쪽 바탕")]
    public Sprite cardBackground;
    [Tooltip("카드 테두리. 등급별 프레임이 있으면 그쪽이 우선한다")]
    public Sprite cardFrame;
    [Tooltip("카드 뒤 후광. 가장자리가 부드러운 그림이 어울린다")]
    public Sprite cardGlow;
    [Tooltip("카드 상단 등급 띠")]
    public Sprite rarityStrip;
    [Tooltip("이름 뒤에 깔리는 띠")]
    public Sprite namePlate;

    [Header("등급별 프레임 (Common/Rare/Epic/Legendary 순서)")]
    [Tooltip("채워 두면 해당 등급은 cardFrame 대신 이걸 쓴다. 일부만 채워도 된다")]
    public Sprite[] frameByRarity = new Sprite[0];

    [Header("패널")]
    public Sprite panelBackground;
    public Sprite panelBorder;
    public Sprite tooltipBackground;
    public Sprite setSlotBackground;
    public Sprite setColumnBackground;

    // 등급에 맞는 프레임. 없으면 공용 프레임, 그것도 없으면 null(=단색 폴백)
    public Sprite FrameFor(AbilityRarity rarity)
    {
        int i = (int)rarity;
        if (frameByRarity != null && i >= 0 && i < frameByRarity.Length && frameByRarity[i] != null)
            return frameByRarity[i];

        return cardFrame;
    }
}

// 스킨이 null이어도 호출부가 조건문으로 지저분해지지 않게 하는 확장.
// skin?.cardFrame 식으로 흩어 놓으면 폴백 규칙이 여러 곳에 복제된다.
public static class AbilityUISkinUtil
{
    public static Sprite CardBackground(this AbilityUISkin skin) => skin != null ? skin.cardBackground : null;
    public static Sprite CardGlow(this AbilityUISkin skin) => skin != null ? skin.cardGlow : null;
    public static Sprite RarityStrip(this AbilityUISkin skin) => skin != null ? skin.rarityStrip : null;
    public static Sprite NamePlate(this AbilityUISkin skin) => skin != null ? skin.namePlate : null;
    public static Sprite PanelBackground(this AbilityUISkin skin) => skin != null ? skin.panelBackground : null;
    public static Sprite PanelBorder(this AbilityUISkin skin) => skin != null ? skin.panelBorder : null;
    public static Sprite TooltipBackground(this AbilityUISkin skin) => skin != null ? skin.tooltipBackground : null;
    public static Sprite SetSlotBackground(this AbilityUISkin skin) => skin != null ? skin.setSlotBackground : null;
    public static Sprite SetColumnBackground(this AbilityUISkin skin) => skin != null ? skin.setColumnBackground : null;

    public static Sprite Frame(this AbilityUISkin skin, AbilityRarity rarity)
        => skin != null ? skin.FrameFor(rarity) : null;
}
