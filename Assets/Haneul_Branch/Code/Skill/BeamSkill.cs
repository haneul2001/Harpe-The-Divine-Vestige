using UnityEngine;

// 원거리 광선 발사: 바라보는 방향으로 직선 범위 안의 적에게 스킬 데미지.
[CreateAssetMenu(fileName = "BeamSkill", menuName = "PlayerSkill/Beam")]
public class BeamSkill : PlayerSkill
{
    [Header("데미지")]
    [Tooltip("스킬 기본 데미지")]
    [SerializeField] private int skillBaseDamage = 50;
    [Tooltip("스킬 계수 — 데미지 = 기본 + 스킬공격력 × 계수 (RollSkillDamage)")]
    [SerializeField] private float coefficient = 3f;

    [Header("광선 형태")]
    [Tooltip("사거리")]
    [SerializeField] private float range = 8f;
    [Tooltip("광선 폭")]
    [SerializeField] private float width = 1f;
    [Tooltip("적 레이어")]
    [SerializeField] private LayerMask enemyLayer;

    public override void Activate(SkillContext ctx)
    {
        if (!TrySpendCost(ctx)) return;

        CharacterStats stats = ctx.Status.Stats;
        float dir = (ctx.Sprite != null && ctx.Sprite.flipX) ? -1f : 1f;
        Vector2 origin = ctx.Transform.position;
        Vector2 boxCenter = origin + new Vector2(dir * range * 0.5f, 0f);

        // TODO: 광선 VFX / 애니메이션 (LineRenderer 등)
        Debug.Log($"[{skillName}] 광선 발사 (dir {dir}, range {range})");

        Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, new Vector2(range, width), 0f, enemyLayer);
        foreach (Collider2D hit in hits)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null) continue;

            int dmg = stats.RollSkillDamage(skillBaseDamage, coefficient, out bool crit);
            enemy.TakeDamage(dmg, crit);
        }
    }
}
