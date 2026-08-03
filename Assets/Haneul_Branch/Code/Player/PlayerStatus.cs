using System.Collections;
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

    private bool isInvincible;
    public bool IsInvincible => isInvincible;

    private SpriteRenderer[] renderers;

    // 패링 상태 (Parry 스킬이 BeginParry로 설정)
    private float parryPerfectEndTime = -1f;
    private float parryEndTime = -1f;
    private float parryPerfectMult = 0f;
    private float parryBlockedMult = 1f;
    private float parryReflectMultiplier = 0f;
    private float parryIframeDuration = 0f;

    private enum ParryResult { None, Normal, Perfect }

    private void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void Start()
    {
        currentHp = MaxHp;
    }

    // 피해를 받는다. 반환값: 실제로 피해가 적중했는지(넉백 여부 판단용). 퍼펙트 패링/무적이면 false.
    public bool TakeDamage(int damage, Enemy attacker = null)
    {
        if (isInvincible)
            return false;

        ParryResult parry = EvaluateParry();

        // 퍼펙트 패링: 완전 무효 + 반사 + 경직 + 짧은 무적
        if (parry == ParryResult.Perfect)
        {
            OnPerfectParry(damage, attacker);
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

    // 퍼펙트 패링 성공 처리
    private void OnPerfectParry(int incomingDamage, Enemy attacker)
    {
        Debug.Log("[Parry] PERFECT! 반사 + 경직 + 무적");

        if (attacker != null && !attacker.isDead)
        {
            if (parryReflectMultiplier > 0f)
            {
                int reflect = Mathf.Max(1, Mathf.RoundToInt(incomingDamage * parryReflectMultiplier));
                attacker.TakeDamage(reflect, true); // 반사 (+HitState 경직, 데미지 숫자 표시)
            }
            else
            {
                attacker.Stagger(); // 반사 없으면 경직만
            }
        }

        if (parryIframeDuration > 0f)
            StartCoroutine(InvincibleFor(parryIframeDuration));
    }

    // 패링 창 시작 (Parry 스킬에서 호출).
    public void BeginParry(float perfectWindow, float window, float perfectMult, float blockedMult,
                           float reflectMultiplier, float iframeDuration)
    {
        float now = Time.time;
        parryPerfectEndTime = now + perfectWindow;
        parryEndTime = now + window;
        parryPerfectMult = perfectMult;
        parryBlockedMult = blockedMult;
        parryReflectMultiplier = reflectMultiplier;
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

    private void Die()
    {
        Debug.Log("플레이어 사망");
        // 사망 처리 (예: 애니메이션, 게임 오버 화면 등)
    }
}
