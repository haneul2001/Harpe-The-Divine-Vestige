using UnityEngine;

public class EnemyHitBox : MonoBehaviour
{
    private Enemy owner;
    private bool hasHit;

    public void Initialize(Enemy enemy)
    {
        owner = enemy;
    }

    public void ResetHit()
    {
        hasHit = false;
    }

    // 이동하며 진입할 때(돌진 등)
    private void OnTriggerEnter2D(Collider2D other) => TryHit(other);

    // 이미 겹친 채로 히트박스가 켜지는 경우(제자리 공격 등):
    // Enter가 안 터지므로, 켜지는 순간 직접 겹침 검사로 확실히 처리 (트리거/sleep 의존 X)
    private void OnEnable()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return;

        Physics2D.SyncTransforms();
        Collider2D[] hits = Physics2D.OverlapBoxAll(col.bounds.center, col.bounds.size, 0f);
        foreach (var h in hits) TryHit(h);
    }

    private void TryHit(Collider2D other)
    {
        if (hasHit) return;
        if (owner == null) return;

        // 공격 예고 중에 죽으면 코루틴이 그대로 돌아 히트박스가 켜진다.
        // 시체가 때리지 않도록 여기서 끊는다.
        if (owner.isDead) return;

        PlayerStatus playerHealth = other.GetComponentInParent<PlayerStatus>();
        if (playerHealth == null) return;

        // 이 공격이 지면 공격이고 플레이어가 공중이면 무시
        IEnemyAttack attack = owner.AttackBehavior;
        if (attack != null && attack.IsGroundOnly)
        {
            PlayerJump jump = playerHealth.GetComponentInParent<PlayerJump>();
            if (jump != null && jump.IsAirborne)
            {
                return; // 회피 성공
            }
        }

        hasHit = true;
        bool landed = playerHealth.TakeDamage(owner.AttackDamage, owner);

        // 퍼펙트 패링/무적으로 무효화된 경우 넉백도 없음
        if (landed)
        {
            PlayerMove move = playerHealth.GetComponentInParent<PlayerMove>();
            if (move != null)
            {
                move.KnockBack(transform.position);
            }
        }
    }
}
