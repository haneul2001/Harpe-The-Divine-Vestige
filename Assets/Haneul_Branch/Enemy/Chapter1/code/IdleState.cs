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
// Enter()는 상태가 시작될 때 한 번 호출되는 메서드입니다. 여기서는 적이 멈추도록 설정합니다.
    public void Enter()
    {
        enemy.StopMove();

        Debug.Log("Enter Idle");
    }

    public void Update()
    {
        float distance = enemy.DistanceToPlayer();
        //distance가 범위에 들어오면 ChaseState로 전환
        if (distance <= enemy.detectRange)
        {//chaseState로 전환
            stateMachine.ChangeState(enemy.ChaseState);
        }
    }

    public void Exit()
    {
    }
}