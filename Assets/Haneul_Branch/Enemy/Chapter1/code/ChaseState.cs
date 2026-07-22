using UnityEngine;

public class ChaseState : IEnemyState
{
    private Enemy enemy;
    private EnemyStateMachine stateMachine;

    public ChaseState(Enemy enemy, EnemyStateMachine stateMachine)
    {
        this.enemy = enemy;
        this.stateMachine = stateMachine;
    }

    public void Enter()
    {
        Debug.Log("Enter Chase");
    }

    public void Update()
    {
        float distance = enemy.DistanceToPlayer();

        enemy.FaceToPlayer();

        // 공격 범위 안: 전진은 멈추되 분리는 유지해 서로 겹치지 않게 자리를 잡음
        if (distance <= enemy.attackRange)
        {
            enemy.SettleWithSeparation();

            if (enemy.CanAttack())
            {
                stateMachine.ChangeState(enemy.AttackState);
            }

            return;
        }

        // 공격 범위 밖: 추격
        enemy.MoveToPlayer();
    }

    public void Exit()
    {
    }
}