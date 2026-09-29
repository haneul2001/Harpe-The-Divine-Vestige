using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 2층 보스 — 밤의 드래곤.
//
// 지상에서는 다른 보스처럼 패턴(할퀴기·브레스·화염구)을 뽑아 쓴다. 다른 점은 페이즈 전환이다 —
// 연출이 끝나면 날아올라 공중전을 치른다.
//
//   · 날아 있는 동안은 일반 공격이 안 들어간다. 패링 반격만 들어간다
//   · 공중 패턴(급강하·화염구·마법진·불비)의 "떨어지는 순간"은 전부 받아칠 수 있다
//   · 2번 받아치면 추락해서 잠깐 기절한다 (이때 받는 피해 1.5배)
//   · 못 받아쳐도 정해진 시간이 지나면 스스로 내려온다
//
// 그림(Body)은 자식이라 공중에서는 그림만 위로 올라가고, 뿌리(=그림자 자리)는 바닥에 남는다.
// 그래서 공중에서의 "위치"는 늘 바닥의 그림자 자리이고, 예고 원도 그 기준으로 깔린다.
public class DragonBoss : BossEnemy
{
    [Header("용 — 몸")]
    [Tooltip("그림이 붙은 자식. 공중에서는 이것만 위로 올라간다")]
    [SerializeField] private Transform body;

    [Tooltip("그림자 그림 (방 바닥에 따로 깔린다)")]
    [SerializeField] private Sprite shadowSprite;
    [SerializeField] private Vector2 shadowSize = new Vector2(3.6f, 1.0f);

    [Tooltip("지상에서 브레스·화염구가 나오는 입 자리 (오른쪽을 볼 때, 발밑 기준)")]
    [SerializeField] private Vector2 mouthOffset = new Vector2(2.65f, 1.4f);
    [Tooltip("공중에서 입 자리 (그림 기준)")]
    [SerializeField] private Vector2 flyMouthOffset = new Vector2(2.0f, 1.85f);
    [Tooltip("브레스 부채꼴의 꼭짓점 — 입 바로 아래 바닥")]
    [SerializeField] private float groundMouthForward = 2.2f;

    [Header("무대")]
    [Tooltip("방 가장자리에서 이만큼 안쪽까지만 쓴다 (좌우, 아래, 위). 위쪽은 벽 앞면이 3줄이라 넉넉히 뺀다")]
    [SerializeField] private Vector4 arenaMargin = new Vector4(1.5f, 1.5f, 1.3f, 4.2f);

    [Header("비행")]
    [Tooltip("1배 크기 기준. 실제 높이는 몸 크기 배율을 곱한다")]
    [SerializeField] private float flyHeight = 2.3f;
    [SerializeField] private float ascendTime = 0.8f;
    [SerializeField] private float landTime = 0.7f;
    [SerializeField] private float flySpeed = 4.5f;
    [Tooltip("이 시간 안에 못 떨어뜨리면 스스로 내려온다")]
    [SerializeField] private float flightMaxDuration = 20f;
    [SerializeField] private int parriesToFall = 5;
    [SerializeField] private float stunDuration = 3f;
    [SerializeField] private float stunDamageMultiplier = 1.5f;
    [Tooltip("공중 패턴 사이 쉬는 시간. 두 번째 비행은 더 짧다")]
    [SerializeField] private float patternGap = 1.1f;
    [SerializeField] private float secondFlightGap = 0.75f;
    [Tooltip("공중에 있을 때 그리기 순서. 방의 모든 것 위에 그린다")]
    [SerializeField] private int flyingSortOrder = 200;

    [Header("공중 — 급강하 할퀴기")]
    [SerializeField] private float diveTrackTime = 2f;
    [SerializeField] private float diveTrackSpeed = 7f;
    [SerializeField] private float diveWarn = 0.8f;
    [SerializeField] private float diveRadius = 2.2f;
    [SerializeField] private int diveDamage = 20;

    [Header("공중 — 화염구")]
    [SerializeField] private int airFireballs = 3;
    [SerializeField] private int airFireballsSecond = 4;
    [SerializeField] private float airFireballRadius = 1.4f;
    [SerializeField] private float airFireballFlight = 1.1f;
    [SerializeField] private float airFireballGap = 0.55f;
    [SerializeField] private int airFireballDamage = 14;

    [Header("공중 — 어둠 마법진 연쇄")]
    [SerializeField] private int circleChain = 4;
    [SerializeField] private int circleChainSecond = 6;
    [SerializeField] private float circleRadius = 1.3f;
    [SerializeField] private float circleWarn = 0.9f;
    [SerializeField] private float circleInterval = 0.35f;
    [SerializeField] private int circleDamage = 14;

    [Header("공중 — 불비")]
    [SerializeField] private Vector2Int meteorCount = new Vector2Int(8, 10);
    [SerializeField] private Vector2Int meteorCountSecond = new Vector2Int(11, 13);
    [SerializeField] private float meteorRadius = 1.2f;
    [SerializeField] private float meteorWarn = 1.0f;
    [SerializeField] private float meteorSpan = 1.2f;
    [SerializeField] private int meteorDamage = 16;

