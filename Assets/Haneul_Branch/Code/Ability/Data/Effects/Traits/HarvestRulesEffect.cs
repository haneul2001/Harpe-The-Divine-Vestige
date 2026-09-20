using System.Collections.Generic;
using UnityEngine;

// [처형] 영혼 수확(획득 소울 증가) / 사형집행인의 눈(처형 가능 체력 증가)
[CreateAssetMenu(fileName = "Effect_HarvestRules", menuName = "Harpe/Ability/Effect/처형 규칙")]
public class HarvestRulesEffect : AbilityEffect
{
    [Tooltip("처형 시 얻는 소울 +% (0.5 = +50%)")]
    [SerializeField] private float soulBonus = 0f;
    [Tooltip("처형 가능 체력 비율 +%p (0.1 = 30% → 40%)")]
    [SerializeField] private float thresholdBonus = 0f;

    public override float HarvestSoulBonus(AbilityContext ctx) { return soulBonus; }
    public override float HarvestThresholdBonus(AbilityContext ctx) { return thresholdBonus; }
}
