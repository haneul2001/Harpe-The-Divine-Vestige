using UnityEngine;

// 패링: 발동 후 짧은 창 동안 들어오는 피해를 타이밍에 따라 무효/감소.
[CreateAssetMenu(fileName = "Parry", menuName = "PlayerSkill/Parry")]
public class Parry : PlayerSkill
{
    [Header("패링 타이밍")]
    [Tooltip("패링 유효 시간(초) — 이 안에 맞으면 감소")]
    [SerializeField] private float parryWindow = 0.5f;
    [Tooltip("퍼펙트 패링 시간(초) — 발동 직후 이 안에 맞으면 완전 무효")]
    [SerializeField] private float perfectWindow = 0.15f;

    [Header("피해 배율")]
    [Tooltip("퍼펙트 패링 시 받는 피해 배율 (0 = 완전 무효)")]
    [Range(0f, 1f)][SerializeField] private float perfectDamageMultiplier = 0f;
    [Tooltip("일반 패링 시 받는 피해 배율 (0.5 = 50% 감소)")]
    [Range(0f, 1f)][SerializeField] private float blockedDamageMultiplier = 0.5f;

    [Header("퍼펙트 패링 보상")]
    [Tooltip("반사 데미지 배율 (막을 뻔한 피해 × 이 값을 공격자에게). 0이면 반사 없음.")]
    [SerializeField] private float reflectMultiplier = 2f;
    [Tooltip("퍼펙트 패링 후 짧은 무적 시간(초)")]
    [SerializeField] private float iframeDuration = 0.4f;

    public override void Activate(SkillContext ctx)
    {
        if (!TrySpendCost(ctx)) return;

        // TODO: 패링 애니메이션 / 이펙트
        Debug.Log($"[{skillName}] 패링 (perfect {perfectWindow}s / window {parryWindow}s)");

        ctx.Status.BeginParry(perfectWindow, parryWindow,
            perfectDamageMultiplier, blockedDamageMultiplier,
            reflectMultiplier, iframeDuration);
    }
}
