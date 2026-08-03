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
        // 감지 범위 안이고 은신 중이 아니면 추격 시작
        if (enemy.CanSeePlayer())
        {
            stateMachine.ChangeState(enemy.ChaseState);
        }
    }

    public void Exit()
    {
    }
}