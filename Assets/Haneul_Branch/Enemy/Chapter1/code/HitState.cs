using UnityEngine;

public class HitState : IEnemyState
{
    private Enemy enemy;
    private EnemyStateMachine stateMachine;

    private float hitTimer;

    public HitState(Enemy enemy, EnemyStateMachine stateMachine)
    {
        this.enemy = enemy;
        this.stateMachine = stateMachine;
    }

    public void Enter()
    {
        Debug.Log("Enter Hit");

        hitTimer = enemy.hitDuration;

        enemy.StopMove();

        enemy.animator.SetTrigger("hit");
    }

    public void Update()
    {
        hitTimer -= Time.deltaTime;

        if (hitTimer <= 0f)
        {
            stateMachine.ChangeState(enemy.CombatIdleState);
        }
    }

    public void Exit()
    {
    }
}