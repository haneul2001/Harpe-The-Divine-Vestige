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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        if (owner == null) return;

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
        playerHealth.TakeDamage(owner.AttackDamage);

        PlayerMove move = playerHealth.GetComponentInParent<PlayerMove>();
        if (move != null)
        {
            move.KnockBack(transform.position);
        }
    }
}
