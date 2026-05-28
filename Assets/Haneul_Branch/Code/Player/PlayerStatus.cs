using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStatus : MonoBehaviour
{
        [Header("체력")]
    [SerializeField] private int maxHp = 10;

    private int currentHp;

    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;


    [Header("소울")]
    [SerializeField] private int maxSoul = 100;
    private int currentSoul;

    [Header("경험치")]
    [SerializeField] private int currentExp = 0;
    [SerializeField] private int maxExp = 100;

    [Header("레벨")]
    [SerializeField] private int level = 1;

    [Header("피격")]
    [SerializeField] private float invincibleTime = 0.5f;
    private bool isInvincible;

    private void Start()
    {
        currentHp = maxHp;
    }

    public void TakeDamage(int damage)
    {
        if(isInvincible)
            return;
        currentHp -= damage;
        Debug.Log($"플레이어피격 : {damage} damage. Current HP: {currentHp}/{maxHp}");
    
        if (currentHp <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(InvincibleCoroutine());
        }
    }
        private IEnumerator InvincibleCoroutine()
        {
            isInvincible = true;
            yield return new WaitForSeconds(invincibleTime);
            isInvincible = false;
        }

        private void Die()
        {
            Debug.Log("플레이어 사망");
            // 사망 처리 (예: 애니메이션, 게임 오버 화면 등)
        }
    

}
