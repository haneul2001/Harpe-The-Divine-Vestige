using UnityEngine;

// 세트 정의. 같은 세트 카드를 몇 장 모으면 어떤 효과가 붙는지를 담는다.
//
// 효과 "적용"은 여기서 하지 않는다 — 이 에셋은 순수 데이터이고,
// 실제 스탯 반영은 나중에 AbilitySetCalculator 결과를 읽는 쪽에서 처리한다.
// (데이터와 적용 로직을 붙여 두면 UI에서 미리보기를 못 만든다)
[CreateAssetMenu(fileName = "Set_", menuName = "Harpe/Ability/Set")]
public class AbilitySet : ScriptableObject
{
    // 몇 장을 모았을 때 어떤 효과가 열리는지
    [System.Serializable]
    public class Tier
    {
        [Tooltip("이 효과가 열리는 데 필요한 카드 수")]
        [Min(1)] public int required = 2;

        [Tooltip("효과 설명. 툴팁에 그대로 나온다")]
        [TextArea(1, 3)] public string effect = "";
    }

    [SerializeField] private string displayName = "이름 없는 세트";
    [SerializeField] private Sprite icon;
    [Tooltip("세트의 분위기 문구. 없으면 툴팁에서 생략된다")]
    [TextArea(1, 3)]
    [SerializeField] private string flavor = "";

    [Tooltip("필요 장수 오름차순으로 넣을 것")]
    [SerializeField] private Tier[] tiers = new Tier[0];

    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public string Flavor => flavor;
    public Tier[] Tiers => tiers;

    // 지금 장수로 열려 있는 단계 수
    public int ActiveTierCount(int owned)
    {
        int n = 0;
        for (int i = 0; i < tiers.Length; i++)
            if (tiers[i] != null && owned >= tiers[i].required) n++;
        return n;
    }

    // 다음으로 열릴 단계의 필요 장수. 전부 열렸으면 -1
    public int NextRequirement(int owned)
    {
        for (int i = 0; i < tiers.Length; i++)
            if (tiers[i] != null && owned < tiers[i].required) return tiers[i].required;
        return -1;
    }
}
