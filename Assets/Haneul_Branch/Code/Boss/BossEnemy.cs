using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 보스 공통 베이스.
//
// 잡몹과 다른 점은 셋뿐이다.
//   · 체력이 줄면 페이즈가 바뀐다 (속도 · 공격 간격 · 쓸 수 있는 패턴이 달라진다)
//   · 공격이 한 종류가 아니라 여러 패턴 중에서 뽑힌다
//   · 페이즈가 바뀌는 순간 잠깐 멈추고 무적이 된다
//
// 나머지(예고 → 판정 → 회복, 추격, 피격, 체력바)는 전부 AttackEnemyBase 것을 그대로 쓴다.
// 그래서 새 보스를 만드는 일은 "패턴 몇 개 쓰고 페이즈 표 채우기"로 끝난다.
public class BossEnemy : AttackEnemyBase
{
    [Header("페이즈")]
    [Tooltip("위에서부터 1페이즈. 체력 비율이 enterAtHpRatio 이하가 되면 그 페이즈로 넘어간다.\n"
           + "비워 두면 페이즈 없이 패턴만 도는 보스가 된다")]
    [SerializeField] private List<BossPhase> phases = new List<BossPhase>();

    [Header("패턴")]
    [Tooltip("비워 두면 자기 자신과 자식에서 자동으로 모은다")]
    [SerializeField] private List<BossPattern> patterns = new List<BossPattern>();

    [Tooltip("직전에 쓴 패턴을 연달아 뽑지 않는다. 쓸 수 있는 패턴이 하나뿐이면 무시된다")]
    [SerializeField] private bool avoidRepeat = true;

    [Tooltip("쓸 수 있는 패턴이 하나도 없을 때 제자리에서 버티는 시간(초).\n"
           + "0으로 두면 공격 루틴이 곧바로 다시 돌아 프레임을 잡아먹는다")]
    [SerializeField] private float idlePatternFallback = 0.5f;

    [Header("맷집")]
    [Tooltip("공격 · 페이즈 전환 중에는 경직되지 않는다.\n"
           + "끄면 잡몹처럼 맞을 때마다 흔들려서, 플레이어가 계속 때리는 것만으로 보스를 묶어 둘 수 있다")]
    [SerializeField] private bool superArmor = true;

    [Header("페이즈 전환 연출")]
    [SerializeField] private float phaseShake = 0.6f;
    [Tooltip("전환 순간 띄울 이펙트 id. 비우면 안 띄운다")]
    [SerializeField] private string phaseVfxId = "SlashHit";

    [Tooltip("예고가 끝난 뒤 칼이 닿기까지 걸리는 시간의 첫 추정값(초).\n"
           + "실제로 한 번 재고 나면 잰 값으로 바뀐다")]
    [Min(0f)] [SerializeField] private float defaultHitDelay = 0.5f;

    [Tooltip("페이즈 전환 때 카메라를 얼마나 당길지 (1 = 그대로, 0.7 = 30% 확대)")]
    [Range(0.3f, 1f)] [SerializeField] private float phaseZoom = 0.72f;

    [Tooltip("이 시간(초)이 지나면 체력과 상관없이 마지막 페이즈(광폭)로 넘어간다. 0이면 안 쓴다")]
    [Min(0f)] [SerializeField] private float enrageAfterSeconds = 120f;

    private float combatStartTime = -1f;

    // ── 상태 ────────────────────────────────────────────
    public int PhaseIndex { get; private set; }
    public bool IsPhaseChanging { get; private set; }

    public float HpRatio
    {
        get { return maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f; }
    }

    public BossPhase CurrentPhase
    {
        get { return (phases != null && PhaseIndex < phases.Count) ? phases[PhaseIndex] : null; }
    }

    public int PhaseCount { get { return phases != null ? phases.Count : 0; } }

    // 체력바가 페이즈 경계선을 그릴 때 쓴다. 1페이즈의 시작(1.0)은 경계가 아니므로 뺀다.
    public float[] PhaseBoundaries
    {
        get
        {
            if (phases == null || phases.Count <= 1) return null;

            var list = new List<float>(phases.Count - 1);
            for (int i = 1; i < phases.Count; i++) list.Add(Mathf.Clamp01(phases[i].enterAtHpRatio));
            return list.ToArray();
        }
    }

