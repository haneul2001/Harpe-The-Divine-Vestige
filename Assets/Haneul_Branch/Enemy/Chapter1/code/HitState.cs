using UnityEngine;

public class HitState : IEnemyState
{
    private Enemy enemy;
    private EnemyStateMachine stateMachine;

    private float hitTimer;
    private static readonly int HurtHash = Animator.StringToHash("Hurt");

    public HitState(Enemy enemy, EnemyStateMachine stateMachine)
    {
        this.enemy = enemy;
        this.stateMachine = stateMachine;
    }

    public void Enter()
    {
        Debug.Log("Enter Hit");

        hitTimer = enemy.hitDuration;

        // 맞으면 하던 공격은 끝 — 안 끊으면 피격 모션 도중 공격이 튀어나온다
        if (enemy is AttackEnemyBase attacker) attacker.CancelAttack();

        enemy.StopMove();

        // 피격 모션은 신호(trigger)가 아니라 처음부터 바로 다시 튼다.
        // 신호로 걸면 피격·공격 모션 중에 맞은 신호가 받아 줄 전이가 없어 쌓여 있다가,
        // 대기로 돌아오는 순간 피격 모션이 한 번 더 나왔다.
        var an = enemy.animator;
        if (an != null)
        {
            an.ResetTrigger("hit");
            an.ResetTrigger("attack");
            if (an.HasState(0, HurtHash)) an.Play(HurtHash, 0, 0f);
            else an.SetTrigger("hit");
        }
    }

    public void Update()
    {
        hitTimer -= Time.deltaTime;

        if (hitTimer <= 0f)
        {
            stateMachine.ChangeState(enemy.IdleState);
        }
    }

    public void Exit()
    {
    }
}