using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    Animator anim;
    public bool isAttacking=false;
    public int attackNum = 0;
    void Awake()
    {
        anim = GetComponent<Animator>();
    }


    float curTime;
    float coolTime = 0.5f;

    public Transform pos;
    public Vector2 boxSize;
    void Update()
    {

        if (!isAttacking && Input.GetKeyDown(KeyCode.Z))
        {
            Collider2D[] collider2Ds = Physics2D.OverlapBoxAll(pos.position, boxSize, 0);

            foreach(Collider2D collider in collider2Ds)
            {
                if (collider.CompareTag("Enemy"))
                {
                    Debug.Log("적과 충돌");
                    //적에게 데미지 주는 코드 작성
                    collider.GetComponent<Enemy>().TakeDamage(1);

                }
            }

            if (attackNum > 2)
            {
                attackNum = 0;
            }
            Attack(attackNum);
            attackNum++;
            
        }
        
    }

    public enum AttackType
    {
        None,
        Attack1,
        Attack2,
        Attack3
    }
    public AttackType currentAttack;

    public void setAttack()
    {

    }

    public void Attack(int attackNum)
    {
        anim.SetFloat("Blend",attackNum);
        anim.SetTrigger("Attack");
    }

    public void StartAttack()
    {
        isAttacking=true;
        Debug.Log("공격 시작");
    }
    public void EndAttack()
    {
        isAttacking = false;
        Debug.Log("공격 끝");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(pos.position, boxSize);

    }

    void Guard()
    {
        //방어 코드 작성
    }
}
