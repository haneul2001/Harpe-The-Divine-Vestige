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
        enemy.FaceToPlayer();

        float distance = enemy.DistanceToPlayer();

        // 공격 범위 밖이면 이동
        if (distance > enemy.attackRange)
        {
            enemy.MoveToPlayer();
        }
        // 공격 범위 안이면 전투 대기 상태 진입
        else
        {
            enemy.StopMove();//isFollow 애니메이션 끄기

            stateMachine.ChangeState(enemy.CombatIdleState);
        }
    }

    public void Exit()
    {
        enemy.StopMove();
    }
}