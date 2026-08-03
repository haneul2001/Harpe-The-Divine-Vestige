using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("이동 설정")]
    public float speed = 5f;

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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
        spriter = GetComponentInChildren<SpriteRenderer>();
        combat = GetComponent<PlayerCombat>();
    }

    void Update()
    {
        if (isExecuting || inputLocked)
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
        if (Input.GetKeyDown(KeyCode.LeftShift) &&
            !isDashing && 
            Time.time >= lastDashTime + dashCooldown&&
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
        float yScale = (moveInput.y != 0) ? 0.7f : 1f;
        Vector2 dashInput = new Vector2(moveInput.x, moveInput.y * yScale);

        if (moveInput != Vector2.zero)
        {
            dashDirection = dashInput.normalized;
        }
        else
        {
            dashDirection = spriter.flipX ? Vector2.left : Vector2.right;
        }// ★★★ 핵심 수정: 스프라이트가 바라보는 방향으로 대쉬 방향 결정 ★★★

        

        // 만약 위/아래도 대쉬하고 싶다면 아래처럼 y값도 고려할 수 있지만,
        // 당신이 요청한 대로 좌우만 바라보는 방향으로 고정합니다.
    }

    // ====================== 대쉬 중 처리 ======================
    private void HandleDash()
    {
        dashTimeLeft -= Time.fixedDeltaTime;

        rb.velocity = dashDirection * dashSpeed;

        if (dashTimeLeft <= 0f)
        {
            isDashing = false;
        }

        if (combat.isAttacking)
            isDashing = false;
    }

    // ====================== 일반 이동 ======================
    private void HandleNormalMovement()
    {
        if (combat.isAttacking)
        {
            rb.velocity = Vector2.zero;
            anim.SetBool("isRun", false);
            return;
        }

        rb.velocity = moveInput * speed;

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