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

        // 공격 예고 동안 다른 적에게 밀려 끌려오지 않도록 위치를 잠근다.
        // (돌진처럼 발동 구간에 스스로 움직이는 공격은 AttackEnemyBase가 그때만 풀어 준다)
        if (enemy.LockPositionWhileAttacking)
            enemy.SetPositionLocked(true);

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
            return;
        }

        // 발동(히트박스 활성) 구간엔 파생 클래스(돌진/제자리)가 속도를 직접 제어하므로 건드리지 않고,
        // 예고·회복 구간엔 제자리에 완전히 고정 (공격 대기 중 제자리걸음 방지)
        if (!enemy.IsAttackActive)
            enemy.StopMove();
    }

    public void Exit()
    {
        // 피격 등으로 중간에 끊겨도 잠금이 남지 않게
        enemy.SetPositionLocked(false);
    }
}