    [Header("낙뢰 — 전투 내내 주기적으로")]
    [SerializeField] private float lightningInterval = 3f;
    [SerializeField] private int lightningCount = 3;
    [SerializeField] private float lightningRadius = 1.3f;
    [SerializeField] private float lightningWarn = 1f;
    [SerializeField] private int lightningDamage = 12;
    [Tooltip("한 번에 떨어지는 낙뢰 중 하나는 플레이어 이 거리 안에 떨어진다 — 전부 먼 데 떨어지면 없는 셈이 된다")]
    [SerializeField] private float lightningNearPlayer = 4f;

    [Header("불장판")]
    [SerializeField] private float fireDuration = 3f;
    [SerializeField] private int fireTickDamage = 5;

    // ── 상태 ────────────────────────────────────────────
    public bool IsFlying { get { return airborne; } }
    public bool IsStunned { get { return stunned; } }
    public bool IsDarkPhase { get { return PhaseIndex >= 2; } }

    private bool sequenceActive;   // 날아오름 ~ 착지(기절 포함). 지상 AI는 멈춘다
    private bool airborne;         // 패링 반격만 받는다
    private bool stunned;          // 추락 후 기절 — 더 아프게 맞는다
    private int flightCount;
    private int parryCount;
    private bool fallRequested;
    private bool flightPatternRunning;
    private Coroutine flightPatternRoutine;
    private readonly List<DangerZone> flightZones = new List<DangerZone>();
    private readonly List<PixelVfx> flightFx = new List<PixelVfx>();

    private Room room;
    private SortingGroup sortGroup;
    private CapsuleCollider2D bodyCol;
    private Vector2 bodyColOffset;
    private bool bodyColTrigger;
    private readonly List<Collider2D> feet = new List<Collider2D>();
    private SpriteRenderer shadow;
    private float rejectedAt = -1f;

    private static readonly int FlyingHash = Animator.StringToHash("Flying");
    private static readonly int FlyReadyHash = Animator.StringToHash("FlyReady");
    private static readonly int FlySpitHash = Animator.StringToHash("FlySpit");
    private static readonly int FlyHitHash = Animator.StringToHash("FlyHit");
    private static readonly int LandingHash = Animator.StringToHash("Landing");
    private static readonly int HurtHash = Animator.StringToHash("Hurt");
    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int FlyDeathHash = Animator.StringToHash("FlyDeath");

    protected override void Awake()
    {
        base.Awake();

        // 그림은 자식에 있다. 피격 번쩍임·연출 막이 이 그림을 기준으로 잡는다
        if (body == null) body = transform.Find("Body");
        if (body != null)
        {
            var sr = body.GetComponent<SpriteRenderer>();
            if (sr != null) spriteRenderer = sr;
        }

        sortGroup = GetComponent<SortingGroup>();
        bodyCol = GetComponent<CapsuleCollider2D>();
        if (bodyCol != null) { bodyColOffset = bodyCol.offset; bodyColTrigger = bodyCol.isTrigger; }
        foreach (var c in GetComponentsInChildren<Collider2D>(true))
            if (c != bodyCol && !c.isTrigger && c.gameObject != gameObject) feet.Add(c);
    }

    protected override void Start()
    {
        base.Start();
        room = GetComponentInParent<Room>();
        MakeShadow();
        Died += OnDragonDied;
        StartCoroutine(LightningStorm());
    }

    // 몸통 가운데(1배 기준 발밑에서 1.5칸 위)를 비춘다
    protected override float PhaseFocusLift { get { return 1.5f * BodyScale; } }

    // 방이 스폰할 때 준 크기 배율(보스방에서 2배). 입 자리·비행 높이·그림자처럼
    // "몸에 붙은 거리"는 전부 1배 기준으로 적어 두고 여기에 곱한다.
    // 등장 연출 중에는 0에서 부풀어 오르므로 0이면 1로 본다
    private float BodyScale
    {
        get { float s = Mathf.Abs(transform.lossyScale.x); return s > 0.05f ? s : 1f; }
    }

    // 날아오른 동안은 지상 AI(추격·공격·페이즈 판정)를 통째로 쉰다 — 비행 코루틴이 몸을 직접 움직인다
    protected override void Update()
    {
        if (sequenceActive) return;
        base.Update();
    }

    private void LateUpdate()
    {
        PlaceDarkFlames();
        if (shadow == null) return;
        shadow.transform.position = transform.position;
        float h = body != null ? body.localPosition.y : 0f;
        float k = 1f - 0.3f * Mathf.Clamp01(h / Mathf.Max(0.01f, flyHeight));
        shadow.transform.localScale = ShadowScale() * k;
        shadow.enabled = !isDead || h > 0.05f;
    }

    private void OnDestroy()
    {
        StopDarkFlames();
        if (shadow != null) Destroy(shadow.gameObject);
    }

