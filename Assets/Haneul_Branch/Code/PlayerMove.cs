using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("이동 설정")]
    public float speed = 5f;

    [Header("대쉬 설정")]
    public float dashSpeed = 10f;
    public float dashDuration = 0.25f;
    public float dashCooldown = 1f;

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
        if (Input.GetKeyDown(KeyCode.LeftShift) && !isDashing && Time.time >= lastDashTime + dashCooldown)
        {
            StartDash();
        }
        if (Input.GetKeyDown(KeyCode.V))
        {
            Debug.Log("V 눌림 (Move에서)");
            //combat.TryExecuteEnemy();
        }
    }

    void FixedUpdate()
    {
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

        // ★★★ 핵심 수정: 스프라이트가 바라보는 방향으로 대쉬 방향 결정 ★★★
        dashDirection = spriter.flipX ? Vector2.left : Vector2.right;

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
}