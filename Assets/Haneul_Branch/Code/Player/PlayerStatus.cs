using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStatus : MonoBehaviour
{
    [Header("스탯")]
    [SerializeField] private CharacterStats stats = new CharacterStats();

    private int currentHp;

    // ===== 외부 접근용 (파생 스탯은 CharacterStats가 계산) =====
    public CharacterStats Stats => stats;
    public int MaxHp => stats.MaxHp;
    public int CurrentHp => currentHp;
    public int CurrentSoul => stats.soul;
    public int MaxSoul => stats.MaxSoul;

    [Header("경험치")]
    [SerializeField] private int currentExp = 0;
    [SerializeField] private int maxExp = 100;

    [Header("레벨")]
    [SerializeField] private int level = 1;

    [Header("피격")]
    [SerializeField] private float invincibleTime = 0.5f;
    [Tooltip("무적 동안 스프라이트를 깜빡여 눈에 보이게 한다.\n" +
             "색이 아니라 렌더러 on/off를 토글하므로 은신(알파 조절)과 충돌하지 않는다.")]
    [SerializeField] private bool blinkWhileInvincible = true;
    [Tooltip("깜빡임 주기(초). 작을수록 빠르게 명멸한다")]
    [SerializeField] private float blinkInterval = 0.06f;
    [Tooltip("피격 시 카메라 흔들림 세기 (0.2 = 가벼운 타격 / 0.6 = 처형급). 0이면 없음")]
    [SerializeField] private float hitShake = 0.25f;

    [Header("사망")]
    [Tooltip("쓰러지는 걸 눈으로 따라갈 시간(초). 이 뒤에 결과 화면이 뜬다")]
    [SerializeField] private float deathHoldTime = 1.4f;
    [Tooltip("사망 순간의 시간 배율. 0.25면 4배 느려진다. 1이면 슬로우 없음")]
    [Range(0.05f, 1f)]
    [SerializeField] private float deathTimeScale = 0.25f;
    [SerializeField] private float deathShake = 0.55f;

    private bool isDead;
    public bool IsDead { get { return isDead; } }

    private bool isInvincible;
    public bool IsInvincible => isInvincible;

    private SpriteRenderer[] renderers;

    [Header("패링 반격 애니메이션")]
    [Tooltip("퍼펙트 패링 성공 시 재생할 반격 클립. 길이만큼 무적이 유지된다 " +
             "(애니메이터의 ParryCounter 트리거가 같은 클립을 재생하도록 맞춰 둘 것)")]
    [SerializeField] private AnimationClip parryCounterClip;

    [Header("패링 반격 범위")]
    [Tooltip("반격이 실제로 맞은 지점(공격자 위치) 기준 범위 피해 반경. 0이면 원래 대상만 맞는다.\n" +
             "하베스트 충격파(HarvestShockwaveEffect)와 같은 방식 — 처형 로직이 아니라 여기 직접 둔다.")]
    [SerializeField] private float counterAreaRadius = 1.8f;

    [Tooltip("범위 판정에 쓸 레이어. 비우면 Enemy 레이어를 자동으로 쓴다")]
    [SerializeField] private LayerMask counterAreaEnemyLayers;

    // 처형 충격파와 같은 이유로 재사용 — 반격마다 배열을 새로 잡지 않는다
    private static readonly Collider2D[] counterAreaBuffer = new Collider2D[16];

    private Animator anim;
    private PlayerMove move;

    // 패링 상태 (Parry 스킬이 BeginParry로 설정)
    private float parryPerfectEndTime = -1f;
    private float parryEndTime = -1f;
    private float parryPerfectMult = 0f;
    private float parryBlockedMult = 1f;
    private float parryCounterMultiplier = 0f;
    private float parryIframeDuration = 0f;

    // 반격 애니메이션의 "내려찍는" 프레임이 올 때까지 들고 있는 대상.
    // ParryCounterHit()이 불릴 때 실제로 데미지를 넣는다.
    private Enemy pendingCounterTarget;

    private enum ParryResult { None, Normal, Perfect }

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        move = GetComponent<PlayerMove>();

        // 디버그 표시(공격 박스 등)는 제외한다.
        // 안 거르면 무적 깜빡임이 끝나면서 전부 enabled=true로 되돌려
        // 꺼 둔 디버그 박스가 한 대 맞을 때마다 되살아난다.
        var all = GetComponentsInChildren<SpriteRenderer>(true);
        var keep = new List<SpriteRenderer>(all.Length);

        for (int i = 0; i < all.Length; i++)
            if (!DebugVisual.Owns(all[i])) keep.Add(all[i]);

        renderers = keep.ToArray();
    }

    private void Start()
    {
        currentHp = MaxHp;
    }

    // 피해를 받는다. 반환값: 실제로 피해가 적중했는지(넉백 여부 판단용). 퍼펙트 패링/무적이면 false.
    public bool TakeDamage(int damage, Enemy attacker = null)
    {
        if (isDead || isInvincible)
            return false;

        ParryResult parry = EvaluateParry();

        // 퍼펙트 패링: 완전 무효 + 반격 + 경직 + 짧은 무적
        if (parry == ParryResult.Perfect)
        {
            OnPerfectParry(attacker);
            return false; // 무효 → 넉백 X
        }

        // 일반 패링: 피해 감소
        float mult = (parry == ParryResult.Normal) ? parryBlockedMult : 1f;
        int raw = Mathf.Max(1, Mathf.RoundToInt(damage * mult));

        // 방어(비율 감산)로 피해 경감 — CharacterStats 공식 사용. 최소 1은 들어감.
        int taken = stats.CalcIncomingDamage(raw);

        currentHp -= taken;
        Debug.Log($"플레이어피격 : {taken} damage (원본 {damage}, 패링 x{mult}, 방어 {stats.Defense}). HP: {currentHp}/{MaxHp}");

        // 실제로 피해가 들어간 경우에만 (무적·퍼펙트 패링은 위에서 이미 빠져나갔다)
        if (hitShake > 0f) CameraShake.Shake(hitShake);

        if (currentHp <= 0)
            Die();
        else
            StartCoroutine(InvincibleCoroutine());

        return true;
    }

    // 퍼펙트 패링 성공 처리.
    // 데미지는 여기서 바로 넣지 않는다 — 반격 애니메이션의 "내려찍는" 프레임(ParryCounterHit)에서
    // 실제로 들어간다. 그래야 판정이 눈에 보이는 타이밍과 맞는다.
    private void OnPerfectParry(Enemy attacker)
    {
        PixelVfx.Play("ParryClang", transform.position + Vector3.up * 0.6f);

        bool canCounter = attacker != null && !attacker.isDead && parryCounterMultiplier > 0f
                        && anim != null && parryCounterClip != null;

        if (canCounter)
        {
            Debug.Log("[Parry] PERFECT! 반격 애니메이션 재생");
            pendingCounterTarget = attacker;
            anim.SetTrigger("ParryCounter");
            StartCoroutine(ParryCounterLock(parryCounterClip.length)); // 반격 애니가 도는 동안 무적 + 이동 불가
        }
        else
        {
            Debug.Log("[Parry] PERFECT! 경직 + 무적");
            if (attacker != null && !attacker.isDead) attacker.Stagger(); // 반격 없으면 경직만
            if (parryIframeDuration > 0f) StartCoroutine(InvincibleFor(parryIframeDuration));
        }
    }

    // 반격 스윙 중엔 캐릭터가 미끄러지듯 움직이면 안 되므로, 무적과 함께 이동도 같이 잠근다.
    // 처형의 isExecuting과 같은 잠금이라 서로 겹쳐도 안전하다(둘 다 끝나야 풀리는 게 아니라
    // 각자 자기 구간이 끝나면 그냥 false로 되돌리는 방식 — 패링 반격 중엔 처형이 불가능해 겹칠 일이 없다).
    private IEnumerator ParryCounterLock(float duration)
    {
        if (move != null) move.isExecuting = true;
        yield return InvincibleFor(duration);
        if (move != null) move.isExecuting = false;
    }

    // 반격 애니메이션 이벤트(내려찍는 프레임)에서 호출. 실제 데미지는 여기서 들어간다.
    public void ParryCounterHit()
    {
        if (pendingCounterTarget == null) return;

        Enemy target = pendingCounterTarget;
        pendingCounterTarget = null;
        if (target.isDead) return;

        bool isCrit;
        int counter = Mathf.Max(1, Mathf.RoundToInt(stats.RollPhysicalDamage(out isCrit) * parryCounterMultiplier));
        target.TakeDamage(counter, true, Color.yellow); // 반격 (+HitState 경직, 노란 데미지 숫자)

        if (counterAreaRadius > 0f) HitNearbyEnemies(target, counter);
    }

    // 반격이 찍은 자리 주변에도 같은 피해를 준다 — HarvestShockwaveEffect와 같은 방식.
    // 처형은 카드 효과라 어떤 카드를 가졌는지 몰라야 하지만, 반격은 패링 자체의 보상이라
    // 여기 직접 둔다.
    private void HitNearbyEnemies(Enemy excluded, int damage)
    {
        int mask = counterAreaEnemyLayers.value != 0 ? counterAreaEnemyLayers.value : LayerMask.GetMask("Enemy");
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(mask);

        int count = Physics2D.OverlapCircle(excluded.transform.position, counterAreaRadius, filter, counterAreaBuffer);

        // 같은 적이 콜라이더 여러 개(몸통·발밑)로 잡히므로 중복 타격을 막는다
        Enemy last = null;
        for (int i = 0; i < count; i++)
        {
            Collider2D col = counterAreaBuffer[i];
            if (col == null) continue;

            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy == null || enemy == last) continue;
            if (enemy == excluded || enemy.isDead) continue; // 원래 대상은 이미 맞았다

            enemy.TakeDamage(damage, true, Color.yellow);
            last = enemy;
        }

        ShockwaveRing.Spawn(excluded.transform.position, counterAreaRadius, Color.white);
    }

    // 패링 창 시작 (Parry 스킬에서 호출).
    public void BeginParry(float perfectWindow, float window, float perfectMult, float blockedMult,
                           float counterMultiplier, float iframeDuration)
    {
        float now = Time.time;
        parryPerfectEndTime = now + perfectWindow;
        parryEndTime = now + window;
        parryPerfectMult = perfectMult;
        parryBlockedMult = blockedMult;
        parryCounterMultiplier = counterMultiplier;
        parryIframeDuration = iframeDuration;
    }

    private ParryResult EvaluateParry()
    {
        float now = Time.time;
        if (now <= parryPerfectEndTime) return ParryResult.Perfect;
        if (now <= parryEndTime) return ParryResult.Normal;
        return ParryResult.None;
    }

    private IEnumerator InvincibleFor(float duration)
    {
        isInvincible = true;
        yield return Blink(duration);
        isInvincible = false;
    }

    // 무적 동안 스프라이트를 명멸시킨다.
    // 무적인지 아닌지 화면에서 안 보이면 플레이어는 그냥 "안 맞은 것"으로 착각한다.
    private IEnumerator Blink(float duration)
    {
        if (!blinkWhileInvincible || renderers == null || blinkInterval <= 0f)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        float elapsed = 0f;
        bool on = false;

        while (elapsed < duration)
        {
            SetRenderersEnabled(on);
            on = !on;

            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        SetRenderersEnabled(true);
    }

    private void SetRenderersEnabled(bool value)
    {
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = value;
    }

    // 소울 획득 (처형 시). CharacterStats.GainSoul이 최대치로 클램프.
    public void AddSoul(int amount)
    {
        if (amount <= 0) return;
        stats.GainSoul(amount);
        Debug.Log($"소울 획득: +{amount} → {stats.soul}/{MaxSoul}");
    }

    private IEnumerator InvincibleCoroutine()
    {
        isInvincible = true;
        yield return Blink(invincibleTime);
        isInvincible = false;
    }

    // 한 판의 끝. 입력을 끊고 쓰러지는 걸 보여 준 뒤 결과 화면으로 넘긴다.
    private void Die()
    {
        if (isDead) return;
        isDead = true;
        currentHp = 0;

        // 무적 깜빡임이 도중에 멈추면 스프라이트가 꺼진 채로 남는다.
        // 코루틴을 끊는 쪽이 먼저이므로 여기서 직접 되돌려 놓는다.
        StopAllCoroutines();
        SetRenderersEnabled(true);
        isInvincible = true;   // 시체가 더 맞고 넉백되지 않게

        LockControl();

        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.SetBool("Dead", true);

        if (deathShake > 0f) CameraShake.Shake(deathShake);

        StartCoroutine(DeathSequence());
    }

    // 플레이어를 조종하는 것들을 전부 끈다.
    // 컴포넌트를 끄는 방식이라 각자의 Update가 아예 안 돌고,
    // 진행 중이던 대시·차징이 사망 뒤에 마저 나가는 일이 없다.
    private void LockControl()
    {
        PlayerMove move = GetComponent<PlayerMove>();
        if (move != null) move.inputLocked = true;

        MonoBehaviour[] toStop =
        {
            GetComponent<PlayerCombat>(),
            GetComponent<SkillInputController>(),
        };

        for (int i = 0; i < toStop.Length; i++)
            if (toStop[i] != null) toStop[i].enabled = false;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null) body.velocity = Vector2.zero;
    }

    private IEnumerator DeathSequence()
    {
        // 죽는 순간을 느리게 보여 준다. 결과 화면이 곧바로 덮치면
        // 무엇에 맞아 죽었는지 확인할 틈이 없다.
        float previousScale = Time.timeScale;
        Time.timeScale = deathTimeScale;

        // 시간이 느려져 있으므로 실제 시간으로 잰다 — 안 그러면 4배 더 기다린다.
        float t = 0f;
        while (t < deathHoldTime)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = previousScale;   // 결과 화면이 0으로 다시 세운다
        GameOverScreen.Show();
    }
}
