using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [처형 세트] 피의 여유 — 처형 시 체력 회복 + 잠깐 이동속도 증가
[CreateAssetMenu(fileName = "Synergy_BloodLeisure", menuName = "Harpe/Ability/Synergy/피의 여유")]
public class BloodLeisureEffect : AbilityEffect
{
    [Tooltip("최대 체력 대비 회복량")]
    [SerializeField] private float healRatio = 0.03f;
    [SerializeField] private float moveSpeed = 0.1f;
    [SerializeField] private float duration = 3f;

    private class S { public float until; }

    public override void OnHarvestImpact(AbilityContext ctx, HarvestImpact impact)
    {
        if (ctx.status != null) ctx.status.Heal(Mathf.Max(1, Mathf.RoundToInt(ctx.status.MaxHp * healRatio)));
        ctx.State<S>(this).until = Time.time + duration;
    }

    public override float MoveSpeedBonus(AbilityContext ctx) { return Time.time < ctx.State<S>(this).until ? moveSpeed : 0f; }
}
