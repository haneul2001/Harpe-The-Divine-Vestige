using UnityEngine;

// 은신. 기본 C키로 켜고 끈다.
//   · 플레이어 스프라이트가 반투명해진다
//   · 적이 감지하지 못한다 (Enemy.CanSeePlayer가 이 상태를 본다)
//   · 이동을 뺀 다른 행동(공격/차징/대시/처형/스킬)을 하거나 피해를 입으면 풀린다
//
// 나중에 능력 해금으로 열 예정이라 unlocked 스위치를 미리 뒀다.
// 해금 전에는 키를 눌러도 아무 일도 일어나지 않는다.
public class PlayerStealth : MonoBehaviour
{
    public static PlayerStealth Instance { get; private set; }

    // 적이 매 프레임 참조하므로 정적으로 노출한다. 플레이어가 없으면 항상 false.
    public static bool IsHidden { get { return Instance != null && Instance.hidden; } }

    [Header("입력")]
    [SerializeField] private KeyCode toggleKey = KeyCode.C;
    [Tooltip("능력 해금 스위치. 꺼져 있으면 키를 눌러도 은신하지 않는다.\n" +
             "나중에 능력 시스템에서 PlayerStealth.IsUnlocked = true 로 열어 주면 된다.")]
    [SerializeField] private bool unlocked = true;

    [Header("연출")]
    [Range(0f, 1f)]
    [SerializeField] private float hiddenAlpha = 0.5f;
    [Tooltip("투명해지고 돌아오는 데 걸리는 시간(초). 0이면 즉시 바뀐다")]
    [SerializeField] private float fadeDuration = 0.12f;

    [Header("해제 조건")]
    [Tooltip("대시도 은신을 푸는지. 끄면 대시를 그냥 이동으로 취급한다")]
    [SerializeField] private bool breakOnDash = true;
    [Tooltip("피해를 입으면 은신이 풀린다")]
    [SerializeField] private bool breakOnDamage = true;
    [Tooltip("은신이 풀린 뒤 다시 은신할 수 있을 때까지의 시간(초)")]
    [SerializeField] private float recloakDelay = 0.5f;

    public bool IsUnlocked { get { return unlocked; } set { unlocked = value; } }

    private bool hidden;
    private float nextCloakTime;

    // 현재 적용 중인 알파. 페이드 때문에 목표값과 따로 관리한다.
    private float alpha = 1f;
    private float appliedAlpha = -1f;

    private PlayerCombat combat;
    private PlayerMove move;
    private PlayerStatus status;
    private int lastHp;

    private SpriteRenderer[] renderers;
    private Color[] baseColors;

    private void Awake()
    {
        Instance = this;

        combat = GetComponent<PlayerCombat>();
        move = GetComponent<PlayerMove>();
        status = GetComponent<PlayerStatus>();

        // 원래 색을 기억해 둔다. 알파를 곱해서 쓰므로 원본 투명도가 있어도 유지된다.
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i].color;
    }

    private void Start()
    {
        if (status != null) lastHp = status.CurrentHp;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();

        // 해제 판정은 hp를 갱신하기 전에 (이번 프레임에 깎였는지 봐야 한다)
        if (hidden && ShouldBreak())
            Break();

        if (status != null) lastHp = status.CurrentHp;

        UpdateAlpha();
    }

    private void Toggle()
    {
        if (hidden)
        {
            Break();
            return;
        }

        if (!unlocked) return;
        if (Time.time < nextCloakTime) return;

        // 공격 중이거나 처형 중이면 애초에 못 들어간다
        if (ShouldBreak()) return;

        hidden = true;
    }

    // 외부에서 은신을 끊을 때 (스킬 발동 등). 컴포넌트를 못 찾아도 안전하게 동작한다.
    public static void BreakStealth()
    {
        if (Instance != null) Instance.Break();
    }

    public void Break()
    {
        if (!hidden) return;

        hidden = false;
        nextCloakTime = Time.time + recloakDelay;
    }

    // 이동 외의 행동. 스킬은 SkillInputController가 실제로 발동한 순간에 직접 끊어 준다
    // (쿨타임이라 실패한 키 입력까지 은신을 풀면 억울하다).
    private bool ShouldBreak()
    {
        if (combat != null && (combat.isAttacking || combat.isCharging)) return true;
        if (move != null && move.isExecuting) return true;                 // 처형
        if (breakOnDash && move != null && move.IsDashing) return true;
        if (breakOnDamage && status != null && status.CurrentHp < lastHp) return true;

        return false;
    }

    private void UpdateAlpha()
    {
        float target = hidden ? hiddenAlpha : 1f;

        if (fadeDuration > 0f)
            alpha = Mathf.MoveTowards(alpha, target, Time.deltaTime / fadeDuration);
        else
            alpha = target;

        // 값이 바뀔 때만 칠한다. 매 프레임 색을 덮으면 피격 점멸 같은 다른 연출과 싸운다.
        if (Mathf.Approximately(alpha, appliedAlpha)) return;
        appliedAlpha = alpha;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            Color c = baseColors[i];
            c.a *= alpha;
            renderers[i].color = c;
        }
    }
}
