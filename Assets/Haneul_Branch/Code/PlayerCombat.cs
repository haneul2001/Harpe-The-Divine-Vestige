using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    Animator anim;
    public bool isAttacking = false;
    public int attackNum = 0;

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    public Transform AttackBoxPos;
    public Vector2 boxSize;

    void Update()
    {
        NormalAttack(); // Z입력

        // ===== 처형 =====

    }

    void LateUpdate()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();

        if (sr.flipX)
        {
            AttackBoxPos.localPosition = new Vector3(-Mathf.Abs(AttackBoxPos.localPosition.x), AttackBoxPos.localPosition.y, 0);
        }
        else
        {
            AttackBoxPos.localPosition = new Vector3(Mathf.Abs(AttackBoxPos.localPosition.x), AttackBoxPos.localPosition.y, 0);
        }
    }

    public void TryExecuteEnemy()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 7f);

        SpriteRenderer playerSR = GetComponent<SpriteRenderer>();
        Vector2 playerForward = playerSR.flipX ? Vector2.left : Vector2.right;

        foreach (var hit in hits)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null) continue;
            if (!enemy.CanExecute()) continue;

            Vector2 dirToEnemy = (enemy.transform.position - transform.position).normalized;
            float dot = Vector2.Dot(playerForward, dirToEnemy);

            if (dot > 0.5f)
            {
                Debug.Log("정면 처형 성공");
                enemy.TryExecution(transform);
                break;
            }
        }
    }

    public void NormalAttack()
    {
        // ===== 일반 공격 =====
        if (!isAttacking && Input.GetKeyDown(KeyCode.Z))
        {
            Collider2D[] collider2Ds = Physics2D.OverlapBoxAll(AttackBoxPos.position, boxSize, 0);
            //OverlapBoxAll을 사용
            foreach (Collider2D collider in collider2Ds)
            {
                if (collider.TryGetComponent(out Enemy enemy))
                {
                    enemy.TakeDamage(1);
                }
            }
            //공격 애니메이션과 공격 번호 관리
            if (attackNum > 2)
                attackNum = 0;

            Attack(attackNum);
            attackNum++;
        }
    }

    public void Attack(int attackNum)
    {
        anim.SetFloat("Blend", attackNum);
        anim.SetTrigger("Attack");
    }

    public void StartAttack()
    {
        isAttacking = true;
    }

    public void EndAttack()
    {
        isAttacking = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(AttackBoxPos.position, boxSize);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, 2f);
    }

    public void StartParry()
    {
        Debug.Log("패링 시작");
    }
}