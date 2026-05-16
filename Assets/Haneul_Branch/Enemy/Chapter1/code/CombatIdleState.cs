using UnityEngine;

public class CombatIdleState : IEnemyState
{
    private Enemy enemy;
    private EnemyStateMachine stateMachine;

    private float waitTimer;

    public CombatIdleState(Enemy enemy, EnemyStateMachine stateMachine)
    {
        this.enemy = enemy;
        this.stateMachine = stateMachine;
    }

    public void Enter()
    {
        Debug.Log("Enter CombatIdle");

        waitTimer = enemy.combatIdleDuration;

        enemy.StopMove();
    }

    public void Update()
    {
        enemy.FaceToPlayer();

        waitTimer -= Time.deltaTime;

        if (waitTimer <= 0f)
        {
            float distance = enemy.DistanceToPlayer();

            // 공격 가능 + 공격 범위 안
            if (distance <= enemy.attackRange && enemy.CanAttack())
            {
                stateMachine.ChangeState(enemy.AttackState);
            }
            // 공격 불가능하거나 플레이어가 멀면 다시 추적
            else
            {
                stateMachine.ChangeState(enemy.ChaseState);
            }
        }
    }

    public void Exit()
    {
    }
}