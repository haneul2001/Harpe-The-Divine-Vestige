using UnityEngine;

public class AttackState : IEnemyState
{
    private Enemy enemy;
    private EnemyStateMachine stateMachine;

    private float attackTimer;
    private float attackDuration = 0.3f; // 나중에 데이터 참조

    public AttackState(Enemy enemy, EnemyStateMachine stateMachine)
    {
        this.enemy = enemy;
        this.stateMachine = stateMachine;
    }

    public void Enter()
    {
        Debug.Log("Enter Attack");

        attackTimer = attackDuration;

        enemy.StopMove();

        enemy.FaceToPlayer();

        enemy.Attack();
    }

    public void Update()
    {
        attackTimer -= Time.deltaTime;

        if (attackTimer <= 0f)
        {
            stateMachine.ChangeState(enemy.CombatIdleState);
        }
    }

    public void Exit()
    {
    }
}