    private void MakeShadow()
    {
        if (shadowSprite == null) return;
        var go = new GameObject("DragonShadow");
        shadow = go.AddComponent<SpriteRenderer>();
        shadow.sprite = shadowSprite;
        shadow.color = new Color(0f, 0f, 0f, 0.45f);
        shadow.sortingLayerName = "Object";
        shadow.sortingOrder = 5;
        go.transform.position = transform.position;
        go.transform.localScale = ShadowScale();
    }

    private Vector3 ShadowScale()
    {
        Vector2 b = shadowSprite != null ? (Vector2)shadowSprite.bounds.size : Vector2.one;
        float k = BodyScale;
        return new Vector3(shadowSize.x * k / Mathf.Max(0.01f, b.x), shadowSize.y * k / Mathf.Max(0.01f, b.y), 1f);
    }

    // ─────────────────────────────────────────────
    // 피해 규칙
    // ─────────────────────────────────────────────

    protected override bool RejectsDamage(bool fromParry)
    {
        if (!airborne || fromParry) return false;

        // 안 들어간다는 걸 보여 준다 — 회색 0. 연타가 전부 뜨면 화면이 지저분해 조금 거른다
        if (Time.time - rejectedAt > 0.25f && DamageNumberSpawner.Instance != null)
        {
            rejectedAt = Time.time;
            Vector3 at = body != null ? body.position + Vector3.up * 1.2f * BodyScale : transform.position;
            DamageNumberSpawner.Instance.Show(at, 0, false, new Color(0.6f, 0.6f, 0.65f));
        }
        return true;
    }

    protected override float IncomingDamageMultiplier { get { return stunned ? stunDamageMultiplier : 1f; } }

    protected override bool CanBeStaggered { get { return !sequenceActive && base.CanBeStaggered; } }

    public override void OnParriedByPlayer()
    {
        if (!airborne || fallRequested) return;

        parryCount++;
        CameraShake.Shake(0.35f);
        if (parryCount >= parriesToFall)
        {
            fallRequested = true;
            ToastManager.Show("용이 균형을 잃는다!", ToastManager.Kind.Warn);
        }
        else ToastManager.Show("용이 휘청인다 (" + parryCount + "/" + parriesToFall + ")", ToastManager.Kind.Warn);
    }

    // ─────────────────────────────────────────────
    // 자리
    // ─────────────────────────────────────────────

    private Vector2 Facing { get { return IsFacingRight ? Vector2.right : Vector2.left; } }

    // 지금 입의 월드 좌표 (공중이면 올라간 그림 기준)
    public Vector3 MouthPosition
    {
        get
        {
            Vector3 from = body != null ? body.position : transform.position;
            Vector2 off = (airborne ? flyMouthOffset : mouthOffset) * BodyScale;
            return from + new Vector3(off.x * Facing.x, off.y, 0f);
        }
    }

    // 입 바로 아래 바닥 — 브레스 부채꼴의 꼭짓점
    public Vector3 GroundMouthPoint
    {
        get { return transform.position + (Vector3)(Facing * groundMouthForward * BodyScale); }
    }

    // 방 바닥 안쪽으로 잘라 준다. pad는 원 반지름 등 여유
    public Vector2 ClampToArena(Vector2 p, float pad)
    {
        Rect r = Arena();
        return new Vector2(Mathf.Clamp(p.x, r.xMin + pad, r.xMax - pad), Mathf.Clamp(p.y, r.yMin + pad, r.yMax - pad));
    }

    private Rect Arena()
    {
        if (room == null) room = GetComponentInParent<Room>();
        if (room == null) return new Rect((Vector2)transform.position - new Vector2(12f, 7f), new Vector2(24f, 14f));

        Vector2 c = room.transform.position;
        Vector2 half = room.Size * 0.5f;
        return Rect.MinMaxRect(c.x - half.x + arenaMargin.x, c.y - half.y + arenaMargin.z,
                               c.x + half.x - arenaMargin.y, c.y + half.y - arenaMargin.w);
    }

    private Vector2 RandomArenaPoint(float pad)
    {
        Rect r = Arena();
        return new Vector2(Random.Range(r.xMin + pad, r.xMax - pad), Random.Range(r.yMin + pad, r.yMax - pad));
    }

    private Vector2 PlayerPos()
    {
        Transform p = DragonFx.Player();
        return p != null ? (Vector2)p.position : (Vector2)transform.position;
    }

    // ─────────────────────────────────────────────
    // 어둠 마법진 (공중 연쇄 + 3페이즈 브레스가 같이 쓴다)
    // ─────────────────────────────────────────────

    // 플레이어 주위 방 곳곳에 한꺼번에 깐다 (첫 개는 플레이어 발밑)
    public void ScatterDarkCircles(int count, float stagger)
    {
        Vector2 center = PlayerPos();
        for (int i = 0; i < count; i++)
        {
            Vector2 at = i == 0 ? center : center + Random.insideUnitCircle * 5f;
            StartCoroutine(DarkCircle(ClampToArena(at, circleRadius), circleWarn, i * stagger));
        }
    }

