using UnityEngine;

public class AttackState : IEnemyState
{
    private Enemy enemy;
    private EnemyStateMachine stateMachine;

    public AttackState(Enemy enemy, EnemyStateMachine stateMachine)
    {
        this.enemy = enemy;
        this.stateMachine = stateMachine;
    }

    public void Enter()
    {
        Debug.Log("Enter Attack");

        enemy.StopMove();

        // 삭제
        // enemy.FaceToPlayer();

        enemy.Attack();
    }

    public void Update()
    {
        // 공격 끝
        if (!enemy.isAttacking)
        {
            float distance = enemy.DistanceToPlayer();

            // 멀어졌으면 추적
            if (distance > enemy.attackRange)
            {
                stateMachine.ChangeState(enemy.ChaseState);
            }
            // 가까우면 다시 공격 준비
            else
            {
                stateMachine.ChangeState(enemy.CombatIdleState);
            }
        }
    }

    public void Exit()
    {
    }
}