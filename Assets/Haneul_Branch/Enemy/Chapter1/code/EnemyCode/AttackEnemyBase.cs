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

    [Header("공격 타이밍")]
    [Tooltip("공격 애니메이션 중간에 걸린 이벤트를 기다렸다 히트박스를 켠다.\n" +
             "끄면 애니메이션 시작과 동시에 켜진다(예전 방식).")]
    [SerializeField] protected bool waitForAnimationHit = true;

    [Tooltip("이벤트가 오지 않을 때를 대비한 최대 대기 시간(초).\n" +
             "애니메이션에 이벤트를 안 걸었거나 클립이 바뀌어도 공격이 먹통이 되지 않게 한다.")]
    [SerializeField] protected float hitEventTimeout = 0.7f;

    // 애니메이션 이벤트가 이 프레임에 도달했는지
    private bool animHitReceived;

    // ── 애니메이션 이벤트에서 호출 ──────────────────────────
    // 공격 클립 중간 프레임에 Function 이름으로 "AnimAttackHit"을 걸어 두면 된다.
    // Animator와 같은 오브젝트에 이 컴포넌트가 있어야 호출된다 (둘 다 적 루트에 있다).
    public void AnimAttackHit()
    {
        animHitReceived = true;
    }

    [Header("공격 방향")]
    [Tooltip("플레이어처럼 좌우로만 공격한다. 끄면 플레이어 쪽으로 임의 각도로 회전한다.\n" +
             "탑다운이지만 캐릭터·이펙트가 전부 좌우 기준으로 그려져 있어 켜 두는 편이 자연스럽다.")]
    [SerializeField] protected bool horizontalAttackOnly = true;

    [Tooltip("좌우 공격일 때 높이가 이만큼 안에 들어와야 공격한다.\n" +
             "너무 좁으면 적이 줄을 맞추느라 계속 따라다니고, 너무 넓으면 빗나가는 게 눈에 보인다.")]
    [SerializeField] protected float verticalTolerance = 1.2f;

    [Header("공격 예고 표시")]
    [Tooltip("공격 전 범위 표시를 띄운다. 끄면 예고 시간(attackWarningDuration)은 그대로 두고 표시만 안 나온다 — "
           + "리듬은 유지하되 화면을 깔끔하게 두고 싶을 때 쓴다.")]
    [SerializeField] protected bool showAttackWarning = false;

    // 지금 떠 있는 예고 표식. 적이 예고 도중 죽으면 코루틴이 finally 없이 끊기므로
    // 여기 들고 있다가 OnDisable에서 확실히 지운다.
    private PixelVfx activeTelegraph;

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

    // 좌우 공격이면 높이가 맞아야 실제로 닿는다. 안 맞으면 계속 접근해 줄을 맞춘다.
    public override bool IsAttackAligned()
    {
        if (!horizontalAttackOnly || player == null) return true;
        return Mathf.Abs(player.position.y - transform.position.y) <= verticalTolerance;
    }

    // 플레이어 쪽 방향. 좌우 공격이면 x 부호만 남긴다.
    protected Vector2 AimDirection()
    {
        Vector2 raw = player != null
            ? ((Vector2)(player.position - transform.position))
            : (IsFacingRight ? Vector2.right : Vector2.left);

        if (!horizontalAttackOnly) return raw.sqrMagnitude > 0.0001f ? raw.normalized : Vector2.right;

        return raw.x >= 0f ? Vector2.right : Vector2.left;
    }

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

    // 베기 이펙트. 판정이 나가는 그 자리에, 그 크기로, 그 방향으로 띄운다.
    // 판정과 그림이 어긋나면 "분명히 피했는데 맞았다"가 된다.
    private void SpawnSlash(Vector2 dir)
    {
        if (attackHitBox == null) return;

        // 파티클은 음수 배율로 뒤집으면 깨지므로, 좌향은 아예 좌우 반전된 프리팹을 쓴다.
        bool left = horizontalAttackOnly && dir.x < 0f;
        var slash = PixelVfx.Play(left ? "SlashHitLeft" : "SlashHit", attackHitBox.transform.position);
        if (slash == null) return;

        var box = attackHitBox.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            // 판정 높이에 맞춰 키운다. 라이브러리 기본 배율 위에 곱한다.
            float h = box.size.y * Mathf.Abs(attackHitBox.transform.lossyScale.y);
            float k = Mathf.Clamp(h / 2f, 0.4f, 2f);
            slash.transform.localScale = Vector3.Scale(slash.transform.localScale, new Vector3(k, k, 1f));
        }

        if (!horizontalAttackOnly)
            slash.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    // 공격 예고 표식. 히트박스 자리에 히트박스 크기로 띄운다.
    // 적을 부모로 삼지 않는다 — 예고는 "여기가 맞는다"는 표시라 적을 따라 움직이면 안 된다.
    private PixelVfx SpawnTelegraph()
    {
        if (attackHitBox == null) return null;

        ClearTelegraph();   // 이전 것이 남아 있으면 먼저 치운다

        var vfx = PixelVfx.Play("AttackTelegraph", attackHitBox.transform.position);
        activeTelegraph = vfx;
        if (vfx == null) return null;

        // 히트박스는 꺼져 있어 bounds를 못 쓴다. 콜라이더 크기 x 스케일로 직접 잰다.
        var box = attackHitBox.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Vector3 ls = attackHitBox.transform.lossyScale;
            float w = box.size.x * Mathf.Abs(ls.x);
            float h = box.size.y * Mathf.Abs(ls.y);
            // 프레임 원본이 2x2 유닛이다. 가로·세로를 따로 늘려 판정 박스 비율을 그대로 따라간다 —
            // 원형으로 두면 가로로 긴 판정과 표시가 어긋나 어디까지 맞는지 알 수 없다.
            vfx.transform.localScale = new Vector3(
                Mathf.Clamp(w / 2f, 0.3f, 5f),
                Mathf.Clamp(h / 2f, 0.3f, 5f),
                1f);
        }
        return vfx;
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

            Vector2 dir = AimDirection();
            if (attackRangeSet != null) attackRangeSet.SetDirection(dir);

            // 공격 예고 — 표시 여부와 무관하게 시간은 흐른다
            if (showAttackWarning)
            {
                if (attackRangeBox != null) attackRangeBox.SetActive(true);
                SpawnTelegraph();
            }

            // 통째로 기다리지 않고 매 프레임 사망을 확인한다.
            // WaitForSeconds로 묶으면 예고 도중 죽어도 그 시간이 다 흐를 때까지
            // 표식이 화면에 남고, 죽은 적이 뒤늦게 공격까지 낸다.
            float warned = 0f;
            while (warned < attackWarningDuration && !isDead)
            {
                warned += Time.deltaTime;
                yield return null;
            }

            if (attackRangeBox != null) attackRangeBox.SetActive(false);
            ClearTelegraph();

            if (isDead) yield break;   // 뒷정리는 finally가 한다

            // 애니메이션을 걸기 직전. 이번 공격이 무엇인지 여기서 정해야
            // 그에 맞는 클립을 재생할 수 있다 (보스의 패턴 선택이 여기 붙는다).
            OnAttackStart();

            // 공격 발동
            animator.SetTrigger("attack");

            // 애니메이션 중간 지점까지 기다렸다 판정을 켠다.
            // 휘두르기 시작과 동시에 맞는 것보다 훨씬 읽기 쉬운 공격이 된다.
            if (waitForAnimationHit)
                yield return WaitForAnimationHit();

            if (attackHitBox != null)
            {
                hitBox.ResetHit();
                attackHitBox.SetActive(true);
                SpawnSlash(dir);
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
            ClearTelegraph();
            SetPositionLocked(false);
            OnAttackFinally();
            lastAttackTime = Time.time;
            isAttacking = false;
            IsAttackActive = false;
        }
    }

    // 애니메이션 이벤트를 기다린다.
    // 이벤트가 없거나(클립에 안 걸었거나 교체됐거나) 늦으면 타임아웃으로 넘어간다 —
    // 공격이 영영 안 나가는 것보다 조금 어긋나는 편이 낫다.
    private IEnumerator WaitForAnimationHit()
    {
        animHitReceived = false;

        float elapsed = 0f;
        while (!animHitReceived && elapsed < hitEventTimeout && !isDead)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // 공격 히트박스가 켜져 있는 동안의 동작. 파생 클래스가 구현.
    protected abstract IEnumerator AttackActivePhase(Vector2 dirToPlayer);

    // 발동 구간에도 위치를 잠글지. 제자리/원거리는 true(기본), 돌진처럼 스스로 이동하면 false.
    protected virtual bool LockPositionDuringActivePhase => LockPositionWhileAttacking;

    // 공격 종료 정리 훅 (예: 돌진의 물리 설정 복구)
    protected virtual void OnAttackFinally() { }

    // 공격 애니메이션을 걸기 직전에 불린다.
    // 이 시점 이후로는 LockPositionDuringActivePhase도 결정돼 있어야 한다.
    protected virtual void OnAttackStart() { }

    // 오브젝트가 꺼지거나 파괴되면 코루틴이 finally 없이 끊긴다.
    // 예고 표식은 적의 자식이 아니라서 그대로 화면에 남으므로 여기서 반드시 지운다.
    protected virtual void OnDisable()
    {
        if (attackHitBox != null) attackHitBox.SetActive(false);
        if (attackRangeBox != null) attackRangeBox.SetActive(false);
        ClearTelegraph();
    }

    private void ClearTelegraph()
    {
        if (activeTelegraph != null) activeTelegraph.Stop();
        activeTelegraph = null;
    }
}
