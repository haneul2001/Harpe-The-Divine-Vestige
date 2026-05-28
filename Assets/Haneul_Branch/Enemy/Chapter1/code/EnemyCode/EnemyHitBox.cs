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
        if(hasHit)
            return;
        if(owner == null)
            return;

        Debug.Log("충돌 대상 : " + other.name);

        PlayerStatus playerHealth =
            other.GetComponent<PlayerStatus>();

        if (playerHealth != null)
        {
            hasHit = true;
            Debug.Log("플레이어 감지");
            
            playerHealth.TakeDamage(owner.AttackDamage);

            PlayerMove move =
            playerHealth.GetComponent<PlayerMove>();

            if (move != null)
            {
                move.KnockBack(transform.position);
            }
            Debug.Log("데미지 적용");
        }
    }
}