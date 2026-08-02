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

    [Header("사거리 자동 맞춤")]
    [Tooltip("공격 시작 거리(attackRange)와 예고 표시를 실제 히트박스가 닿는 범위에 맞춘다.\n" +
             "히트박스는 스폰 크기 배율(Room.SpawnEntry.scale)을 따라 커지지만 attackRange는 숫자라 따라가지 않는다.\n" +
             "그래서 인스펙터에서 손으로 맞춰도 방에 스폰되는 순간 다시 어긋난다.\n" +
             "히트박스가 없는 원거리 적은 이 값과 무관하게 인스펙터 값을 그대로 쓴다.")]
    [SerializeField] protected bool matchAttackRangeToHitBox = true;

    [Tooltip("자동 계산에 더할 여유. 거리는 서로의 중심끼리 재므로 플레이어 몸 반지름\n" +
             "(캡슐 폭 0.8 → 0.4)만큼 줘야 몸이 닿는 순간 공격이 시작된다")]
    [SerializeField] protected float attackRangePadding = 0.4f;

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

        // 스폰 시 바뀐 localScale은 Start 시점엔 이미 적용돼 있으므로 여기서 재면 실제 크기가 반영된다.
        MatchAttackRangeToHitBox();
    }

    // 파생 클래스 초기화 훅
    protected virtual void OnStart() { }

    // ─────────────────────────────────────────────
    // 공격 시작 거리 = 히트박스가 실제로 닿는 거리
    // ─────────────────────────────────────────────

    // 히트박스보다 더 멀리서 시작해야 하는 공격(돌진 등)이 추가로 확보하는 거리.
    protected virtual float ExtraAttackReach => 0f;

    protected virtual void MatchAttackRangeToHitBox()
    {
        if (!matchAttackRangeToHitBox || attackHitBox == null)
            return;

        Transform hitBoxTransform = attackHitBox.transform;

        if (!TryGetHitBoxTip(hitBoxTransform, out Vector3 tip))
            return;

        // 아직 한 번도 공격하지 않아 AttackPivot은 정면(로컬 +x)을 보고 있다.
        // transform.right는 좌우 반전(y 180도)에 따라 뒤집히므로 절대값으로 잡는다.
        float reach = Mathf.Abs(Vector3.Dot(tip - transform.position, transform.right));

        attackRange = reach + attackRangePadding + ExtraAttackReach;

        MatchRangeBoxToHitBox(hitBoxTransform);
    }

    // 히트박스는 꺼져 있어 Collider2D.bounds를 쓸 수 없다. 로컬 값으로 정면 끝점을 직접 구한다.
    private bool TryGetHitBoxTip(Transform hitBoxTransform, out Vector3 tip)
    {
        tip = Vector3.zero;

        Collider2D col = hitBoxTransform.GetComponent<Collider2D>();
        if (col == null)
            return false;

        Vector2 local = col.offset;

        if (col is BoxCollider2D box) local.x += box.size.x * 0.5f;
        else if (col is CircleCollider2D circle) local.x += circle.radius;
        else return false;

        tip = hitBoxTransform.TransformPoint(local);
        return true;
    }

    // 예고 표시를 히트박스와 같은 자리·같은 크기로 맞춘다.
    // 둘 다 AttackPivot의 자식이고 같은 1유닛 사각 스프라이트를 쓰므로 트랜스폼만 복사하면 정확히 겹친다.
    private void MatchRangeBoxToHitBox(Transform hitBoxTransform)
    {
        if (attackRangeBox == null)
            return;

        Transform rangeBox = attackRangeBox.transform;

        // 부모가 다르면 좌표계가 달라 그대로 복사할 수 없다.
        if (rangeBox.parent != hitBoxTransform.parent)
            return;

        rangeBox.localPosition = hitBoxTransform.localPosition;
        rangeBox.localRotation = hitBoxTransform.localRotation;
        rangeBox.localScale = hitBoxTransform.localScale;
    }

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
            // 스스로 이동하는 공격(돌진)만 이 구간에 위치 잠금을 푼다.
            if (!LockPositionDuringActivePhase) SetPositionLocked(false);

            yield return AttackActivePhase(dir);
            IsAttackActive = false;

            if (attackHitBox != null) attackHitBox.SetActive(false);

            // 회복 — 경직 중에도 밀려나지 않게 다시 잠근다
            if (LockPositionWhileAttacking) SetPositionLocked(true);
            yield return new WaitForSeconds(recoverTime);
        }
        finally
        {
            SetPositionLocked(false);
            OnAttackFinally();
            lastAttackTime = Time.time;
            isAttacking = false;
            IsAttackActive = false;
        }
    }

    // 공격 히트박스가 켜져 있는 동안의 동작. 파생 클래스가 구현.
    protected abstract IEnumerator AttackActivePhase(Vector2 dirToPlayer);

    // 발동 구간에도 위치를 잠글지. 제자리/원거리는 true(기본), 돌진처럼 스스로 이동하면 false.
    protected virtual bool LockPositionDuringActivePhase => LockPositionWhileAttacking;

    // 공격 종료 정리 훅 (예: 돌진의 물리 설정 복구)
    protected virtual void OnAttackFinally() { }

    protected virtual void OnDisable()
    {
        if (attackHitBox != null) attackHitBox.SetActive(false);
        if (attackRangeBox != null) attackRangeBox.SetActive(false);
    }
}