    private IEnumerator DarkCircle(Vector2 at, float warn, float delay)
    {
        if (delay > 0f) yield return Wait(delay);
        if (isDead) yield break;

        DangerZone zone = DangerZone.Circle(at, circleRadius, warn);
        if (zone != null) zone.Dark();
        PixelVfx rune = DragonFx.Play("DragonDarkRune", at, circleRadius / 1.3f);

        yield return Wait(warn);
        if (rune != null) rune.Stop();
        if (isDead) { if (zone != null) zone.Cancel(); yield break; }

        if (zone != null) zone.CompleteNow();
        DragonFx.Play("DragonDarkBurst", at, circleRadius / 1.3f);
        CameraShake.Shake(0.15f);
        DamagePlayerInRadius(at, circleRadius, circleDamage, true);
    }

    // ─────────────────────────────────────────────
    // 비행
    // ─────────────────────────────────────────────

    // 비행 흐름을 콘솔에 남긴다 (시험할 때만 켠다)
    public static bool Trace;
    private void Log(string s) { if (Trace) Debug.Log("[Dragon] " + Time.time.ToString("F2") + " " + s); }

    protected override IEnumerator AfterPhaseShow(int phase)
    {
        flightCount++;
        sequenceActive = true;
        StopMove();
        Log("비행 시작 #" + flightCount + " (페이즈 " + phase + ")");

        yield return Ascend();
        if (!isDead) yield return FlightLoop();

        if (!isDead)
        {
            if (fallRequested) yield return FallAndStun();
            else yield return Land();
        }

        // 내려오자마자 할퀴지 않게 공격 간격을 새로 잰다
        lastAttackTime = Time.time;
        sequenceActive = false;
        Log("지상 복귀");
    }

    private void SetAirborneBody(bool air)
    {
        airborne = air;
        if (air) IsInvulnerableExternal = false;   // 반격은 들어가야 한다 — 막는 건 RejectsDamage가 한다

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.bodyType = air ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        }
        SetPositionLocked(false);

        // 공중에서는 몸이 아무것도 막지 않고, 그림 자리(위)로 판정을 올린다 — 때리면 회색 0이 뜬다
        if (bodyCol != null)
        {
            bodyCol.isTrigger = air || bodyColTrigger;
            bodyCol.offset = bodyColOffset + (air ? Vector2.up * flyHeight : Vector2.zero);   // 로컬 단위라 배율이 이미 들어 있다
        }
        foreach (var f in feet) if (f != null) f.enabled = !air;

