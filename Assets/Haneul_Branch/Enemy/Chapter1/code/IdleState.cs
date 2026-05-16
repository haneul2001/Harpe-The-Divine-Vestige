using UnityEngine;

public class IdleState : IEnemyState
{
    private Enemy enemy;
    private EnemyStateMachine stateMachine;

    public IdleState(Enemy enemy, EnemyStateMachine stateMachine)
    {
        this.enemy = enemy;
        this.stateMachine = stateMachine;
    }

    public void Enter()
    {
        enemy.StopMove();

        Debug.Log("Enter Idle");
    }

    public void Update()
    {
        float distance = enemy.DistanceToPlayer();

        if (distance <= enemy.detectRange)
        {
            stateMachine.ChangeState(enemy.ChaseState);
        }
    }

    public void Exit()
    {
    }
}