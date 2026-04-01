using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    Animator anim;
    public bool isAttacking=false;

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            Attack1();
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

    public void Attack1()
    {
        isAttacking = true;
        anim.SetTrigger("Attack1");
    }

    public void StartAttack()
    {
        isAttacking=false;
    }
    public void EndAttack()
    {
        isAttacking = false;
    }
}
