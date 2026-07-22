using System.Collections;
using UnityEngine;

// 근접 공격 적의 공통 베이스.
// 공통 흐름: 정지 → 방향 전환 → 공격 예고 → 공격 애니/히트박스 → (공격 중 동작) → 회복.
// "공격 중 동작"(돌진 / 제자리 등)만 파생 클래스가 AttackActivePhase로 구현한다.
public abstract class AttackEnemyBase : Enemy, IEnemyAttack
{
    [Header("공격 오브젝트")]
    [SerializeField] protected GameObject attackHitBox;
    [SerializeField] protected GameObject attackRangeBox;
    [SerializeField] protected GameObject attackPivot;

    [Header("공격 공통 설정")]
    [Tooltip("지상 전용 공격이면 체크 (플레이어가 공중이면 회피됨)")]
    [SerializeField] protected bool groundOnly = false;
    [Tooltip("공격 후 회복(경직) 시간(초)")]
    [SerializeField] protected float recoverTime = 1f;

    protected AttackRangeSet attackRangeSet;
    protected EnemyHitBox hitBox;

    public bool IsRunning => isAttacking;
    public bool IsGroundOnly => groundOnly;

    protected override void Start()
    {
        base.Start();

        // 근접 히트박스/범위 표시는 선택적 (원거리 적은 사용 안 함)
        if (attackPivot != null) attackRangeSet = attackPivot.GetComponent<AttackRangeSet>();
        if (attackHitBox != null)
        {
            hitBox = attackHitBox.GetComponent<EnemyHitBox>();
            if (hitBox != null) hitBox.Initialize(this);
            attackHitBox.SetActive(false);
        }
        if (attackRangeBox != null) attackRangeBox.SetActive(false);

        OnStart();
    }

    // 파생 클래스 초기화 훅
    protected virtual void OnStart() { }

    public void ShowRange(bool show)
    {
        attackRangeBox.SetActive(show);
    }

    public void Execute()
    {
        if (isAttacking)
            return;

        isAttacking = true;
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        try
        {
            StopMove();
            FaceToPlayer();

            Vector2 dir = ((Vector2)(player.position - transform.position)).normalized;
            if (attackRangeSet != null) attackRangeSet.SetDirection(dir);

            // 공격 예고
            if (attackRangeBox != null) attackRangeBox.SetActive(true);
            yield return new WaitForSeconds(attackWarningDuration);
            if (attackRangeBox != null) attackRangeBox.SetActive(false);

            // 공격 발동
            animator.SetTrigger("attack");
            if (attackHitBox != null)
            {
                hitBox.ResetHit();
                attackHitBox.SetActive(true);
            }

            // 공격 활성 동안의 동작 (돌진 / 제자리 / 발사 등) — 파생 구현
            // 이 구간엔 파생이 속도를 직접 제어하므로 상태머신의 분리 정렬을 끈다.
            IsAttackActive = true;
            yield return AttackActivePhase(dir);
            IsAttackActive = false;

            if (attackHitBox != null) attackHitBox.SetActive(false);

            // 회복
            yield return new WaitForSeconds(recoverTime);
        }
        finally
        {
            OnAttackFinally();
            lastAttackTime = Time.time;
            isAttacking = false;
            IsAttackActive = false;
        }
    }

    // 공격 히트박스가 켜져 있는 동안의 동작. 파생 클래스가 구현.
    protected abstract IEnumerator AttackActivePhase(Vector2 dirToPlayer);

    // 공격 종료 정리 훅 (예: 돌진의 물리 설정 복구)
    protected virtual void OnAttackFinally() { }

    protected virtual void OnDisable()
    {
        if (attackHitBox != null) attackHitBox.SetActive(false);
        if (attackRangeBox != null) attackRangeBox.SetActive(false);
    }
}
