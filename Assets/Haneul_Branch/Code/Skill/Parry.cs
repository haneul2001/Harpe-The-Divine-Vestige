using UnityEngine;

// 패링: 발동 후 짧은 창 동안 들어오는 피해를 타이밍에 따라 무효/감소.
[CreateAssetMenu(fileName = "Parry", menuName = "PlayerSkill/Parry")]
public class Parry : PlayerSkill
{
    [Header("패링 타이밍")]
    [Tooltip("패링 유효 시간(초) — 이 안에 맞으면 감소")]
    [SerializeField] private float parryWindow = 1.0f;
    [Tooltip("퍼펙트 패링 시간(초) — 발동 직후 이 안에 맞으면 완전 무효.\n" +
             "방패 이펙트(VFX_2D_Shield_01_Color, 실제로 보이는 시간 1.0초)가 떠 있는 동안이 곧 판정 구간이다.\n" +
             "parryWindow와 같이 두면 감소 구간 없이 전부 퍼펙트로 처리된다.")]
    [SerializeField] private float perfectWindow = 1.0f;

    [Header("연출 · 조작")]
    [Tooltip("발동하면 캐릭터에 붙여 띄울 VfxLibrary 이펙트 id")]
    [SerializeField] private string shieldVfxId = "ParryShield";
    [Tooltip("방패 중심의 발 기준 위치 (오른쪽을 볼 때 — 왼쪽을 보면 x가 뒤집힌다).\n" +
             "Little Reaper 몸통 중심은 피벗에서 옆으로 4.5px(≈0.14), 위로 9px(≈0.28)이다")]
    [SerializeField] private Vector2 shieldOffset = new Vector2(0.14f, 0.28f);
    [Tooltip("방패 애니메이션(= 판정 시간 perfectWindow)이 끝난 뒤 추가로 조작을 막는 시간(초)")]
    [SerializeField] private float controlLockDuration = 0.5f;

    [Header("피해 배율")]
    [Tooltip("퍼펙트 패링 시 받는 피해 배율 (0 = 완전 무효)")]
    [Range(0f, 1f)][SerializeField] private float perfectDamageMultiplier = 0f;
    [Tooltip("일반 패링 시 받는 피해 배율 (0.5 = 50% 감소)")]
    [Range(0f, 1f)][SerializeField] private float blockedDamageMultiplier = 0.5f;

    [Header("퍼펙트 패링 보상")]
    [Tooltip("반격 데미지 배율 (내 평타 굴림 × 이 값을 공격자에게). 0이면 반격 없이 경직만.")]
    [SerializeField] private float counterDamageMultiplier = 1.5f;
    [Tooltip("퍼펙트 패링 후 짧은 무적 시간(초)")]
    [SerializeField] private float iframeDuration = 0.4f;

    // 연타 방지: 판정 시간(방패)이 떠 있거나, 조작 불가 구간이거나, 반격 애니 중이면 다시 못 쓴다.
    // 쿨타임만으로는 성공 후(조작 잠금을 지운 뒤) 반격 도중에 또 눌리는 걸 못 막는다.
    public override bool CanActivate(SkillContext ctx)
    {
        if (!base.CanActivate(ctx)) return false;
        if (ctx.Status.IsParrying) return false;

        var move = ctx.Transform != null ? ctx.Transform.GetComponent<PlayerMove>() : null;
        if (move != null && (move.isExecuting || move.IsControlLocked)) return false;
        return true;
    }

    public override void Activate(SkillContext ctx)
    {
        if (!TrySpendCost(ctx)) return;

        Debug.Log($"[{skillName}] 패링 (perfect {perfectWindow}s / window {parryWindow}s)");

        if (ctx.Animator != null) ctx.Animator.SetTrigger("Parry");

        // 방패 이펙트 — 몸통 중심에 맞춰 캐릭터를 부모로 붙인다. 이 애니메이션이 도는 동안이 판정 시간이다
        if (!string.IsNullOrEmpty(shieldVfxId) && ctx.Transform != null)
        {
            float side = ctx.Sprite != null && ctx.Sprite.flipX ? -1f : 1f;
            Vector3 center = ctx.Transform.position + new Vector3(shieldOffset.x * side, shieldOffset.y, 0f);
            PixelVfx.Play(shieldVfxId, center, 0f, ctx.Transform);
        }

        // 조작 불가: 방패 애니메이션(판정 시간)이 끝나고 나서 0.5초 더
        var move = ctx.Transform != null ? ctx.Transform.GetComponent<PlayerMove>() : null;
        if (move != null) move.LockControl(perfectWindow + Mathf.Max(0f, controlLockDuration - AbilityHooks.ParryLockReduction()));   // 세트: 반격의 기회

        ctx.Status.BeginParry(perfectWindow, parryWindow,
            perfectDamageMultiplier, blockedDamageMultiplier,
            counterDamageMultiplier, iframeDuration);
    }
}
