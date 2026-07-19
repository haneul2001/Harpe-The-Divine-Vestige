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

        if (enemy.AttackBehavior != null)
        {
            enemy.AttackBehavior.Execute();
        }
        else
        {
            Debug.LogWarning($"{enemy.name}: IEnemyAttack 컴포넌트가 없음. Chase로 복귀.");
            stateMachine.ChangeState(enemy.ChaseState);
        }
    }

    public void Update()
    {
        // 공격 끝
        if (!enemy.isAttacking)
        {
            stateMachine.ChangeState(enemy.ChaseState);
        }
    }

    public void Exit()
    {
    }
}