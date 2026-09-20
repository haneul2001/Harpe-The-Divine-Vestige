using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [소울 세트] 영혼의 끌림 — 모든 소울 획득량 증가
[CreateAssetMenu(fileName = "Synergy_SoulGain", menuName = "Harpe/Ability/Synergy/영혼의 끌림")]
public class SoulGainEffect : AbilityEffect
{
    [SerializeField] private float bonus = 0.3f;

    public override float SoulGainBonus(AbilityContext ctx) { return bonus; }
}
