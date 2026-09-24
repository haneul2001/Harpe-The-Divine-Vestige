using UnityEngine;

// 특성 분류. 순서를 바꾸면 이미 만든 카드의 값이 어긋나므로 뒤에만 추가할 것
public enum AbilityCategory
{
    Stat = 0,       // 스탯
    Ranged = 1,     // 원거리
    Harvest = 2,    // 처형
    Kill = 3,       // 처치 / 연쇄
    Parry = 4,      // 패링
    Stealth = 5,    // 은신
    Dash = 6,       // 대시
    Soul = 7,       // 소울
    Special = 8,    // 특수
}

public static class AbilityCategoryUtil
{
    public static string Label(this AbilityCategory c)
    {
        switch (c)
        {
            case AbilityCategory.Ranged:  return "원거리";
            case AbilityCategory.Harvest: return "처형";
            case AbilityCategory.Kill:    return "처치 / 연쇄";
            case AbilityCategory.Parry:   return "패링";
            case AbilityCategory.Stealth: return "은신";
            case AbilityCategory.Dash:    return "대시";
            case AbilityCategory.Soul:    return "소울";
            case AbilityCategory.Special: return "특수";
            default:                      return "스탯";
        }
    }
}

// 능력 카드 한 장의 정의. 보상으로 획득하는 단위.
//
// 순수 데이터만 담는다. "이 카드가 실제로 무슨 일을 하는가"는
// 나중에 효과 컴포넌트/스킬을 연결해 붙일 자리를 effectHook로 열어 뒀다.
[CreateAssetMenu(fileName = "Card_", menuName = "Harpe/Ability/Card")]
public class AbilityCard : ScriptableObject
{
    [SerializeField] private string displayName = "이름 없는 카드";

    [Tooltip("툴팁에 나오는 효과 설명")]
    [TextArea(2, 5)]
    [SerializeField] private string description = "";

    [Tooltip("카드 그림. 비우면 등급 색 사각형으로 대체된다")]
    [SerializeField] private Sprite icon;

    [SerializeField] private AbilityRarity rarity = AbilityRarity.Common;

    [Tooltip("중복으로 먹었을 때 한 장당 세지는 정도(%). 0이면 등급 기본값")]
    [Min(0f)] [SerializeField] private float duplicateBonus = 0f;

    [Tooltip("특성 분류 (스탯·원거리·처형 …). 툴팁에 표시된다")]
    [SerializeField] private AbilityCategory category = AbilityCategory.Stat;

    [Tooltip("소속 세트. 없으면 세트 효과에 잡히지 않는다")]
    [SerializeField] private AbilitySet set;

    [Tooltip("분위기 문구 (선택). 툴팁 맨 아래에 기울임처럼 흐리게 나온다")]
    [TextArea(1, 3)]
    [SerializeField] private string flavor = "";

    [Tooltip("이 카드가 실제로 하는 일. 여러 개를 넣을 수 있다\n" +
             "(예: '처형 시 충격파' + '소울 획득 +2' 를 한 장에)")]
    [SerializeField] private AbilityEffect[] effects = new AbilityEffect[0];

    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public AbilityRarity Rarity => rarity;

    // 같은 카드를 또 먹었을 때 한 장당 세지는 정도(%).
    // 0이면 등급 기본값을 쓴다 — 약한 카드일수록 많이, 센 카드일수록 적게 오른다.
    public float DuplicateBonus => duplicateBonus > 0f ? duplicateBonus : rarity.DuplicateBonus();

    // 이 카드를 n장 들고 있을 때의 효과 배율 (1장 = 1.0)
    public float StackMultiplier(int copies)
    {
        if (copies <= 1) return 1f;
        return 1f + DuplicateBonus * 0.01f * (copies - 1);
    }
    public AbilityCategory Category => category;
    public AbilitySet Set => set;
    public string Flavor => flavor;
    public AbilityEffect[] Effects => effects;
}