    public event System.Action<BossEnemy, int> PhaseChanged;

    private BossPattern lastPattern;
    private BossPattern pending;          // 이번 공격에 쓸 패턴. 애니메이션 전에 미리 정해 둔다
    private bool hasAtkIndexParam;
    private readonly List<BossPattern> usable = new List<BossPattern>();

    // 페이즈 배수는 "원래 값"에 곱한다. 현재 값에 곱하면 전환할 때마다 누적돼 순식간에 0이 된다.
    private float baseMoveSpeed;
    private float baseAttackIdle;
    private float baseWarning;

    protected override void OnStart()
    {
        Grade = EnemyGrade.Boss;

        baseMoveSpeed  = moveSpeed;
        baseAttackIdle = attackIdleTime;
        baseWarning    = attackWarningDuration;

        CollectPatterns();
        SortPhases();

        // 애니메이터마다 파라미터가 다르다. 없는 파라미터에 값을 쓰면 경고가 쏟아지므로 한 번만 확인해 둔다.
        hasAtkIndexParam = false;
        if (animator != null)
            foreach (var p in animator.parameters)
                if (p.name == "atkIndex" && p.type == AnimatorControllerParameterType.Int) hasAtkIndexParam = true;

        PhaseIndex = 0;
        if (CurrentPhase != null) ApplyPhase(CurrentPhase);
    }

    private void CollectPatterns()
    {
        if (patterns == null) patterns = new List<BossPattern>();

        if (patterns.Count == 0)
            patterns.AddRange(GetComponentsInChildren<BossPattern>(true));

        for (int i = 0; i < patterns.Count; i++)
            if (patterns[i] != null) patterns[i].ResetCooldown();

        if (patterns.Count == 0)
            Debug.LogWarning("[BossEnemy] " + name + " 에 패턴이 하나도 없다. 공격하지 않는 보스가 된다", this);
    }

    // 체력이 높은 페이즈가 먼저 오도록 정렬한다.
    // 인스펙터에서 순서를 잘못 넣어도 조용히 이상하게 도는 것보다 낫다.
    private void SortPhases()
    {
        if (phases == null || phases.Count < 2) return;

        bool ordered = true;
        for (int i = 1; i < phases.Count; i++)
            if (phases[i].enterAtHpRatio > phases[i - 1].enterAtHpRatio) { ordered = false; break; }

        if (ordered) return;

        phases.Sort((a, b) => b.enterAtHpRatio.CompareTo(a.enterAtHpRatio));
        Debug.LogWarning("[BossEnemy] " + name + " 의 페이즈 순서를 체력 내림차순으로 다시 정렬했다", this);
    }

    // ─────────────────────────────────────────────
    // 페이즈
    // ─────────────────────────────────────────────

    protected override void Update()
    {
        base.Update();

        if (isDead || IsPhaseChanging || phases == null || phases.Count == 0) return;

        if (combatStartTime < 0f) combatStartTime = Time.time;

        int target = ResolvePhase(HpRatio);

        // 시간을 끌어 이기는 걸 막는다 — 정해진 시간이 지나면 마지막 페이즈로 밀어 넣는다
        if (enrageAfterSeconds > 0f && Time.time - combatStartTime >= enrageAfterSeconds)
            target = phases.Count - 1;

        // 공격 도중에는 넘기지 않는다. 패턴을 중간에 끊으면 히트박스가 켜진 채 남거나
        // 예고만 보여 주고 판정이 안 나가는 공격이 된다. 이번 공격이 끝나면 바로 넘어간다.
        if (target > PhaseIndex && !isAttacking)
            StartCoroutine(RunPhaseTransition(target));
    }

    // 한 방에 여러 단계가 깎였으면 중간 페이즈는 건너뛰고 지금 체력에 맞는 곳으로 간다.
    private int ResolvePhase(float ratio)
    {
        int result = 0;
        for (int i = 0; i < phases.Count; i++)
            if (ratio <= phases[i].enterAtHpRatio) result = i;
        return result;
    }

