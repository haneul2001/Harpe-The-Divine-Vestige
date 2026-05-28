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

        if (enemy is ZombieEnemy zombie)
        {
            zombie.ShowAttackRange(false);
        }
    }

    public void Update()
    {
        float distance = enemy.DistanceToPlayer();

        // 공격 범위 밖이면 추적
        if (distance > enemy.attackRange)
        {
            enemy.FaceToPlayer();

            enemy.MoveToPlayer();
        }
        // 공격 범위 안이면 전투 대기
        else
        {
            enemy.StopMove();

            stateMachine.ChangeState(enemy.CombatIdleState);
        }
    }

    public void Exit()
    {
        enemy.StopMove();
    }
}