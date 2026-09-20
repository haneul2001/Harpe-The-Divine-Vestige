using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [은신 세트] 그림자 발걸음 — 은신 중 이동속도 증가, 은신 중·은신 직후 치명타 피해 증가
[CreateAssetMenu(fileName = "Synergy_ShadowStep", menuName = "Harpe/Ability/Synergy/그림자 발걸음")]
public class ShadowStepEffect : AbilityEffect
{
    [SerializeField] private float moveSpeed = 0.3f;
    [Tooltip("치명타 피해 %p")]
    [SerializeField] private float critDamage = 30f;
    [Tooltip("은신이 풀린 뒤 이 시간 동안은 치명타 피해 보너스가 남는다 (은신에서 나온 첫 공격용)")]
    [SerializeField] private float afterWindow = 1f;

    private class S { public float until; }

    public override void OnStealthExit(AbilityContext ctx) { ctx.State<S>(this).until = Time.time + afterWindow; }

    public override float MoveSpeedBonus(AbilityContext ctx) { return PlayerStealth.IsHidden ? moveSpeed : 0f; }

    public override float CritDamageBonus(AbilityContext ctx)
    {
        return PlayerStealth.IsHidden || Time.time < ctx.State<S>(this).until ? critDamage : 0f;
    }
}