    private IEnumerator RunPhaseTransition(int target)
    {
        IsPhaseChanging = true;
        IsInvulnerable = true;

        StopMove();
        SetPositionLocked(true);

        PhaseIndex = target;
        BossPhase ph = phases[target];
        ApplyPhase(ph);

        if (phaseShake > 0f) CameraShake.Shake(phaseShake);
        if (!string.IsNullOrEmpty(phaseVfxId))
            PixelVfx.Play(phaseVfxId, transform.position + Vector3.up * 0.8f);
        if (!string.IsNullOrEmpty(ph.label))
            ToastManager.Show(ph.label, ToastManager.Kind.Warn);

        if (PhaseChanged != null) PhaseChanged(this, target);

        // 화면이 보스로 당겨지며 단계가 바뀐다 — 숫자만 바뀌는 게 아니라는 걸 몸으로 알린다
        StartCoroutine(RunPhaseCamera(ph.transitionDuration));

        float t = 0f;
        while (t < ph.transitionDuration && !isDead)
        {
            t += Time.deltaTime;
            yield return null;
        }

        SetPositionLocked(false);
        IsInvulnerable = false;
        IsPhaseChanging = false;
    }

    // 페이즈 전환 연출: 보스로 줌인 → 잠깐 멈춤 → 원래대로.
    // Room의 보스 처치 연출과 같은 방식으로 카메라를 잠시 빌려 쓴다.
    private IEnumerator RunPhaseCamera(float duration)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;

        CameraFollow follow = FindObjectOfType<CameraFollow>();
        float baseSize = cam.orthographicSize;
        Vector3 rest = cam.transform.position;
        Vector3 focus = new Vector3(transform.position.x, transform.position.y + 0.5f, rest.z);

        if (follow != null)
        {
            follow.Suspended = true;
            follow.OverridePosition = rest;
        }

        float zoomIn = Mathf.Min(0.35f, duration * 0.3f);
        float hold = Mathf.Max(0f, duration - zoomIn * 2f);

        yield return LerpCamera(cam, follow, 0f, 1f, zoomIn, baseSize, rest, focus);

        // 멈춘 채로 보여 주는 구간 — 이때 이펙트가 가장 잘 읽힌다
        float t = hold;
        while (t > 0f && !isDead) { t -= Time.deltaTime; yield return null; }

        yield return LerpCamera(cam, follow, 1f, 0f, zoomIn, baseSize, rest, focus);

