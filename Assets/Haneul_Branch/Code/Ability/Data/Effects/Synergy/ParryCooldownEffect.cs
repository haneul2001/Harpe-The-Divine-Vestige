using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [패링 세트] 철벽 — 패링 쿨타임 단축
[CreateAssetMenu(fileName = "Synergy_ParryCooldown", menuName = "Harpe/Ability/Synergy/철벽")]
public class ParryCooldownEffect : AbilityEffect
{
    [Tooltip("줄일 시간(초). 1.5초 → 1.0초면 0.5")]
    [SerializeField] private float reduction = 0.5f;

    public override float SkillCooldownReduction(AbilityContext ctx, PlayerSkill skill)
    {
        return skill is Parry ? reduction : 0f;
    }
}
