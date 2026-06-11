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
    Debug.Log("ChaseState Update");
    float distance = enemy.DistanceToPlayer();

    enemy.FaceToPlayer();

    // 공격 범위 안
    if (distance <= enemy.attackRange)
    {
        enemy.StopMove();

        if (enemy.CanAttack())
        {
            Debug.Log("Chase -> Attack");
            stateMachine.ChangeState(enemy.AttackState);
        }

        return;
    }

    // 공격 범위 밖
    enemy.MoveToPlayer();
}

    public void Exit()
    {
    }
}