        cam.orthographicSize = baseSize;
        if (follow != null) follow.Suspended = false;
    }

    private IEnumerator LerpCamera(Camera cam, CameraFollow follow, float from, float to,
        float duration, float baseSize, Vector3 rest, Vector3 focus)
    {
        float t = 0f;
        while (t < 1f && cam != null)
        {
            t += duration > 0f ? Time.deltaTime / duration : 1f;
            float k = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));

            cam.orthographicSize = Mathf.Lerp(baseSize, baseSize * phaseZoom, k);
            if (follow != null) follow.OverridePosition = Vector3.Lerp(rest, focus, k);
            yield return null;
        }
    }

    private void ApplyPhase(BossPhase ph)
    {
        moveSpeed              = baseMoveSpeed  * ph.moveSpeedMultiplier;
        attackIdleTime         = baseAttackIdle * ph.attackIntervalMultiplier;
        attackWarningDuration  = baseWarning    * ph.warningMultiplier;
    }

    // 보스의 처형 판정은 "마지막 칸"만 본다.
    // 전체 체력의 30%로 보면 마지막 페이즈가 시작되자마자 처형이 열려 그 구간을 통째로 건너뛴다.
    // 체력바가 4칸이면 마지막 한 칸(0~25%)을 하나의 체력으로 보고, 그 안에서 30%일 때 열린다.
    protected override float HarvestRatio
    {
        get
        {
            float lastSpan = LastPhaseSpan;
            if (lastSpan <= 0f) return base.HarvestRatio;

            // 마지막 칸 밖(아직 앞 페이즈)이면 1로 둬서 절대 안 열리게 한다
            float ratio = HpRatio / lastSpan;
            return ratio;
        }
    }

    // 마지막 페이즈가 차지하는 체력 비율 (4페이즈에 경계가 0.25면 0.25)
    public float LastPhaseSpan
    {
        get
        {
            if (phases == null || phases.Count == 0) return 1f;
            return Mathf.Clamp01(phases[phases.Count - 1].enterAtHpRatio);
        }
    }

    protected override bool CanBeStaggered
    {
        get { return !superArmor || (!isAttacking && !IsPhaseChanging); }
    }

    // 전환 중에는 공격을 시작하지 않는다.
    // CanAttack()이 이 값을 보므로 여기 한 곳만 막으면 된다.
    public override bool IsAttackAligned()
    {
        if (IsPhaseChanging) return false;
        return base.IsAttackAligned();
    }

    // ─────────────────────────────────────────────
    // 패턴이 쓰는 손잡이
    // ─────────────────────────────────────────────

    public GameObject HitBoxObject { get { return attackHitBox; } }

    // 판정을 다시 열어 준다. 한 패턴 안에서 여러 번 때리는 공격이 쓴다 —
    // EnemyHitBox는 이미 맞힌 대상을 기억하므로, 열어 주지 않으면 두 번째 타격이 그냥 통과한다.
    public void RearmHitBox()
    {
        if (attackHitBox == null) return;

        if (hitBox != null) hitBox.ResetHit();
        attackHitBox.SetActive(true);
    }

    public void CloseHitBox()
    {
        if (attackHitBox != null) attackHitBox.SetActive(false);
    }

    // 지금 플레이어 쪽 방향. 패턴 도중 플레이어가 움직였을 때 다시 겨누는 데 쓴다.
    public Vector2 AimAtPlayer()
    {
        return AimDirection();
    }

    // ─────────────────────────────────────────────
    // 패턴
    // ─────────────────────────────────────────────

    // 패턴은 애니메이션보다 먼저 정해진다. 그래서 잠금 여부도 여기서 정확히 답할 수 있다 —
    // 돌진처럼 스스로 움직이는 패턴만 잠금을 푼다.
    protected override bool LockPositionDuringActivePhase
    {
        get { return pending == null || !pending.MovesSelf; }
    }

    // 예고가 시작되는 시점. 이번에 쓸 패턴을 여기서 정한다 —
    // 무슨 공격인지 알아야 그 모양대로 위험지역을 그릴 수 있다.
    protected override void OnAttackWarning(Vector2 dir, float warningDuration)
    {
        pending = PickPattern();
        if (pending == null) return;

        pending.MarkUsed();
        lastPattern = pending;

        if (hasAtkIndexParam) animator.SetInteger("atkIndex", pending.AnimationIndex);

        // 예고가 끝나도 칼이 바로 닿지는 않는다 — 애니메이션이 한참 더 돈다.
        // 예고 시간만큼만 채우면 "다 찼는데 안 맞네?" 하다가 뒤늦게 맞는다.
        // 그래서 직전에 잰 (예고 끝 → 타격) 시간을 더해서 칸을 채운다.
        warnStartTime = Time.time;
        float fill = warningDuration + HitDelayOf(pending);

        activeZone = pending.ShowDanger(this, dir, fill);
        if (activeZone != null) activeZone.HoldUntilHit();
    }

    // 판정이 열리는 순간 — 표시를 채워 닫고, 이번에 걸린 시간을 다음 예고에 쓴다
    protected override void OnAttackHit()
    {
        if (activeZone != null)
        {
            activeZone.CompleteNow();
            activeZone = null;
        }

        if (pending != null && warnStartTime > 0f)
        {
            float measured = Time.time - warnStartTime - attackWarningDuration;
            hitDelays[pending] = Mathf.Clamp(measured, 0f, 1.5f);
        }
        warnStartTime = -1f;
    }

    // 패턴마다 "예고가 끝난 뒤 칼이 닿기까지" 걸리는 시간. 한 번 재고 나면 그 값을 쓴다.
    private float HitDelayOf(BossPattern p)
    {
        float d;
        if (p != null && hitDelays.TryGetValue(p, out d)) return d;
        if (p != null && p.HitDelay >= 0f) return p.HitDelay;   // 패턴이 적어 둔 값 (애니메이션 타격 시각)
        return defaultHitDelay;
    }

    private DangerZone activeZone;
    private float warnStartTime = -1f;
    private readonly Dictionary<BossPattern, float> hitDelays = new Dictionary<BossPattern, float>();

    // 애니메이션을 걸기 직전. 패턴은 예고 때 이미 정해 뒀다.
    protected override void OnAttackStart()
    {
    }

    // 히트박스 모양 그대로 위험지역을 띄운다. 패턴이 따로 그리지 않으면 이걸 쓴다.
    public DangerZone ShowHitBoxDanger(float duration)
    {
        if (attackHitBox == null) return null;

        var box = attackHitBox.GetComponent<BoxCollider2D>();
        if (box == null) return null;

        Vector3 ls = attackHitBox.transform.lossyScale;
        Vector2 size = new Vector2(box.size.x * Mathf.Abs(ls.x), box.size.y * Mathf.Abs(ls.y));
        float angle = attackHitBox.transform.eulerAngles.z;

        // 히트박스는 보스를 따라 도니 표시도 같이 따라다녀야 한다
        return DangerZone.Box(attackHitBox.transform.position, size, angle, duration, transform);
    }

    // 패턴이 잠깐 무적으로 만들 때 쓴다 (지하 잠행처럼 땅속에 있는 동안)
    public bool IsInvulnerableExternal
    {
        get { return IsInvulnerable; }
        set { IsInvulnerable = value; }
    }

    // 패턴이 스스로 피해를 굴릴 때 쓴다 (히트박스를 안 쓰는 장판·투사체 계열)
    public int AttackDamage { get { return attackDamage; } }

    public void DamagePlayerInRadius(Vector2 center, float radius)
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;

        Transform body = p.transform;
        if (Vector2.Distance(body.position, center) > radius) return;

        PlayerStatus status = p.GetComponent<PlayerStatus>();
        if (status == null) status = p.GetComponentInParent<PlayerStatus>();
        if (status != null) status.TakeDamage(attackDamage, this);
    }

    // 부하를 흡수해 회복한다 (영혼 흡수 패턴)
    public void HealBoss(int amount)
    {
        if (amount <= 0 || isDead) return;
        Heal(amount);
    }

    protected override IEnumerator AttackActivePhase(Vector2 dir)
    {
        BossPattern pick = pending;
        pending = null;

        if (pick == null)
        {
            // 쓸 패턴이 없을 때 즉시 끝내면 공격 루틴이 곧바로 다시 돌아 제자리걸음이 된다.
            rb.velocity = Vector2.zero;
            yield return new WaitForSeconds(Mathf.Max(0.05f, idlePatternFallback));
            yield break;
        }

        yield return pick.Run(this, dir);

        if (rb != null && !pick.MovesSelf) rb.velocity = Vector2.zero;
    }

    private BossPattern PickPattern()
    {
        usable.Clear();

        float total = 0f;
        for (int i = 0; i < patterns.Count; i++)
        {
            BossPattern p = patterns[i];
            if (p == null || !p.IsUsable(this)) continue;

            usable.Add(p);
            total += p.Weight;
        }

        // 같은 패턴이 연달아 나오면 보스가 단순해 보인다.
        // 단, 후보가 그것뿐이면 안 쓰는 것보다 반복하는 편이 낫다.
        if (avoidRepeat && usable.Count > 1 && lastPattern != null && usable.Contains(lastPattern))
        {
            total -= lastPattern.Weight;
            usable.Remove(lastPattern);
        }

        if (usable.Count == 0 || total <= 0f) return null;

        float roll = Random.value * total;
        for (int i = 0; i < usable.Count; i++)
        {
            roll -= usable[i].Weight;
            if (roll <= 0f) return usable[i];
        }

        return usable[usable.Count - 1];
    }
}
