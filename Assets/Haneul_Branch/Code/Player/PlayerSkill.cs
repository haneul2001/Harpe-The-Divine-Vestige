using UnityEngine;

// 모든 스킬의 베이스 (ScriptableObject 데이터 + 발동 로직).
public abstract class PlayerSkill : ScriptableObject
{
    [Header("공통")]
    public string skillName;
    public Sprite icon;
    [Tooltip("쿨타임(초)")] public float cooldown = 0f;
    [Tooltip("소모 소울 (지능이 높을수록 실제 소모량 감소). 0이면 무료.")]
    public int soulCost = 0;

    // 발동 가능 여부: 기본은 소울 충분한지 확인 (파생에서 조건 추가 시 base 호출).
    public virtual bool CanActivate(SkillContext ctx)
    {
        if (ctx == null || ctx.Status == null) return false;
        if (soulCost <= 0) return true;
        return ctx.Status.Stats.soul >= RequiredSoul(ctx);
    }

    public abstract void Activate(SkillContext ctx);

    // 지능 할인 적용된 실제 소모 소울량.
    protected int RequiredSoul(SkillContext ctx)
        => Mathf.Max(1, Mathf.RoundToInt(soulCost * ctx.Status.Stats.SoulCostMult));

    // 소울 차감 시도. 부족하면 false (이때 Activate는 중단해야 함). 무료 스킬은 항상 true.
    protected bool TrySpendCost(SkillContext ctx)
    {
        if (soulCost <= 0) return true;
        return ctx.Status.Stats.TrySpendSoul(soulCost);
    }
}