        if (sortGroup != null) sortGroup.sortingOrder = air ? flyingSortOrder : CharacterSorting.Order;
    }

    private void SetHeight(float h)
    {
        if (body != null) body.localPosition = new Vector3(0f, h / Mathf.Max(0.01f, transform.lossyScale.y), 0f);
    }

    private float FlyHeightWorld { get { return flyHeight * BodyScale; } }

    private float Height { get { return body != null ? body.localPosition.y * transform.lossyScale.y : 0f; } }

    private void PlayAnim(int hash, float normalizedTime = 0f)
    {
        if (animator == null) return;
        animator.speed = 1f;
        animator.Play(hash, 0, normalizedTime);
    }

    private IEnumerator Ascend()
    {
        SetAirborneBody(true);
        PlayAnim(FlyingHash);
        CameraShake.Shake(0.3f);

        float from = Height;
        float t = 0f;
        while (t < 1f && !isDead)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, ascendTime);
            SetHeight(Mathf.Lerp(from, FlyHeightWorld, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
    }

    private IEnumerator FlightLoop()
    {
        parryCount = 0;
        fallRequested = false;
        float until = Time.time + flightMaxDuration;
        int last = -1;

        // 첫 패턴 전에 한 박자 떠서 자리를 잡는다
        yield return Hover(0.6f);

        while (!isDead && !fallRequested && Time.time < until)
        {
            int pick = Random.Range(0, 4);
            if (pick == last) pick = (pick + 1 + Random.Range(0, 3)) % 4;
            last = pick;
            Log("공중 패턴 " + pick + " (남은 " + (until - Time.time).ToString("F1") + "초)");

            flightPatternRunning = true;
            flightPatternRoutine = StartCoroutine(RunFlightPattern(pick));
            while (flightPatternRunning && !fallRequested && !isDead) yield return null;

            if (fallRequested || isDead)
            {
                if (flightPatternRoutine != null) StopCoroutine(flightPatternRoutine);
                CleanupFlightPattern();
                break;
            }

            yield return Hover(flightCount > 1 ? secondFlightGap : patternGap);
        }
        Log("비행 끝 — " + (fallRequested ? "추락" : isDead ? "사망" : "시간 만료"));
    }

    private IEnumerator RunFlightPattern(int which)
    {
        switch (which)
        {
            case 0: yield return FlightDive(); break;
            case 1: yield return FlightFireballs(); break;
            case 2: yield return FlightDarkChain(); break;
            default: yield return FlightMeteors(); break;
        }
        flightPatternRunning = false;
    }

    // 떨어뜨렸을 때 쓰던 공격의 흔적을 치운다. 이미 날아간 화염구·메테오는 그대로 떨어진다(예고가 이미 나갔다)
    private void CleanupFlightPattern()
    {
        foreach (var z in flightZones) if (z != null) z.Cancel();
        foreach (var f in flightFx) if (f != null) f.Stop();
        flightZones.Clear();
        flightFx.Clear();
        flightPatternRunning = false;
    }

    // ── 배회 비행 ──
    // 플레이어를 곧장 쫓으면 줄에 매달린 것처럼 보인다. 플레이어 둘레의 아무 지점을 골라
    // 부드럽게 가속·감속하며 날아가고, 닿거나 시간이 지나면 다음 지점을 고른다. 몸은 살짝 오르내린다.
    [Header("공중 — 배회")]
    [Tooltip("플레이어에게서 이 거리 사이(1배 기준)의 지점을 골라 날아간다")]
    [SerializeField] private Vector2 wanderRadius = new Vector2(2.5f, 4.5f);
    [Tooltip("한 지점으로 날아가는 시간(초) — 이 사이에서 무작위")]
    [SerializeField] private Vector2 wanderLeg = new Vector2(1.4f, 2.6f);
    [Tooltip("가속·감속의 부드러움. 클수록 크게 돌아 들어온다")]
    [SerializeField] private float wanderSmooth = 0.9f;
    [SerializeField] private float bobAmplitude = 0.18f;
    [SerializeField] private float bobSpeed = 2.2f;

    private Vector2 wanderGoal;
    private float wanderUntil = -1f;
    private Vector2 flyVel;

    private Vector2 PickWanderGoal()
    {
        float k = BodyScale;
        Vector2 p = PlayerPos();
        Vector2 here = transform.position;
        Vector2 best = here;
        for (int i = 0; i < 12; i++)
        {
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.01f) continue;
            // 뿌리(그림자)는 플레이어보다 조금 아래에 둔다 — 그림이 flyHeight만큼 위에 떠 있어 화면 밖으로 안 나간다
            Vector2 c = p + dir.normalized * Random.Range(wanderRadius.x, wanderRadius.y) * k + Vector2.down * 0.8f * k;
            c = ClampToArena(c, 2.5f * k);
            best = c;
            if (Vector2.Distance(c, here) > 2f * k) break;   // 제자리 근처만 맴돌지 않게 조금은 떨어진 곳
        }
        return best;
    }

    // 한 프레임 배회. speed는 평소 비행 속도에 곱하는 배율 (패턴 도중엔 느리게 떠돈다)
    private void WanderStep(float speed, bool facePlayer)
    {
        if (Time.time >= wanderUntil || Vector2.Distance(transform.position, wanderGoal) < 0.6f)
        {
            wanderGoal = PickWanderGoal();
            wanderUntil = Time.time + Random.Range(wanderLeg.x, wanderLeg.y);
        }

        Vector2 pos = Vector2.SmoothDamp(transform.position, wanderGoal, ref flyVel, wanderSmooth,
                                         flySpeed * BodyScale * speed, Time.deltaTime);
        if (rb != null) rb.position = pos;
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);

        // 옆으로 크게 움직일 땐 가는 쪽을 보고, 거의 떠 있을 땐 플레이어를 본다
        if (!facePlayer && Mathf.Abs(flyVel.x) > 0.8f) FaceToward(flyVel.x > 0f);
        else FaceToPlayer();

        SetHeight(FlyHeightWorld + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude * BodyScale);
    }

    private void FaceToward(bool right)
    {
        if (IsFacingRight != right) SetFacing(right);
    }

    // 패턴 사이 쉬는 시간 — 플레이어 둘레를 떠돈다
    private IEnumerator Hover(float duration)
    {
        PlayAnimIfNot(FlyingHash);
        float end = Time.time + duration;
        while (Time.time < end && !isDead && !fallRequested)
        {
            WanderStep(1f, false);
            yield return null;
        }
    }

    // 공중 패턴 도중 기다리는 시간 — 멈춰 서지 않고 천천히 떠돈다. 겨누는 동안이라 플레이어를 본다
    private IEnumerator WaitDrifting(float seconds)
    {
        float t = seconds;
        while (t > 0f && !isDead && !fallRequested)
        {
            t -= Time.deltaTime;
            WanderStep(0.35f, true);
            yield return null;
        }
    }

    private void PlayAnimIfNot(int hash)
    {
        if (animator == null) return;
        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash) return;
        PlayAnim(hash);
    }

    // ① 급강하 할퀴기 — 그림자가 플레이어를 쫓다 멈추고, 원 예고 뒤 내려찍는다
    private IEnumerator FlightDive()
    {
        PlayAnimIfNot(FlyingHash);
        float end = Time.time + diveTrackTime;
        while (Time.time < end && !isDead)
        {
            // 한 박자 늦게 따라붙는다 — 딱 붙어 쫓으면 기계처럼 보이고, 피할 틈도 없다
            Vector2 pos = Vector2.SmoothDamp(transform.position, ClampToArena(PlayerPos(), 1.5f), ref flyVel,
                                             0.45f, diveTrackSpeed * BodyScale, Time.deltaTime);
            if (rb != null) rb.position = pos;
            transform.position = new Vector3(pos.x, pos.y, transform.position.z);
            FaceToPlayer();
            yield return null;
        }
        flyVel = Vector2.zero;

        Vector2 at = transform.position;
        PlayAnim(FlyReadyHash);
        DangerZone zone = DangerZone.Circle(at, diveRadius, diveWarn);
        flightZones.Add(zone);
        yield return Wait(diveWarn);

        // 내려찍는다 — 짧게, 빠르게
        PlayAnim(LandingHash, 0.3f);
        float from = Height;
        float t = 0f;
        while (t < 1f && !isDead)
        {
            t += Time.deltaTime / 0.15f;
            SetHeight(Mathf.Lerp(from, 0f, t * t));
            yield return null;
        }
        if (zone != null) zone.CompleteNow();
        flightZones.Remove(zone);

        DragonFx.Play("DragonCrack", at, diveRadius / 2.2f);
        DragonFx.Play("DragonDust", at, diveRadius / 2.2f);
        CameraShake.Shake(0.55f);
        DamagePlayerInRadius(at, diveRadius, diveDamage, true);

        yield return Wait(0.35f);
        if (fallRequested) yield break;   // 받아쳐서 떨어뜨렸으면 그대로 바닥에 남는다

        PlayAnim(FlyingHash);
        t = 0f;
        while (t < 1f && !isDead)
        {
            t += Time.deltaTime / 0.45f;
            SetHeight(Mathf.Lerp(0f, FlyHeightWorld, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
    }

    // ② 공중 화염구 — 큰 불덩이를 한 발씩 플레이어 자리에 던진다
    private IEnumerator FlightFireballs()
    {
        int n = flightCount > 1 ? airFireballsSecond : airFireballs;
        for (int i = 0; i < n && !isDead; i++)
        {
            FaceToPlayer();
            PlayAnim(FlySpitHash);
            yield return Wait(0.3f);   // 입을 벌리는 프레임

            Vector2 target = ClampToArena(PlayerPos(), airFireballRadius);
            DragonLob.Launch(this, MouthPosition, target, airFireballFlight, 1.5f, airFireballRadius,
                             airFireballDamage, 1.6f, flightCount > 1, fireDuration, fireTickDamage);
            CameraShake.Shake(0.1f);
            yield return WaitDrifting(airFireballGap);
        }
        yield return WaitDrifting(airFireballFlight * 0.6f);
    }

    // ③ 어둠 마법진 연쇄 — 플레이어 발밑을 따라 차례로 깔린다
    private IEnumerator FlightDarkChain()
    {
        PlayAnim(FlyReadyHash);
        int n = flightCount > 1 ? circleChainSecond : circleChain;
        for (int i = 0; i < n && !isDead; i++)
        {
            StartCoroutine(DarkCircle(ClampToArena(PlayerPos(), circleRadius), circleWarn, 0f));
            yield return WaitDrifting(circleInterval);
        }
        yield return WaitDrifting(circleWarn);
        PlayAnim(FlyingHash);
    }

    // ④ 불비 — 방 곳곳에 메테오가 쏟아진다. 한 발은 반드시 플레이어 자리
    private IEnumerator FlightMeteors()
    {
        PlayAnim(FlyReadyHash);
        CameraShake.Shake(0.25f);
        Vector2Int range = flightCount > 1 ? meteorCountSecond : meteorCount;
        int n = Random.Range(range.x, range.y + 1);

        var spots = new List<Vector2>();
        spots.Add(ClampToArena(PlayerPos(), meteorRadius));
        int guard = 0;
        while (spots.Count < n && guard++ < 200)
        {
            Vector2 at = RandomArenaPoint(meteorRadius);
            bool close = false;
            foreach (var s in spots) if (Vector2.Distance(s, at) < meteorRadius * 1.6f) { close = true; break; }
            if (!close) spots.Add(at);
        }

        for (int i = 0; i < spots.Count; i++)
            StartCoroutine(Meteor(spots[i], i == 0 ? 0f : Random.Range(0f, meteorSpan)));

        yield return WaitDrifting(meteorSpan + meteorWarn + 0.2f);
        PlayAnim(FlyingHash);
    }

    // 떨어지는 그림은 한 장에 "그림자가 커지고 → 공이 떨어져 → 터지는" 과정이 다 들어 있다.
    // 공이 바닥에 닿는 16번째 칸이 예고가 다 차는 순간과 겹치도록 재생 속도를 맞춘다.
    private const int MeteorImpactFrame = 16;
    private const float MeteorGroundFromCenter = 59f / 100f;   // 프레임 가운데에서 바닥 닿는 곳까지 (원본 px / 100 PPU)

    private IEnumerator Meteor(Vector2 at, float delay)
    {
        if (delay > 0f) yield return Wait(delay);
        if (isDead) yield break;

        bool dark = flightCount > 1;
        DangerZone zone = DangerZone.Circle(at, meteorRadius, meteorWarn);
        if (dark && zone != null) zone.Dark();
        PixelVfx fall = DragonFx.Play(dark ? "DragonDarkMeteor" : "DragonMeteor", at, 1f);
        if (fall != null)
        {
            fall.transform.position = (Vector3)at + Vector3.up * MeteorGroundFromCenter * fall.transform.localScale.y;
            fall.SetFps(MeteorImpactFrame / Mathf.Max(0.1f, meteorWarn));
        }

        yield return Wait(meteorWarn);
        if (isDead) { if (zone != null) zone.Cancel(); yield break; }

        if (zone != null) zone.CompleteNow();
        CameraShake.Shake(0.2f);
        DamagePlayerInRadius(at, meteorRadius, meteorDamage, true);
        DragonFireGround.Spawn(this, at, meteorRadius, fireDuration, fireTickDamage, 0.5f, dark);
    }

    // ─────────────────────────────────────────────
    // 낙뢰 — 전투가 시작되면 끝날 때까지 3초마다 3개씩
    // ─────────────────────────────────────────────

    private IEnumerator LightningStorm()
    {
        // 등장 직후 한 박자는 쉰다
        yield return Wait(lightningInterval);

        while (!isDead)
        {
            Vector2 p = PlayerPos();
            var spots = new List<Vector2>();
            // 하나는 플레이어 가까이 (발밑 그대로는 아니다 — 서 있기만 해도 맞으면 억울하다)
            spots.Add(ClampToArena(p + Random.insideUnitCircle * lightningNearPlayer, lightningRadius));
            int guard = 0;
            while (spots.Count < lightningCount && guard++ < 50)
            {
                Vector2 at = RandomArenaPoint(lightningRadius);
                bool close = false;
                foreach (var s in spots) if (Vector2.Distance(s, at) < lightningRadius * 2.5f) { close = true; break; }
                if (!close) spots.Add(at);
            }

            foreach (var at in spots) StartCoroutine(LightningStrike(at, Random.Range(0f, 0.25f)));

            yield return Wait(lightningInterval);
        }
    }

    private IEnumerator LightningStrike(Vector2 at, float delay)
    {
        if (delay > 0f) yield return Wait(delay);
        if (isDead) yield break;

        DangerZone zone = DangerZone.Circle(at, lightningRadius, lightningWarn);
        yield return Wait(lightningWarn);
        if (isDead) { if (zone != null) zone.Cancel(); yield break; }
        if (zone != null) zone.CompleteNow();

        // 번개 그림은 피벗이 아래쪽(줄기 끝 근처)에 있어 그 자리에 그대로 세우면 바닥에 꽂힌다
        DragonFx.Play("DragonLightning", at, 1f);
        DragonFx.Play("DragonCrack", at, lightningRadius / 2.6f);
        CameraShake.Shake(0.15f);

        // 떨어지는 순간은 받아칠 수 있다 (다른 공중 공격과 같은 규칙)
        DamagePlayerInRadius(at, lightningRadius, lightningDamage, true);
    }

    // 떨어진다 → 기절
    private IEnumerator FallAndStun()
    {
        PlayAnim(FlyHitHash);
        yield return Wait(0.25f);

        float from = Height;
        float t = 0f;
        while (t < 1f && !isDead && from > 0.01f)
        {
            t += Time.deltaTime / 0.3f;
            SetHeight(Mathf.Lerp(from, 0f, t * t));
            yield return null;
        }
        SetHeight(0f);
        SetAirborneBody(false);

        DragonFx.Play("DragonDust", transform.position, 1.2f);
        CameraShake.Shake(0.6f);
        ToastManager.Show("용이 추락했다! 지금이 기회다", ToastManager.Kind.Warn);

        stunned = true;
        PlayAnim(HurtHash);
        yield return Wait(0.3f);
        if (animator != null) animator.speed = 0f;   // 쓰러진 자세로 버틴다

        float end = Time.time + stunDuration;
        while (Time.time < end && !isDead) yield return null;

        stunned = false;
        if (animator != null) animator.speed = 1f;
        if (!isDead) PlayAnim(IdleHash);
    }

    // 시간이 다 돼 스스로 내려온다
    private IEnumerator Land()
    {
        PlayAnim(LandingHash);
        float from = Height;
        float t = 0f;
        while (t < 1f && !isDead)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, landTime);
            SetHeight(Mathf.Lerp(from, 0f, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        SetHeight(0f);
        SetAirborneBody(false);
        CameraShake.Shake(0.3f);
        yield return Wait(0.3f);
        if (!isDead) PlayAnim(IdleHash);
    }

    // ── 3페이즈 어둠 불꽃 ──
    // 1층 해골왕이 페이즈를 넘기면 불타오르듯, 용은 "비늘이 어둠에 물드는" 순간 검보라 불길에 휩싸인다.
    // 용이 워낙 커서 불 하나를 뒤에 두면 몸에 가려 안 보인다 — 몸 둘레(등·날개·목·꼬리) 여러 곳에
    // 작은 불길을 붙이고, 발치 두 곳은 몸 앞에 그린다. 불 그림은 검정 테두리 + 보라 속불로 다시 칠한 것.
    [System.Serializable]
    public struct DarkFlameSpot
    {
        [Tooltip("오른쪽을 볼 때 발밑 기준 자리 (1배 기준)")] public Vector2 offset;
        [Tooltip("불길 크기 배율 (1 = 2칸)")] public float size;
        [Tooltip("솟는 불(가늘고 긴) / 아니면 바닥 불(넓게 타는)")] public bool rising;
        [Tooltip("몸 앞에 그린다 (발치)")] public bool front;
    }

    [Header("3페이즈 — 어둠 불꽃")]
    [SerializeField] private DarkFlameSpot[] darkFlames =
    {
        new DarkFlameSpot { offset = new Vector2(-0.7f, 1.3f), size = 1.5f },
        new DarkFlameSpot { offset = new Vector2(0.3f, 2.3f), size = 1.3f, rising = true },
        new DarkFlameSpot { offset = new Vector2(-1.9f, 0.5f), size = 1.1f },
        new DarkFlameSpot { offset = new Vector2(1.1f, 2.0f), size = 1.1f, rising = true },
        new DarkFlameSpot { offset = new Vector2(-0.3f, 3.0f), size = 1.2f, rising = true },
        new DarkFlameSpot { offset = new Vector2(0.7f, 0.1f), size = 0.9f, front = true },
        new DarkFlameSpot { offset = new Vector2(-1.1f, 0.1f), size = 0.9f, front = true },
    };

    private readonly List<PixelVfx> darkFlameFx = new List<PixelVfx>();

    protected override void OnPhaseShow(int phase)
    {
        if (phase >= 2 && darkFlameFx.Count == 0) LightDarkFire();
    }

    private void LightDarkFire()
    {
        if (body == null) return;
        float k = BodyScale;
        foreach (var spot in darkFlames)
        {
            PixelVfx v = PixelVfx.Play(spot.rising ? "DragonDarkFlameRise" : "DragonDarkFlameGround", body.position);
            if (v == null) continue;
            v.UseUnscaledTime();   // 전환 연출 중에는 시간이 멈춰 있다
            v.transform.localScale = Vector3.one * spot.size * k;
            darkFlameFx.Add(v);
        }
        PlaceDarkFlames();
    }

    // 몸(그림)을 따라가고, 좌우를 바꾸면 자리도 뒤집는다. 앞뒤 그리기 순서는 용의 정렬 묶음 기준
    private void PlaceDarkFlames()
    {
        if (darkFlameFx.Count == 0 || body == null) return;
        float k = BodyScale;
        float side = IsFacingRight ? 1f : -1f;
        int baseOrder = sortGroup != null ? sortGroup.sortingOrder : CharacterSorting.Order;
        for (int i = 0; i < darkFlameFx.Count && i < darkFlames.Length; i++)
        {
            PixelVfx v = darkFlameFx[i];
            if (v == null) continue;
            DarkFlameSpot spot = darkFlames[i];
            v.transform.position = body.position + new Vector3(spot.offset.x * side, spot.offset.y, 0f) * k;
            var sr = v.GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            sr.sortingLayerName = CharacterSorting.Layer;
            sr.sortingOrder = baseOrder + (spot.front ? 1 : -1);
            sr.flipX = side < 0f;
        }
    }

    private void StopDarkFlames()
    {
        foreach (var v in darkFlameFx) if (v != null) v.Stop();
        darkFlameFx.Clear();
    }

    // 공중에서 죽으면(반격 막타) 공중 사망 동작으로 떨어진다
    private void OnDragonDied(Enemy _)
    {
        StopDarkFlames();
        CleanupFlightPattern();
        stunned = false;
        if (Height > 0.05f) StartCoroutine(FallDead());
    }

    private IEnumerator FallDead()
    {
        // 땅 위 사망 동작(dead 신호)보다 공중 사망 동작이 먼저다
        if (animator != null) { animator.ResetTrigger("dead"); animator.speed = 1f; animator.Play(FlyDeathHash, 0, 0f); }
        float from = Height;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.6f;
            SetHeight(Mathf.Lerp(from, 0f, t * t));
            yield return null;
        }
        if (sortGroup != null) sortGroup.sortingOrder = CharacterSorting.Order;
    }

    private IEnumerator Wait(float seconds)
    {
        float t = seconds;
        while (t > 0f && !isDead) { t -= Time.deltaTime; yield return null; }
    }
}
