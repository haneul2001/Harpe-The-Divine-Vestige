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
        // 은신 등으로 플레이어를 놓치면 추격을 멈추고 대기로 돌아간다
        if (!enemy.CanSeePlayer())
        {
            stateMachine.ChangeState(enemy.IdleState);
            return;
        }

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