using UnityEngine;

public class CombatIdleState : IEnemyState
{
    private Enemy enemy;
    private EnemyStateMachine stateMachine;

    private float waitTimer;

    public CombatIdleState(Enemy enemy, EnemyStateMachine stateMachine)
    {
        this.enemy = enemy;
        this.stateMachine = stateMachine;
    }

   public void Enter()
    {
        Debug.Log("Enter CombatIdle");

        waitTimer = enemy.combatIdleDuration;

        enemy.StopMove();

        // 여기서 딱 한 번 방향 고정
        enemy.FaceToPlayer();

        if (enemy is ZombieEnemy zombie)
        {
            zombie.ShowAttackRange(true);
        }
    }

    public void Update()
    {
        waitTimer -= Time.deltaTime;

        // 공격 준비 끝
        if (waitTimer <= 0f && enemy.CanAttack())
        {
            stateMachine.ChangeState(enemy.AttackState);
        }
    }

    public void Exit()
    {
        if (enemy is ZombieEnemy zombie)
        {
            zombie.ShowAttackRange(false);
        }
    }
}