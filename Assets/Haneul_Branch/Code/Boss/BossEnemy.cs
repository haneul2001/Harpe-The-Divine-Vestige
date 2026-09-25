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

        // 화면이 보스로 당겨지며 단계가 바뀐다 — 숫자만 바뀌는 게 아니라는 걸 몸으로 알린다.
        // 연출이 끝날 때까지 여기서 기다린다 — 따로 돌리면 연출 도중에 보스가 다시 움직인다.
        yield return RunPhaseShow(ph.transitionDuration);

        // 연출이 걷힌 자리에 부하가 올라온다. 어둠이 덮여 있는 동안 세우면
        // 커튼 뒤에서 나타나 "갑자기 거기 있었다"가 되므로 걷힌 뒤에 부른다.
        SpawnPhaseMinions();

        SetPositionLocked(false);
        IsInvulnerable = false;
        IsPhaseChanging = false;
    }

    [Header("페이즈 전환 연출")]
    [Tooltip("전환 동안 화면을 얼마나 어둡게 덮을지. 보스와 그 위 이펙트만 남는다")]
    [Range(0f, 1f)] [SerializeField] private float phaseDim = 0.85f;

    [Tooltip("보스에게 붙일 불 이펙트 id. 비우면 안 띄운다.\n"
           + "한 번 붙으면 연출이 끝나도 남는다 — 페이즈가 올라간 보스라는 표식이다")]
    [SerializeField] private string phaseFireVfxId = "BossPhaseFire";

    [Tooltip("불 이펙트 크기 (월드 단위). 첫 연출에서 이 크기로 붙는다")]
    [Min(0.5f)] [SerializeField] private float phaseFireSize = 3f;

    [Tooltip("연출을 한 번 더 볼 때마다 불길이 이만큼 커진다. 0.1이면 10%씩")]
    [Range(0f, 1f)] [SerializeField] private float phaseFireGrow = 0.1f;

    [Tooltip("연출을 한 번 더 볼 때마다 동작이 이만큼 빨라진다. 0.15면 15%씩.\n"
           + "공격만이 아니라 걷기·대기까지 전부 빨라진다 — 단계가 올랐다는 게 움직임에서 읽혀야 한다.\n"
           + "동작이 빨라지면 칼이 닿는 시각도 같이 당겨지므로 예고 길이도 함께 줄어든다")]
    [Range(0f, 1f)] [SerializeField] private float phaseAttackSpeedGrow = 0.15f;

    // 연출을 거칠 때마다 쌓이는 배수 (1 = 처음 그대로)
    private float phaseAttackSpeed = 1f;

    [Tooltip("전환 동안 보여 줄 동작 번호. 한 번 재생하고 마지막 자세에서 멈춘다")]
    [Min(0)] [SerializeField] private int phaseAnimation = 2;

    [Tooltip("전환이 끝날 때 불러낼 잡몹 수. 0이면 안 부른다.\n"
           + "프리팹·스폰 지점·체력은 소환 패턴(BossPatternSummon)의 설정을 그대로 쓴다")]
    [Min(0)] [SerializeField] private int phaseSpawnCount = 5;

    // 페이즈가 오를 때마다 부하를 한 무리 붙여 준다 — 단계가 올랐다는 걸 방 전체로 알린다.
    //
    // 소환 패턴의 설정을 빌려 쓴다. 여기서 프리팹을 따로 들고 있으면
    // 층이 올라갈 때 소환 패턴만 갱신하고 이쪽을 잊어 부하 종류가 어긋난다.
    private void SpawnPhaseMinions()
    {
        if (phaseSpawnCount <= 0 || isDead) return;

        BossPatternSummon summon = GetComponentInChildren<BossPatternSummon>(true);
        if (summon == null)
        {
            Debug.LogWarning("[BossEnemy] " + name + " 에 BossPatternSummon이 없어 페이즈 소환을 건너뛴다", this);
            return;
        }

        summon.SummonBurst(this, phaseSpawnCount);
    }

    [Tooltip("동작을 어느 지점에서 굳힐지 (1이 끝).\n"
           + "1로 두면 늦는다 — 공격 동작이 끝나면 스스로 대기·걷기로 넘어가므로 그 전에 세워야 한다")]
    [Range(0.5f, 1f)] [SerializeField] private float phaseAnimFreezeAt = 0.95f;

    // 페이즈 전환 연출.
    //
    // 시간을 멈추고, 보스만 남기고 화면을 어둡게 덮은 뒤, 칼을 땅에 박는 동작을
    // 한 번 재생해 그 자세에서 멈춘다. 그 위로 불이 오른다.
    // 줌아웃이 시작되면 어둠도 불도 같이 걷히고 시간이 다시 흐른다.
    //
    // 멈춘 동안에도 돌아야 하므로 이 구간은 전부 unscaled 시간으로 센다.
    private IEnumerator RunPhaseShow(float duration)
    {
        Camera cam = Camera.main;

        float savedTimeScale = Time.timeScale;
        AnimatorUpdateMode savedMode = animator != null ? animator.updateMode : AnimatorUpdateMode.Normal;
        float savedSpeed = animator != null ? animator.speed : 1f;

        SpriteRenderer curtain = MakeCurtain();
        Time.timeScale = 0f;

        if (animator != null)
        {
            // 멈춘 시간에도 동작이 돌아야 한다
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.speed = 1f;

            // 끊긴 공격·피격이 남겨 둔 트리거를 비운다.
            // 안 그러면 연출 도중에 그게 터져 동작이 다시 돌거나 피격 자세가 튀어나온다.
            animator.ResetTrigger("attack");
            animator.ResetTrigger("hit");

            // 걷기로 넘어가지 않게 추격 상태도 내려 둔다
            animator.SetBool("isFollow", false);

            PlayAttackAnimation(phaseAnimation);
        }

        LightPhaseFire();

        float zoomIn = Mathf.Min(0.35f, duration * 0.3f);
        float hold = Mathf.Max(0f, duration - zoomIn * 2f);

        // 원래 화면 크기와 자리는 여기서 한 번만 잡아 둔다
        float baseSize = cam != null ? cam.orthographicSize : 0f;
        Vector3 rest = cam != null ? cam.transform.position : Vector3.zero;

        // ① 줌인 — 어둠도 같이 깔린다
        yield return PhaseCamera(cam, 0f, 1f, zoomIn, curtain, baseSize, rest);

        // ② 멈춘 채로 보여 주는 구간. 동작이 끝나면 그 자세로 굳는다
        // 동작이 끝날 때까지 기다린다. 시간만 재고 끊으면 동작 도중에 연출이 끝나
        // "끝났는데 공격이 한 번 더 나온다"처럼 보인다.
        // 설정한 시간은 최소값이고, 동작이 더 길면 그쪽을 기다린다.
        float elapsed = 0f;
        float limit = Mathf.Max(hold, 1f) * 3f;     // 클립이 영영 안 끝나도 갇히지 않게
        while (!isDead)
        {
            elapsed += Time.unscaledDeltaTime;

            // 끝나기 직전에 굳힌다.
            //
            // 정확히 1.0에서 멈추려 하면 늦는다 — 공격 상태는 끝나면 스스로 대기/걷기로 넘어가는데,
            // 그 전이가 먼저 걸려 버려서 "아직 연출 중인데 보스가 걸어간다"가 된다.
            // 속도를 0으로 두면 애니메이터가 아예 안 굴러가므로 전이도, 피격 자세도 끼어들지 못한다.
            bool animDone = animator == null
                || animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= phaseAnimFreezeAt;

            if (animDone && animator != null && animator.speed > 0f) animator.speed = 0f;

            if ((animDone && elapsed >= hold) || elapsed >= limit) break;

            HoldCurtain(curtain, cam, phaseDim);
            yield return null;
        }

        // ③ 줌아웃 — 어둠이 걷히고 시간이 돌아온다.
        // 자세는 여기서도 굳은 채로 둔다. 줌아웃이 시작되자마자 풀면
        // 연출이 아직 화면에 있는데 보스가 대기·걷기 자세로 돌아가 버린다.
        yield return PhaseCamera(cam, 1f, 0f, zoomIn, curtain, baseSize, rest);

        Time.timeScale = savedTimeScale;
        if (animator != null)
        {
            animator.speed = savedSpeed;       // 연출이 다 끝난 뒤에야 자세를 푼다
            animator.updateMode = savedMode;
            animator.ResetTrigger("attack");   // 연출 직후 공격·피격이 튀어나오지 않게
            animator.ResetTrigger("hit");
        }
        if (curtain != null) Destroy(curtain.gameObject);

        // 연출 직후 한 번은 다른 동작을 쓰게 한다
        avoidPhaseAnimNext = true;

        // 연출을 한 번 볼 때마다 빨라진다 — 단계가 올랐다는 걸 몸으로 알린다.
        // 공격만이 아니라 걷기·대기까지 같이 올린다.
        phaseAttackSpeed *= 1f + phaseAttackSpeedGrow;
        ApplyAnimSpeed();
    }

    private bool avoidPhaseAnimNext;
    private PixelVfx phaseFire;

    // 페이즈를 넘긴 보스는 계속 타오른다. 연출이 끝나도 끄지 않는다 —
    // 한 번 올라간 단계를 화면에서 계속 알려 주는 표식이다.
    private void LightPhaseFire()
    {
        if (string.IsNullOrEmpty(phaseFireVfxId)) return;

        // 이미 타는 중이면 새로 붙이지 않고 키운다 — 페이즈가 오를수록 불길이 커진다
        if (phaseFire != null)
        {
            phaseFire.transform.localScale *= 1f + phaseFireGrow;
            return;
        }

        phaseFire = PixelVfx.PlayStretched(phaseFireVfxId, transform.position,
            0f, Vector2.one * phaseFireSize);
        if (phaseFire == null) return;

        phaseFire.Follow(transform);

        // 보스 뒤에서 타오르게 한다. 앞에 두면 불이 보스를 가려
        // "누가 페이즈를 넘겼는지"가 안 보인다.
        foreach (var r in phaseFire.GetComponentsInChildren<Renderer>(true))
        {
            r.sortingLayerID = spriteRenderer.sortingLayerID;
            r.sortingOrder = spriteRenderer.sortingOrder - 1;
        }

        foreach (var psys in phaseFire.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = psys.main;

            // 연출 동안 시간이 멈추므로, 멈춘 시간에도 타오르게 해 둔다
            main.useUnscaledTime = true;

            // 원본은 한 번 타고 꺼지는 이펙트다. 계속 타야 하므로 반복으로 바꾼다 —
            // 이것만 빠지면 연출이 끝나기도 전에 불이 사그라든다.
            main.loop = true;
            psys.Play();
        }
    }

    private void StopPhaseFire()
    {
        if (phaseFire == null) return;
        phaseFire.Stop();
        phaseFire = null;
    }

    // 시체가 계속 타고 있으면 안 된다
    private void OnEnable()
    {
        Died += ClearPhaseFire;
    }

    private void OnDisable()
    {
        Died -= ClearPhaseFire;
        StopPhaseFire();
    }

    private void ClearPhaseFire(Enemy _)
    {
        StopPhaseFire();
    }

    // 보스 바로 아래 칸에 까는 검은 막. 보스와 그 위 이펙트만 남는다
    private SpriteRenderer MakeCurtain()
    {
        if (spriteRenderer == null) return null;

        var go = new GameObject("BossPhaseCurtain");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CurtainSprite();
        sr.color = new Color(0f, 0f, 0f, 0f);
        sr.sortingLayerID = spriteRenderer.sortingLayerID;
        sr.sortingOrder = spriteRenderer.sortingOrder - 2;   // 불(-1)보다도 뒤
        return sr;
    }

    private void HoldCurtain(SpriteRenderer curtain, Camera cam, float alpha)
    {
        if (curtain == null || cam == null) return;

        // 카메라가 움직이고 줌이 변하므로 매 프레임 화면을 다시 덮는다
        Vector3 at = cam.transform.position;
        curtain.transform.position = new Vector3(at.x, at.y, transform.position.z);

        float h = cam.orthographicSize * 2f * 1.3f;
        curtain.transform.localScale = new Vector3(h * cam.aspect, h, 1f);
        curtain.color = new Color(0f, 0f, 0f, alpha);
    }

    private static Sprite curtainSprite;

    private static Sprite CurtainSprite()
    {
        if (curtainSprite != null) return curtainSprite;

        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave;
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();

        // ppu를 폭과 같게 두면 스프라이트 한 장이 정확히 1 월드 단위가 된다
        curtainSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        curtainSprite.hideFlags = HideFlags.HideAndDontSave;
        return curtainSprite;
    }

    // 기준 크기와 제자리는 반드시 밖에서 받는다.
    // 함수 안에서 다시 재면 줌아웃할 때 "이미 당겨진 값"을 원래 크기로 착각해,
    // 화면이 원래대로 돌아가지 못하고 당겨진 채로 굳는다.
    private IEnumerator PhaseCamera(Camera cam, float from, float to, float duration,
        SpriteRenderer curtain, float baseSize, Vector3 rest)
    {
        CameraFollow follow = FindObjectOfType<CameraFollow>();
        if (cam == null) yield break;

        Vector3 focus = new Vector3(transform.position.x, transform.position.y + 0.5f, rest.z);

        bool zoomingIn = to > from;
        if (zoomingIn && follow != null)
        {
            follow.Suspended = true;
            follow.OverridePosition = rest;
        }

        float t = 0f;
        while (t < 1f && cam != null)
        {
            t += duration > 0f ? Time.unscaledDeltaTime / duration : 1f;
            float k = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));

            cam.orthographicSize = Mathf.Lerp(baseSize, baseSize * phaseZoom, k);
            if (follow != null) follow.OverridePosition = Vector3.Lerp(rest, focus, k);

            HoldCurtain(curtain, cam, phaseDim * k);
            yield return null;
        }

        if (!zoomingIn)
        {
            cam.orthographicSize = baseSize;
            if (follow != null) follow.Suspended = false;
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

    // 이번에 나가는 공격. 고르는 순간 PatternChosen이 울린다
    public BossPattern ActivePattern { get { return pending; } }
    public event System.Action<BossPattern> PatternChosen;

    // 예고가 시작되는 시점. 이번에 쓸 패턴을 여기서 정한다 —
    // 무슨 공격인지 알아야 그 모양대로 위험지역을 그릴 수 있다.
    protected override void OnAttackWarning(Vector2 dir, float warningDuration)
    {
        pending = PickPattern();
        hasSlashArea = false;

        if (pending == null)
        {
            // 이름표가 직전 패턴을 그대로 달고 있으면, 판정 없는 헛스윙이
            // 그 패턴의 짓으로 읽힌다. 아무것도 안 고른 것도 알려 준다.
            if (PatternChosen != null) PatternChosen(null);
            return;
        }

        pending.MarkUsed();
        lastPattern = pending;

        // 무슨 공격이 나가는지 알리는 자리. 디버그 표시가 이걸 듣는다 —
        // 예고가 시작되는 순간이라 "표시가 뜰 때 이름도 뜬다"가 지켜진다.
        if (PatternChosen != null) PatternChosen(pending);

        if (hasAtkIndexParam) animator.SetInteger("atkIndex", pending.AnimationIndex);

        // 예고가 끝나도 칼이 바로 닿지는 않는다 — 애니메이션이 한참 더 돈다.
        // 예고 시간만큼만 채우면 "다 찼는데 안 맞네?" 하다가 뒤늦게 맞는다.
        // 그래서 직전에 잰 (예고 끝 → 타격) 시간을 더해서 칸을 채운다.
        warnStartTime = Time.time;
        float fill = warningDuration + HitDelayOf(pending);

        // 베기 그림이 덮을 칸을 여기서 정해 둔다. 표시가 사라진 뒤에 베는 패턴도 있어서
        // 표시 오브젝트를 붙들고 있으면 안 된다.
        DangerShape shape = pending.SlashShape(this);
        if (shape.Valid && !shape.circle && shape.count <= 1)
        {
            hasSlashArea = true;

            // 가로 길이는 예고한 부채꼴 길이에 그대로 맞춘다 — 검기가 예고 끝에서 끊겨야
            // "여기까지 벤다"가 읽힌다. 키우는 건 두께뿐이다.
            slashAreaSize = new Vector2(shape.size.x, shape.size.y * slashOverdraw);
            slashAreaForward = shape.size.x * 0.5f + shape.size.x * slashPush;
            slashAreaLift = shape.size.y * slashLift;
            slashAreaLift = shape.size.y * slashLift;
            slashAreaAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        activeZone = pending.ShowDanger(this, dir, fill);
        if (activeZone != null)
        {
            // 표시는 "칼을 휘두르기 시작하기 전"에 다 차야 한다.
            // 예고가 끝나는 순간 공격 애니메이션이 걸리므로 그보다 한 박자 앞이 기준이다.
            activeZone.HoldUntilHit().FillIn(Mathf.Max(0.12f, warningDuration - 0.1f));
        }
    }

    // 패턴이 베기 그림을 띄울 시점을 직접 정할 때 쓴다 (돌진처럼 나간 뒤에 베는 공격).
    // 베기 자리·크기 계산은 기본 경로와 같은 것을 쓰므로 예고한 칸과 어긋나지 않는다.
    public void PlaySlashNow(Vector2 dir)
    {
        SpawnSlash(dir);
    }

    // 근접 판정을 안 쓰는 패턴이면 상자를 열지 않는다.
    //
    // 쓸 패턴을 못 골랐을 때(pending이 null)도 열지 않는다.
    // 예전에는 이 경우 그냥 열려서, 예고도 없고 이름표는 직전 패턴이 남은 채
    // 보스가 맨손으로 때리고 있었다 — "판정 없는 패턴인데 왜 맞지?"의 정체다.
    protected override bool OpensHitBox
    {
        get { return pending != null && pending.UsesHitBox; }
    }

    // 회전 베기처럼 자기 그림을 직접 그리는 패턴은 기본 베기를 건너뛴다
    protected override bool ShouldSpawnSlash
    {
        get { return pending == null || !pending.DrawsOwnSlash; }
    }

    // 돌진 계열이면 베기 그림도 보스를 따라 앞으로 나간다
    protected override bool SlashFollowsOwner
    {
        get { return pending != null && pending.MovesSelf; }
    }

    // 보스는 예고로 보여 준 칸이 곧 공격 범위다. 베기 그림도 거기까지 늘린다 —
    // 예고와 그림이 같은 자리를 덮어야 "빨간 칸 밖이면 안전하다"를 믿을 수 있다.
    // 원형 예고(흡수 등)는 늘릴 방향이 없으니 기본 방식에 맡긴다.
    protected override bool TryGetSlashArea(Vector2 dir, out Vector3 center, out float angle, out Vector2 size)
    {
        center = Vector3.zero;
        angle = slashAreaAngle;
        size = slashAreaSize;

        if (!hasSlashArea) return false;

        // 방향은 지금 베는 쪽을 쓴다.
        // 예고 때 기억해 둔 각도를 그대로 쓰면, 좌우로 번갈아 치는 연격에서
        // 몸은 오른쪽을 치는데 검기만 계속 처음 방향으로 나간다.
        if (dir.sqrMagnitude > 0.0001f) angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // 자리는 표시가 아니라 "지금 보스 위치"에서 다시 잡는다.
        // 돌진처럼 벤 시점이 늦은 공격은 표시가 이미 사라진 뒤라, 기억해 둔 좌표를 쓰면
        // 보스는 저만치 나가 있는데 베기만 출발 자리에 남는다.
        Vector2 d = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        center = (Vector2)transform.position + d * slashAreaForward + Vector2.up * slashAreaLift;
        return true;
    }

    [Tooltip("베기 그림을 예고 칸보다 이 배로 키운다. 1.2면 20% 크게.\n"
           + "그림 > 예고 > 실제 판정 순으로 조금씩 작아져야 '보이는 것보다 덜 맞는다'가 된다")]
    [Range(1f, 2f)] [SerializeField] private float slashOverdraw = 1.2f;

    [Tooltip("베기 그림을 위로 올리는 정도. 판정 높이에 대한 비율이다.\n"
           + "예고 칸은 바닥에 깔리지만 칼은 공중에서 지나간다 — 발밑에 그리면 절반이 바닥에 묻힌다")]
    [Range(0f, 1.5f)] [SerializeField] private float slashLift = 0.45f;

    [Tooltip("베기 그림을 앞으로 미는 정도. 예고 길이에 대한 비율이다.\n"
           + "0이면 발밑에서 예고 끝까지 정확히 채운다 — 길이를 예고에 맞추므로 보통 0이 맞다")]
    [Range(0f, 1f)] [SerializeField] private float slashPush = 0f;

    public float SlashOverdraw { get { return slashOverdraw; } }

    private bool hasSlashArea;
    private Vector2 slashAreaSize;
    private float slashAreaAngle;
    private float slashAreaForward;
    private float slashAreaLift;

    // 판정이 열리는 순간 — 표시를 채워 닫고, 이번에 걸린 시간을 다음 예고에 쓴다
    protected override void OnAttackHit()
    {
        // 지금 어느 공격 동작을 돌고 있는지 붙잡아 둔다. 연격이 이걸 되감아 다시 휘두른다 —
        // 이 시점은 타격 이벤트가 울린 직후라 반드시 공격 상태다.
        if (animator != null) attackStateHash = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;

        if (activeZone != null)
        {
            activeZone.CompleteNow();
            activeZone = null;
        }

        if (pending != null && warnStartTime > 0f)
        {
            float measured = Mathf.Clamp(Time.time - warnStartTime - attackWarningDuration, 0.1f, 1.5f);

            // 가장 빨랐던 때를 기준으로 남긴다. 늦게 들어온 한 번을 믿으면 다음 예고가 너무 느려지고,
            // 그러면 덜 찬 채로 공격이 나간다 — 어차피 틀릴 거라면 일찍 차는 쪽으로 틀리는 게 안전하다.
            hitDelays[pending] = Mathf.Min(HitDelayOf(pending), measured);
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

    // 한 패턴 안에서 여러 번 휘두르는 공격(연격)이 동작을 처음부터 다시 돌릴 때 쓴다.
    //
    // 트리거를 다시 넣는 방법은 안 통한다 — AnyState 전이가 "자기 자신으로는 못 간다"로 잡혀 있어
    // 이미 그 상태에 있으면 아무 일도 일어나지 않는다. 그래서 상태를 직접 0초로 되감는다.
    public void ReplayAttackAnimation()
    {
        if (animator == null || attackStateHash == 0) return;
        animator.Play(attackStateHash, 0, 0f);
    }

    // 연격이 타격마다 다른 동작을 쓰고 싶을 때. 번호는 패턴의 공격 번호와 같은 뜻이다.
    //
    // 트리거로 넘기지 않고 상태를 직접 재생한다 — 트리거는 다음 애니메이터 갱신까지 미뤄지고,
    // 같은 번호가 연속으로 오면 "자기 자신으로 전이 금지"에 걸려 아무 일도 일어나지 않는다.
    public void PlayAttackAnimation(int index)
    {
        if (animator == null) return;

        if (hasAtkIndexParam) animator.SetInteger("atkIndex", index);

        if (attackStateNames != null && index >= 0 && index < attackStateNames.Length
            && !string.IsNullOrEmpty(attackStateNames[index]))
        {
            animator.Play(attackStateNames[index], 0, 0f);
            return;
        }

        ReplayAttackAnimation();   // 이름을 모르면 지금 동작을 되감는 것으로 갈음한다
    }

    [Tooltip("공격 번호별 애니메이터 상태 이름. 연격이 동작을 직접 바꿔 걸 때 쓴다.\n"
           + "비워 두면 같은 동작을 되감기만 한다")]
    [SerializeField] private string[] attackStateNames = { "Atk1", "Atk2", "Atk3" };

    [Tooltip("공격 번호별 타격 이벤트 시각(초). 연격 중간 타격의 예고 길이를 잡는 데 쓴다.\n"
           + "실제 타격은 애니메이션 이벤트를 기다리므로, 이 값은 예고 칸이 차는 속도에만 쓰인다")]
    [SerializeField] private float[] attackHitTimes = { 0.8f, 0.4f, 1.4f };

    public float AttackHitTimeOf(int index)
    {
        float at = defaultHitDelay;
        if (attackHitTimes != null && index >= 0 && index < attackHitTimes.Length && attackHitTimes[index] > 0f)
            at = attackHitTimes[index];

        // 동작이 빨라진 만큼 칼도 일찍 닿는다. 안 나누면 예고가 타격보다 길어져
        // "다 차기 전에 맞았다"로 돌아간다.
        return at / Mathf.Max(0.1f, phaseAttackSpeed);
    }

    // 연격 중간 타격의 예고. 첫 타의 예고와 같은 규칙으로 차오르고, 실제 타격 때 닫힌다.
    public DangerZone ShowComboDanger(Vector2 dir, float untilHit)
    {
        DangerZone zone = ShowHitBoxDanger(untilHit, dir);
        if (zone != null) zone.HoldUntilHit().FillIn(Mathf.Max(0.1f, untilHit * 0.75f));
        return zone;
    }

    private int attackStateHash;
    private DangerZone activeZone;
    private float warnStartTime = -1f;
    private readonly Dictionary<BossPattern, float> hitDelays = new Dictionary<BossPattern, float>();

    // 페이즈가 오른 만큼 모든 동작을 빠르게 돌린다.
    // 공격할 때만 올리면 걷기·대기는 그대로라, 화면에서는 "빨라졌다"가 거의 안 읽힌다.
    private void ApplyAnimSpeed()
    {
        if (animator != null) animator.speed = phaseAttackSpeed;
    }

    // 애니메이션을 걸기 직전. 패턴은 예고 때 이미 정해 뒀다.
    // 연출이 속도를 건드렸다 돌아오는 경우가 있어 여기서 한 번 더 맞춘다.
    protected override void OnAttackStart()
    {
        ApplyAnimSpeed();
    }

    protected override void OnAttackFinally()
    {
        ApplyAnimSpeed();
    }

    // 히트박스 모양 그대로 위험지역을 띄운다. 패턴이 따로 그리지 않으면 이걸 쓴다.
    //
    // 자리는 히트박스의 월드 좌표가 아니라 "발밑에서 겨눈 쪽으로" 잡는다.
    // 보스는 왼쪽을 볼 때 Y축으로 180도 돌아가는데, 그 상태의 자식 회전값을 읽으면
    // 대각선 공격에서 상자가 엉뚱한 쪽을 가리킨다.
    public DangerZone ShowHitBoxDanger(float duration, Vector2 dir)
    {
        if (attackHitBox == null) return null;

        // 크기는 패턴 쪽 정의를 그대로 쓴다 — 툴이 그리는 범위와 같은 값이어야 한다
        DangerShape shape = BossPattern.HitBoxShape(this);
        if (!shape.Valid) return null;

        if (dir.sqrMagnitude < 0.0001f) dir = IsFacingRight ? Vector2.right : Vector2.left;
        dir.Normalize();

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // 꼭짓점이 발밑, 벌어지는 쪽이 겨눈 방향 — 베기 이펙트가 덮는 모양 그대로다
        DangerShape fan = DangerShape.SectorFromBox(shape.size, DangerOrigin.Boss);
        return DangerZone.Sector(transform.position, fan.Radius, fan.halfAngle, angle, duration, transform);
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

        // 보여 준 원보다 실제 판정은 조금 작다 — 가장자리에서 아슬아슬하게 피한 것이 빗나가 준다
        Transform body = p.transform;
        if (Vector2.Distance(body.position, center) > ShrinkRadius(radius)) return;

        PlayerStatus status = p.GetComponent<PlayerStatus>();
        if (status == null) status = p.GetComponentInParent<PlayerStatus>();
        if (status != null) status.TakeDamage(attackDamage, this);
    }

    // 상자 모양으로 피해를 굴린다. 히트박스를 안 쓰고 자기 칸을 직접 그리는 베기용.
    // 보여 준 칸보다 판정이 조금 작은 것은 반경 피해와 같은 규칙이다.
    public void DamagePlayerInBox(Vector2 center, Vector2 size, float angleDeg)
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;

        Vector2 half = ShrinkSize(size) * 0.5f;

        // 상자를 기울인 만큼 반대로 돌려 놓고 축에 맞춰 재면 회전한 사각형 판정이 된다
        Vector2 offset = (Vector2)p.transform.position - center;
        float rad = -angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        var local = new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);

        if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y) return;

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

        // 페이즈 연출 직후 한 번은 다른 동작을 쓴다.
        // 연출이 그 동작으로 끝났는데 곧바로 같은 동작이 또 나오면,
        // 연출이 덜 끝났다가 다시 튀어나온 것처럼 보여 버그로 읽힌다.
        if (avoidPhaseAnimNext)
        {
            for (int i = usable.Count - 1; i >= 0; i--)
            {
                if (usable[i].AnimationIndex != phaseAnimation) continue;
                if (usable.Count <= 1) break;      // 그것뿐이면 어쩔 수 없다

                total -= usable[i].Weight;
                usable.RemoveAt(i);
            }
            avoidPhaseAnimNext = false;
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
