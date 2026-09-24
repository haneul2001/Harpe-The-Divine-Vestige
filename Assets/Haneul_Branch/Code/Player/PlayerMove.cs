using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("이동 설정")]
    public float speed = 5f;

    // 일시 버프가 곱하는 이동속도 배율 (처형 단계 버프 등). 저장하지 않는 런타임 값
    [System.NonSerialized] public float speedMult = 1f;

    // 최종 이동속도 = 기본 × 버프 × (1 + 특성 보너스)
    // 상점 능력치 카드로 올린 이동속도까지 포함한다
    public float CurrentSpeed => speed * speedMult * (1f + AbilityHooks.MoveSpeedBonus() + StatBonusMoveSpeed);

    private PlayerStatus statusForSpeed;
    private float StatBonusMoveSpeed
    {
        get
        {
            if (statusForSpeed == null) statusForSpeed = GetComponent<PlayerStatus>();
            return statusForSpeed != null ? statusForSpeed.Stats.bonusMoveSpeed : 0f;
        }
    }

    [Header("대쉬 설정")]
    public float dashSpeed = 10f;
    public float dashDuration = 0.25f;
    public float dashCooldown = 1f;
    [Header("피격")]
    [Tooltip("넉백 초기 속도. 시간에 따라 0까지 잦아들므로 실제 밀리는 거리는\n" +
             "약 (knockBackPower × hitDuration ÷ 2) 유닛이다.")]
    public float knockBackPower = 2.67f;
    public float hitDuration = 0.2f;

    // 넉백 감쇠용
    private Vector2 knockBackDir;
    private float knockBackEndTime;

    private bool isHit = false;
    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer spriter;
    private PlayerCombat combat;

    private Vector2 moveInput;
    private Vector2 dashDirection;

    public bool isExecuting = false;

    // 방 전환 같은 연출 중 조작만 막는다.
    // isExecuting과 달리 속도를 0으로 만들지 않는다 — 연출이 직접 속도를 몰아야 하기 때문.
    [HideInInspector] public bool inputLocked = false;

    private bool isDashing = false;

    // 은신 해제 판정 등 외부에서 대시 여부를 봐야 할 때
    public bool IsDashing { get { return isDashing; } }
    private float dashTimeLeft = 0f;
    private float lastDashTime = -100f;

    // 상태 아이콘(대시 쿨타임) 표시용
    // 세트(날렵함)가 줄여 준 최종 대시 쿨타임
    public float DashCooldown => dashCooldown * AbilityHooks.DashCooldownMult();
    public const KeyCode DashKey = KeyCode.LeftShift;
    public float DashCooldownRemaining => Mathf.Max(0f, lastDashTime + DashCooldown - Time.time);

    // 특성(끝없는 사냥)이 대시 쿨타임을 초기화한다
    public void ResetDashCooldown()
    {
        lastDashTime = -100f;
    }

    private Vector2 dashStartPos;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
        spriter = GetComponentInChildren<SpriteRenderer>();
        combat = GetComponent<PlayerCombat>();
        aim = GetComponent<PlayerAim>();
    }

    private PlayerAim aim;

    // 스킬 사용 직후 같은 짧은 조작 불가 (이동·대시·공격·스킬 전부). UI용 inputLocked와 따로 둔다 —
    // 둘이 한 플래그를 쓰면 한쪽이 풀 때 다른 쪽 잠금까지 같이 풀린다.
    private float controlLockUntil = -1f;
    public bool IsControlLocked => Time.time < controlLockUntil;

    public void LockControl(float seconds)
    {
        controlLockUntil = Mathf.Max(controlLockUntil, Time.time + Mathf.Max(0f, seconds));
    }

    public void ClearControlLock()
    {
        controlLockUntil = -1f;
    }

    void Update()
    {
        if (isExecuting || inputLocked || IsControlLocked)
        {
            moveInput = Vector2.zero;
            return;
        }

        // 입력 받기
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");

        if (moveInput.sqrMagnitude > 0.01f)
        {
            moveInput = moveInput.normalized;
            float yScale = (moveInput.y != 0) ? 0.7f : 1f;
            moveInput.y *= yScale;
        }
        

        // 대쉬 입력
        if (Input.GetKeyDown(DashKey) &&
            !isDashing && 
            Time.time >= lastDashTime + DashCooldown &&
            !combat.isAttacking &&
            !combat.isCharging
            ) //대시는 공격이나 차징 중에는 사용할 수 없도록 조건 추가
        {
            StartDash();
        }
        
    }

    void FixedUpdate()
    {
        // 연출 중: 속도는 건드리지 않는다. 방 전환의 "밀어 넣기"가 속도를 직접 몰기 때문에
        // 여기서 0으로 덮으면 매 물리 스텝마다 상쇄돼 플레이어가 문에 박힌다.
        if (inputLocked)
            return;

        if (isExecuting)
        {
            rb.velocity = Vector2.zero;
            anim.SetBool("isRun", false);
            return;
        }
        if (isHit)
        {
            // 넉백은 시간에 따라 잦아든다.
            // 등속으로 밀면 같은 거리라도 "밀렸다"가 아니라 "날아갔다"로 느껴진다.
            float remain = knockBackEndTime - Time.time;
            float k = hitDuration > 0f ? Mathf.Clamp01(remain / hitDuration) : 0f;
            rb.velocity = knockBackDir * knockBackPower * k;
            return;
        }

            if (isDashing)
            {
                HandleDash();
            }
            else
            {
                HandleNormalMovement();
            }
    }

    // ====================== 대쉬 시작 ======================
    private void StartDash()
    {
        isDashing = true;
        dashTimeLeft = dashDuration;
        lastDashTime = Time.time;

        anim.SetTrigger("Dash");

        // 8방향 대시: 누르고 있는 방향키, 안 누르고 있으면 조준(마지막 방향) 쪽으로.
        // 세로 성분은 걷기와 같은 0.7 보정을 줘서 위아래 대시가 옆보다 길어 보이지 않게 한다.
        Vector2 raw = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector2 dir;
        if (raw.sqrMagnitude > 0.01f)
        {
            float a = Mathf.Atan2(raw.y, raw.x) * Mathf.Rad2Deg;
            int index = ((Mathf.RoundToInt(a / 45f) % 8) + 8) % 8;
            dir = PlayerAim.Directions8[index];
        }
        else if (aim != null)
        {
            dir = aim.Direction;
        }
        else
        {
            dir = spriter.flipX ? Vector2.left : Vector2.right;
        }

        dashDirection = new Vector2(dir.x, dir.y * 0.7f);
        if (Mathf.Abs(dir.x) > 0.01f) spriter.flipX = dir.x < 0f;

        dashStartPos = rb.position;
        AbilityHooks.NotifyDashStart(dashStartPos, dashDirection.normalized);
    }

    // ====================== 대쉬 중 처리 ======================
    private void HandleDash()
    {
        dashTimeLeft -= Time.fixedDeltaTime;

        rb.velocity = dashDirection * dashSpeed;

        if (dashTimeLeft <= 0f || combat.isAttacking)
        {
            isDashing = false;
            AbilityHooks.NotifyDashEnd(dashStartPos, rb.position);
        }
    }

    // ====================== 공격 전진 ======================
    private Vector2 lungeVelocity;
    private float lungeEndTime = -1f;

    // 공격 방향으로 짧게 미끄러져 나간다. 속도로 밀기 때문에 벽·몬스터 콜라이더에는 막힌다.
    public void Lunge(Vector2 direction, float distance, float duration)
    {
        if (direction.sqrMagnitude < 0.0001f || distance <= 0f || duration <= 0f) return;
        Vector2 d = direction.normalized;
        d.y *= 0.7f;   // 걷기와 같은 세로 보정 — 위아래 전진이 옆보다 멀어 보이지 않게
        lungeVelocity = d * (distance / duration);
        lungeEndTime = Time.time + duration;
    }

    // ====================== 일반 이동 ======================
    private void HandleNormalMovement()
    {
        if (combat.isAttacking)
        {
            // 전진 구간이면 그만큼만 밀고, 끝나면 멈춘다
            rb.velocity = Time.time < lungeEndTime ? lungeVelocity : Vector2.zero;
            anim.SetBool("isRun", false);
            return;
        }

        rb.velocity = moveInput * CurrentSpeed;

        // 스프라이트 좌우 반전 (이 부분이 대쉬 방향의 기준이 됩니다)
        if (moveInput.x != 0)
        {
            spriter.flipX = moveInput.x < 0;
        }

        anim.SetBool("isRun", moveInput.sqrMagnitude > 0.01f);
    }
    public void KnockBack(Vector2 attackPos)
    {
        combat.CancelAttack(); // 피격 시 공격 취소
        isHit = true;

        knockBackDir =
            ((Vector2)transform.position - attackPos).normalized;

        knockBackEndTime = Time.time + hitDuration;
        rb.velocity = knockBackDir * knockBackPower;

        anim.SetTrigger("Hurt");

        Invoke(nameof(EndHit), hitDuration);
        
    }
    private void EndHit()
    {
        isHit = false;
    }

    
}