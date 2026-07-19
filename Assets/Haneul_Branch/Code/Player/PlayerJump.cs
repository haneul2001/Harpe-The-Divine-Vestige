using UnityEngine;

public class PlayerJump : MonoBehaviour
{
    [Header("점프 설정")]
    [SerializeField] private float jumpHeight = 1.8f;
    [SerializeField] private float jumpDuration = 0.6f;
    [SerializeField] private KeyCode jumpKey = KeyCode.C;

    [Header("참조")]
    [Tooltip("점프 시 위로 뜨는 자식 오브젝트. 비워두면 자동 감지.")]
    [SerializeField] private Transform spriteRoot;

    private Animator anim;
    private PlayerCombat combat;

    private float verticalVelocity;
    private float currentHeight;
    private float gravity;
    private float initialUpVelocity;
    private bool isJumping;

    public float Height => currentHeight;
    public bool IsAirborne => isJumping;

    void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        anim = GetComponentInChildren<Animator>();

        if (spriteRoot == null && anim != null && anim.gameObject != gameObject)
        {
            spriteRoot = anim.transform;
        }

        float tUp = jumpDuration * 0.5f;
        initialUpVelocity = 2f * jumpHeight / tUp;
        gravity = initialUpVelocity / tUp;
    }

    void Update()
    {
        if (!isJumping && Input.GetKeyDown(jumpKey) && CanJump())
        {
            StartJump();
        }

        if (isJumping)
        {
            currentHeight += verticalVelocity * Time.deltaTime;
            verticalVelocity -= gravity * Time.deltaTime;

            if (currentHeight <= 0f)
            {
                currentHeight = 0f;
                verticalVelocity = 0f;
                isJumping = false;
                if (anim != null) anim.SetBool("isJumping", false);
            }

            ApplySpriteOffset();
        }
    }

    private bool CanJump()
    {
        if (combat == null) return true;
        return !combat.isAttacking && !combat.isCharging;
    }

    private void StartJump()
    {
        isJumping = true;
        verticalVelocity = initialUpVelocity;
        if (anim != null) anim.SetBool("isJumping", true);
    }

    private void ApplySpriteOffset()
    {
        if (spriteRoot == null) return;
        Vector3 pos = spriteRoot.localPosition;
        pos.y = currentHeight;
        spriteRoot.localPosition = pos;
    }
}
