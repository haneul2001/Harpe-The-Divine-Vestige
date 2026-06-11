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
    public float knockBackPower = 8f;
    public float hitDuration = 0.2f;

    private bool isHit = false;
    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer spriter;
    private PlayerCombat combat;

    private Vector2 moveInput;
    private Vector2 dashDirection;

    public bool isExecuting = false;
    private bool isDashing = false;
    private float dashTimeLeft = 0f;
    private float lastDashTime = -100f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriter = GetComponent<SpriteRenderer>();
        combat = GetComponent<PlayerCombat>();
    }

    void Update()
    {
        if (isExecuting)
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
        if (isExecuting)
        {
            rb.velocity = Vector2.zero;
            anim.SetBool("isRun", false);
            return;
        }
        if (isHit)
                return;

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

        Vector2 dir =
            ((Vector2)transform.position - attackPos).normalized;

        rb.velocity = dir * knockBackPower;

        anim.SetTrigger("Hurt");

        Invoke(nameof(EndHit), hitDuration);
        
    }
    private void EndHit()
    {
        isHit = false;
    }

    
}