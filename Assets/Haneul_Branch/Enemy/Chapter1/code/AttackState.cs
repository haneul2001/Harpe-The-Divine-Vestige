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
        Debug.Log("AttackState Update");
        // 공격 끝
        if (!enemy.isAttacking)
        {
            Debug.Log("Attack -> Chase");
                stateMachine.ChangeState(enemy.ChaseState);
        }
    }

    public void Exit()
    {
    }
}