using System.Collections.Generic;
using UnityEngine;

// [스탯] 상시 능력치 보너스. 강철 피부 / 질풍 / 날카로운 감각이 이 한 종류를 수치만 바꿔 쓴다.
[CreateAssetMenu(fileName = "Effect_StatBonus", menuName = "Harpe/Ability/Effect/능력치 보너스")]
public class StatBonusEffect : AbilityEffect
{
    [SerializeField] private int maxHp = 0;
    [SerializeField] private int defense = 0;
    [Tooltip("0.15 = 공격속도 +15%")]
    [SerializeField] private float attackSpeed = 0f;
    [Tooltip("0.15 = 이동속도 +15%")]
    [SerializeField] private float moveSpeed = 0f;
    [Tooltip("치명타 확률 %p")]
    [SerializeField] private float critRate = 0f;
    [Tooltip("치명타 피해 %p (150 → 180)")]
    [SerializeField] private float critDamage = 0f;

    // 스탯 세트 시너지(스탯 증폭)가 수치를 키운다
    private static float Amp { get { return AbilityHooks.StatAmplify(); } }

    public override float MaxHpBonus(AbilityContext ctx) { return maxHp * Amp; }
    public override float DefenseBonus(AbilityContext ctx) { return defense * Amp; }
    public override float AttackSpeedBonus(AbilityContext ctx) { return attackSpeed * Amp; }
    public override float MoveSpeedBonus(AbilityContext ctx) { return moveSpeed * Amp; }
    public override float CritRateBonus(AbilityContext ctx) { return critRate * Amp; }
    public override float CritDamageBonus(AbilityContext ctx) { return critDamage * Amp; }
}
