using System.Collections;
using UnityEngine;

// 제자리 좀비: 이동 없이 그 자리에서 공격 판정만 낸다.
public class StationaryZombie : AttackEnemyBase
{
    [Header("제자리 공격 설정")]
    [Tooltip("공격 히트박스가 켜져 있는 시간(초)")]
    [SerializeField] private float attackActiveDuration = 0.3f;

    protected override IEnumerator AttackActivePhase(Vector2 dir)
    {
        // 이동하지 않고 히트박스만 유지
        rb.velocity = Vector2.zero;

        float timer = attackActiveDuration;
        while (timer > 0f && !isDead)
        {
            timer -= Time.deltaTime;
            rb.velocity = Vector2.zero; // 넉백 등으로 밀리지 않게 고정
            yield return null;
        }
    }
}
