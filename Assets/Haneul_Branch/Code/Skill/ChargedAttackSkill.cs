using System.Collections;
using UnityEngine;

// 차징 공격: 일정 시간 차징 후 전방 범위에 강한 스킬 데미지.
[CreateAssetMenu(fileName = "ChargedAttackSkill", menuName = "PlayerSkill/ChargedAttack")]
public class ChargedAttackSkill : PlayerSkill
{
    [Header("데미지")]
    [Tooltip("스킬 기본 데미지")]
    [SerializeField] private int skillBaseDamage = 80;
    [Tooltip("스킬 계수 — 데미지 = 기본 + 스킬공격력 × 계수 (RollSkillDamage)")]
    [SerializeField] private float coefficient = 2f;

    [Header("차징")]
    [Tooltip("차징 시간(초)")]
    [SerializeField] private float chargeTime = 1f;

    [Header("공격 판정")]
    [Tooltip("전방 판정 박스 크기")]
    [SerializeField] private Vector2 hitBoxSize = new Vector2(3f, 2f);
    [Tooltip("전방으로 밀어낸 거리")]
    [SerializeField] private float hitBoxForward = 1.5f;
    [Tooltip("적 레이어")]
    [SerializeField] private LayerMask enemyLayer;

    public override void Activate(SkillContext ctx)
    {
        if (!TrySpendCost(ctx)) return;
        ctx.CoroutineRunner.StartCoroutine(ChargeRoutine(ctx));
    }

    private IEnumerator ChargeRoutine(SkillContext ctx)
    {
        // TODO: 차징 애니메이션 / 이동 잠금 / 이펙트
        Debug.Log($"[{skillName}] 차징 시작 {chargeTime}s");
        yield return new WaitForSeconds(chargeTime);

        CharacterStats stats = ctx.Status.Stats;
        float dir = (ctx.Sprite != null && ctx.Sprite.flipX) ? -1f : 1f;
        Vector2 center = (Vector2)ctx.Transform.position + new Vector2(dir * hitBoxForward, 0f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, hitBoxSize, 0f, enemyLayer);
        foreach (Collider2D hit in hits)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null) continue;

            int dmg = stats.RollSkillDamage(skillBaseDamage, coefficient, out bool crit);
            enemy.TakeDamage(dmg, crit);
        }
        Debug.Log($"[{skillName}] 차징 공격 발동");
    }
}
