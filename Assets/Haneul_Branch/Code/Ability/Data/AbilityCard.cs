using UnityEngine;

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

    [Tooltip("소속 세트. 없으면 세트 효과에 잡히지 않는다")]
    [SerializeField] private AbilitySet set;

    [Tooltip("분위기 문구 (선택). 툴팁 맨 아래에 기울임처럼 흐리게 나온다")]
    [TextArea(1, 3)]
    [SerializeField] private string flavor = "";

    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public AbilityRarity Rarity => rarity;
    public AbilitySet Set => set;
    public string Flavor => flavor;
}
