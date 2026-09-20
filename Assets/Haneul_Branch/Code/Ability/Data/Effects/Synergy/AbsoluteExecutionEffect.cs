using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [처형 세트] 절대 집행 — 처형 진행 중 무적 (HarvestManager가 물어본다)
[CreateAssetMenu(fileName = "Synergy_AbsoluteExecution", menuName = "Harpe/Ability/Synergy/절대 집행")]
public class AbsoluteExecutionEffect : AbilityEffect
{
    public override bool GrantsHarvestInvincibility(AbilityContext ctx) { return true; }
}
