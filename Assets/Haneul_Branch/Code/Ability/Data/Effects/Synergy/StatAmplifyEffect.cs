using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [스탯 세트] 스탯 증폭 — 스탯 카드 수치를 키운다 (+ 이동속도)
// 단계는 누적되므로 II 단계는 "추가분"만 넣는다 (I 0.15 + II 0.15 = 30%)
[CreateAssetMenu(fileName = "Synergy_StatAmplify", menuName = "Harpe/Ability/Synergy/스탯 증폭")]
public class StatAmplifyEffect : AbilityEffect
{
    [SerializeField] private float amplify = 0.15f;
    [SerializeField] private float moveSpeed = 0f;

    public override float StatAmplify(AbilityContext ctx) { return amplify; }
    public override float MoveSpeedBonus(AbilityContext ctx) { return moveSpeed; }
}
