using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 3층 최종 보스 — 거대 사신.
//
// 플레이어와 같은 "사신"이라, 플레이어가 쓰는 수를 그대로 되돌려 준다.
//   · 역처형: 플레이어 체력이 30% 이하면 목에 표식을 걸고 순간이동해 처형 일격을 날린다
//   · 순간이동: 땅속으로 가라앉았다가 등 뒤에서 솟아오른다
//   · 분신·진짜 찾기·심판·영혼 결계 같은 기믹이 페이즈마다 하나씩 열린다
// 기믹이 나올 때는 토스트로 힌트를 띄운다 — 처음 보는 사람도 무엇을 해야 하는지 알 수 있어야 한다.
//
// 그림은 자식(Body)에 있다. 순간이동·진짜 찾기에서 몸을 숨기고 다시 보이는 일이 잦아서
// 몸(그림·판정)과 뿌리(위치·물리)를 나눠 두는 편이 다루기 쉽다.
public class ReaperBoss : BossEnemy
{
    [Header("사신 — 몸")]
    [SerializeField] private Transform body;
    [SerializeField] private Sprite shadowSprite;
    [SerializeField] private Vector2 shadowSize = new Vector2(2.2f, 0.7f);

    [Header("분신 그림 (온몸이 색 불꽃에 휩싸인 모습 — dead_loop)")]
    [SerializeField] private Sprite[] cloneBlue;
    [SerializeField] private Sprite[] cloneGreen;
    [SerializeField] private Sprite[] cloneRed;
    [SerializeField] private Sprite[] cloneBrown;

    [Header("무대")]
    [Tooltip("방 가장자리에서 이만큼 안쪽까지만 쓴다 (좌, 우, 아래, 위)")]
    [SerializeField] private Vector4 arenaMargin = new Vector4(1.5f, 1.5f, 1.3f, 4.2f);

    [Header("4페이즈 — 붉은 오오라")]
    [SerializeField] private Vector2[] auraSpots =
    {
        new Vector2(-0.75f, 0.9f), new Vector2(0.75f, 0.95f), new Vector2(-1.15f, 1.7f), new Vector2(1.15f, 1.75f),
        new Vector2(-0.35f, 0.55f), new Vector2(0.4f, 0.6f), new Vector2(0f, 2.3f),
    };
    [Tooltip("불꽃 하나의 크기 (몸 배율에 곱한다). 크면 몸을 덮어 덩어리처럼 보인다")]
    [SerializeField] private float auraScale = 0.85f;

    [Header("4페이즈 — 영혼 결계")]
    [Tooltip("결계가 다 좁혀지는 데 걸리는 시간(초)")]
    [SerializeField] private float barrierTime = 30f;
    [Tooltip("다 좁혀졌을 때 남는 가로·세로 비율 (0.775 × 0.775 ≈ 넓이 60%)")]
    [SerializeField] private float barrierMinScale = 0.775f;
    [SerializeField] private int barrierTickDamage = 6;
    [SerializeField] private float barrierTickInterval = 0.5f;

    [Header("기절")]
    [SerializeField] private float stunDamageMultiplier = 1.5f;

    [Header("두 번째 목숨 — 쓰러지거나 처형당하면 한 번 되살아난다")]
    [SerializeField] private bool secondLifeEnabled = true;
    [Tooltip("되살아난 뒤의 최대 체력 (처음 최대 체력 대비). 체력바는 가득 찬 채로 다시 시작한다")]
    [Range(0.1f, 1f)] [SerializeField] private float secondLifeHpRatio = 0.6f;
    [Tooltip("쓰러지는 동작 dead_1~7. 일어설 때 거꾸로 튼다")]
    [SerializeField] private Sprite[] deathFrames;
    [Tooltip("붉은 기운(red_dark dead_loop)에 휩싸여 체력이 다시 차는 시간(초)")]
    [SerializeField] private float reviveRefillTime = 2.6f;
    [Tooltip("두 번째 목숨의 이동·공격 간격·예고 배율 (처음 값 대비)")]
    [SerializeField] private Vector3 secondTempo = new Vector3(1.65f, 0.45f, 0.62f);
    [Tooltip("두 번째 목숨 체력 절반 아래 — 마지막 발악")]
    [SerializeField] private Vector3 frenzyTempo = new Vector3(1.8f, 0.38f, 0.58f);
    [Tooltip("공격이 끝날 때마다 순간이동 베기를 한 번 더 이어 붙일 확률 (두 번째 목숨 / 마지막 발악)")]
    [SerializeField] private Vector2 chainChance = new Vector2(0.4f, 0.6f);
    [Tooltip("두 번째 목숨에 몸에 더 얹는 검보라 불꽃 (용 3페이즈 불꽃)")]
    [SerializeField] private string darkAuraRiseId = "DragonDarkFlameRise";

    // 되살아난 뒤인지 / 두 번째 목숨의 절반 아래인지
    public bool SecondLife { get; private set; }
    public bool Frenzy { get; private set; }
    private bool reviving;
    private readonly List<PixelVfx> darkAura = new List<PixelVfx>();

    public float BodyScale { get { float s = Mathf.Abs(transform.lossyScale.x); return s > 0.05f ? s : 1f; } }
    public Sprite BodySprite { get { return spriteRenderer != null ? spriteRenderer.sprite : null; } }
    public Vector2 Chest { get { return (Vector2)transform.position + Vector2.up * 1.6f * BodyScale; } }
    public bool IsStunned { get { return stunned; } }
    public bool Hidden { get; private set; }

    // 역처형 받아치기 창 — 열려 있는 동안 받아치면 기록한다
    public bool ExecutionWindowOpen { get; set; }
    public bool ExecutionParried { get; set; }

    // 진짜 찾기 중 본체가 맞았는지
    public bool FindActive { get; set; }
    public bool RealHit { get; set; }

    private Room room;
    private SortingGroup sortGroup;
    private SpriteRenderer shadow;
    private bool stunned;
    private readonly List<Collider2D> bodyCols = new List<Collider2D>();
    private readonly List<PixelVfx> aura = new List<PixelVfx>();
    private readonly List<ReaperClone> clones = new List<ReaperClone>();

    // 결계
    private bool barrierOn;
    private float barrierStart;
    private readonly List<PixelVfx> barrierFlames = new List<PixelVfx>();
    private readonly SpriteRenderer[] barrierCurtains = new SpriteRenderer[4];
    private float nextBarrierTick;

    protected override void Awake()
    {
        base.Awake();
        if (body == null) body = transform.Find("Body");
        if (body != null)
        {
            var sr = body.GetComponent<SpriteRenderer>();
            if (sr != null) spriteRenderer = sr;
        }
        sortGroup = GetComponent<SortingGroup>();
        foreach (var c in GetComponentsInChildren<Collider2D>(true))
            if (c.gameObject.layer == LayerMask.NameToLayer("Enemy")) bodyCols.Add(c);
    }

    protected override void Start()
    {
        base.Start();
        room = GetComponentInParent<Room>();
        MakeShadow();
        Died += OnReaperDied;
        AnyDamaged += OnAnyDamaged;
    }

    private void OnDestroy()
    {
        AnyDamaged -= OnAnyDamaged;
        Cleanup();
        if (shadow != null) Destroy(shadow.gameObject);
    }

    protected override void Update()
    {
        base.Update();
        if (barrierOn && !isDead && !reviving) TickBarrier();

        // 두 번째 목숨의 절반 아래 — 마지막 발악. 공격 도중에는 끊지 않는다
        if (SecondLife && !Frenzy && !reviving && !isDead && !isAttacking && !IsPhaseChanging && HpRatio <= 0.5f)
        {
            Frenzy = true;
            StartCoroutine(RunScripted(FrenzyShow()));
        }
    }

    private void LateUpdate()
    {
        if (shadow != null)
        {
            shadow.transform.position = transform.position;
            shadow.enabled = !Hidden && !isDead;
        }
        PlaceAura();
    }

    private void MakeShadow()
    {
        if (shadowSprite == null) return;
        var go = new GameObject("ReaperShadow");
        shadow = go.AddComponent<SpriteRenderer>();
        shadow.sprite = shadowSprite;
        shadow.color = new Color(0f, 0f, 0f, 0.5f);
        shadow.sortingLayerName = "Object";
        shadow.sortingOrder = 5;
        Vector2 b = shadowSprite.bounds.size;
        go.transform.localScale = new Vector3(shadowSize.x * BodyScale / b.x, shadowSize.y * BodyScale / b.y, 1f);
    }

    public Sprite[] CloneFrames(ReaperClone.Kind kind)
    {
        switch (kind)
        {
            case ReaperClone.Kind.Blue: return cloneBlue;
            case ReaperClone.Kind.Green: return cloneGreen;
            case ReaperClone.Kind.Red: return cloneRed;
            case ReaperClone.Kind.Brown: return cloneBrown;
        }
        return null;
    }

    // ─────────────────────────────────────────────
    // 무대
    // ─────────────────────────────────────────────

    public Rect Arena
    {
        get
        {
            if (room == null) room = GetComponentInParent<Room>();
            Rect r;
            if (room == null) r = new Rect((Vector2)transform.position - new Vector2(12f, 7f), new Vector2(24f, 14f));
            else
            {
                Vector2 c = room.transform.position;
                Vector2 half = room.Size * 0.5f;
                r = Rect.MinMaxRect(c.x - half.x + arenaMargin.x, c.y - half.y + arenaMargin.z,
                                    c.x + half.x - arenaMargin.y, c.y + half.y - arenaMargin.w);
            }
            if (!barrierOn) return r;

            // 결계가 좁혀지는 만큼 무대도 줄어든다
            float k = Mathf.Lerp(1f, barrierMinScale, Mathf.Clamp01((Time.time - barrierStart) / Mathf.Max(1f, barrierTime)));
            Vector2 center = r.center, size = r.size * k;
            return new Rect(center - size * 0.5f, size);
        }
    }

    public Vector2 ClampToArena(Vector2 p, float pad)
    {
        Rect r = Arena;
        return new Vector2(Mathf.Clamp(p.x, r.xMin + pad, r.xMax - pad), Mathf.Clamp(p.y, r.yMin + pad, r.yMax - pad));
    }

    public Vector2 RandomArenaPoint(float pad)
    {
        Rect r = Arena;
        return new Vector2(Random.Range(r.xMin + pad, r.xMax - pad), Random.Range(r.yMin + pad, r.yMax - pad));
    }

    // ─────────────────────────────────────────────
    // 숨기 / 나타나기
    // ─────────────────────────────────────────────

    public void SetHidden(bool hide)
    {
        Hidden = hide;
        if (spriteRenderer != null) spriteRenderer.enabled = !hide;
        foreach (var c in bodyCols) if (c != null) c.enabled = !hide;
        IsInvulnerableExternal = hide;
        if (rb != null) rb.velocity = Vector2.zero;
    }

    public void TeleportTo(Vector2 pos)
    {
        pos = ClampToArena(pos, 1f);
        if (rb != null) rb.position = pos;
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        FaceToPlayer();
    }

    public void Turn() { SetFacing(!IsFacingRight); }

    public void PlayState(string state, float speed = 1f)
    {
        if (animator == null) return;
        animator.ResetTrigger("attack");
        animator.speed = speed;
        animator.Play(state, 0, 0f);
    }

    public float ClipLength(string state)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return 0.5f;
        foreach (var c in animator.runtimeAnimatorController.animationClips)
            if (c != null && c.name.EndsWith("_" + state)) return c.length;
        return 0.5f;
    }

    // ─────────────────────────────────────────────
    // 기절 / 피해 규칙
    // ─────────────────────────────────────────────

    public IEnumerator Stun(float seconds, string toast)
    {
        stunned = true;
        if (!string.IsNullOrEmpty(toast)) ToastManager.Show(toast, ToastManager.Kind.Warn);
        PlayState("Hurt");
        yield return ReaperFx.Wait(this, 0.25f);
        if (animator != null) animator.speed = 0f;
        yield return ReaperFx.Wait(this, seconds);
        if (animator != null) animator.speed = 1f;
        stunned = false;
        if (!isDead) PlayState("Idle");
    }

    protected override float IncomingDamageMultiplier { get { return stunned ? stunDamageMultiplier : 1f; } }

    // 두 번째 목숨의 체력바는 절반에 선 하나 — 마지막 발악이 거기서 시작된다
    public override float[] PhaseBoundaries
    {
        get { return SecondLife ? new float[] { 0.5f } : base.PhaseBoundaries; }
    }

    // 처형은 두 번째 목숨의 마지막 칸(절반 아래)에서 열린다. 되살아나는 중에는 절대 안 열린다
    protected override float HarvestRatio
    {
        get
        {
            if (reviving) return 1f;
            if (SecondLife) return HpRatio / 0.5f;
            return base.HarvestRatio;
        }
    }

    // 두 번째 목숨에서는 공격 끝에 순간이동 베기를 이어 붙인다 — 숨 돌릴 틈을 주지 않는다
    protected override IEnumerator AttackActivePhase(Vector2 dir)
    {
        BossPattern pick = ActivePattern;
        yield return base.AttackActivePhase(dir);

        if (!SecondLife || isDead || reviving || pick == null || pick is BossPatternReaperTeleport) yield break;
        if (Random.value >= (Frenzy ? chainChance.y : chainChance.x)) yield break;
        var tp = GetComponent<BossPatternReaperTeleport>();
        if (tp != null) yield return tp.RunCount(this, 1);
    }

    protected override bool CanBeStaggered { get { return false; } }

    // 처형해도(첫 목숨이든 두 번째 목숨이든) 소울을 주지 않는다
    public override bool HarvestGivesSoul { get { return false; } }

    public override void OnParriedByPlayer()
    {
        if (ExecutionWindowOpen) ExecutionParried = true;
    }

    private void OnAnyDamaged(Enemy e)
    {
        if (e == this && FindActive) RealHit = true;
    }

    // ─────────────────────────────────────────────
    // 분신
    // ─────────────────────────────────────────────

    public ReaperClone SpawnClone(ReaperClone.Kind kind, Vector2 pos, float life, int hp)
    {
        var c = ReaperClone.Create(this, kind, ClampToArena(pos, 1f), life, hp);
        if (c != null) clones.Add(c);
        return c;
    }

    // ─────────────────────────────────────────────
    // 페이즈 연출
    // ─────────────────────────────────────────────

    protected override float PhaseFocusLift { get { return 1.8f * BodyScale; } }

    protected override void OnPhaseShow(int phase)
    {
        if (phase >= 3 && aura.Count == 0) LightAura();
        if (SecondLife && darkAura.Count == 0) LightDarkAura();
    }

    // 두 번째 목숨 — 붉은 오오라 사이로 검보라 불꽃이 더 오른다
    private void LightDarkAura()
    {
        for (int i = 0; i < auraSpots.Length; i += 2)
        {
            PixelVfx v = PixelVfx.Play(darkAuraRiseId, transform.position);
            if (v == null) continue;
            v.UseUnscaledTime();
            v.transform.localScale = Vector3.one * auraScale * 0.9f * BodyScale;
            darkAura.Add(v);
        }
    }

    protected override IEnumerator AfterPhaseShow(int phase)
    {
        if (phase >= 3 && !barrierOn) StartBarrier();
        yield break;
    }

    private void LightAura()
    {
        foreach (var spot in auraSpots)
        {
            PixelVfx v = PixelVfx.Play(Random.value < 0.5f ? "ReaperRedFlameRise" : "ReaperRedFlameGround", transform.position);
            if (v == null) continue;
            v.UseUnscaledTime();
            v.transform.localScale = Vector3.one * auraScale * BodyScale;
            aura.Add(v);
        }
        PlaceAura();
    }

    private void PlaceAura()
    {
        if (body == null) return;
        float side = IsFacingRight ? 1f : -1f;
        int baseOrder = sortGroup != null ? sortGroup.sortingOrder : CharacterSorting.Order;
        for (int i = 0; i < aura.Count && i < auraSpots.Length; i++)
            PlaceFlame(aura[i], auraSpots[i], side, baseOrder);
        // 검보라 불꽃은 붉은 불꽃 사이(짝수 자리)에 조금 비껴 선다
        for (int i = 0; i < darkAura.Count && i * 2 < auraSpots.Length; i++)
            PlaceFlame(darkAura[i], auraSpots[i * 2] + new Vector2(0.25f, 0.15f), side, baseOrder);
    }

    private void PlaceFlame(PixelVfx v, Vector2 spot, float side, int baseOrder)
    {
        if (v == null) return;
        v.transform.position = body.position + new Vector3(spot.x * side, spot.y, 0f) * BodyScale;
        var sr = v.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        sr.enabled = !Hidden;
        sr.sortingLayerName = CharacterSorting.Layer;
        sr.sortingOrder = baseOrder - 1;
    }

    // ─────────────────────────────────────────────
    // 영혼 결계 — 방 가장자리부터 영혼 불꽃이 조여 온다
    // ─────────────────────────────────────────────

    private void StartBarrier()
    {
        barrierOn = true;
        barrierStart = Time.time;
        ToastManager.Show("영혼 결계가 좁혀 온다!", ToastManager.Kind.Warn);

        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("ReaperBarrierCurtain");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite();
            sr.color = new Color(0.25f, 0.06f, 0.35f, 0.55f);
            sr.sortingLayerName = "Object";
            sr.sortingOrder = 18;
            barrierCurtains[i] = sr;
        }
    }

    private void TickBarrier()
    {
        Rect inner = Arena;
        Rect outer;
        if (room != null)
        {
            Vector2 c = room.transform.position, half = room.Size * 0.5f + Vector2.one;
            outer = Rect.MinMaxRect(c.x - half.x, c.y - half.y, c.x + half.x, c.y + half.y);
        }
        else outer = new Rect(inner.center - inner.size, inner.size * 2f);

        // 바깥 띠 넷: 아래, 위, 왼, 오른
        SetBand(barrierCurtains[0], outer.xMin, outer.yMin, outer.xMax, inner.yMin);
        SetBand(barrierCurtains[1], outer.xMin, inner.yMax, outer.xMax, outer.yMax);
        SetBand(barrierCurtains[2], outer.xMin, inner.yMin, inner.xMin, inner.yMax);
        SetBand(barrierCurtains[3], inner.xMax, inner.yMin, outer.xMax, inner.yMax);

        // 안쪽 테두리를 따라 영혼 불꽃
        float per = 1.6f;
        int want = Mathf.Max(8, Mathf.RoundToInt((inner.width + inner.height) * 2f / per));
        while (barrierFlames.Count < want)
        {
            PixelVfx v = PixelVfx.Play("ReaperSoulFire", inner.center);
            if (v == null) break;
            v.transform.localScale *= 1.3f;
            barrierFlames.Add(v);
        }
        float perimeter = (inner.width + inner.height) * 2f;
        for (int i = 0; i < barrierFlames.Count; i++)
        {
            if (barrierFlames[i] == null) continue;
            float d = perimeter * i / barrierFlames.Count;
            barrierFlames[i].transform.position = PointOnRect(inner, d);
        }

        // 결계 밖에 서 있으면 탄다 (받아칠 수 없다)
        Transform p = ReaperFx.Player();
        if (p != null && !inner.Contains(p.position) && Time.time >= nextBarrierTick)
        {
            nextBarrierTick = Time.time + barrierTickInterval;
            DamagePlayer(barrierTickDamage, false);
        }
    }

    private static void SetBand(SpriteRenderer sr, float x0, float y0, float x1, float y1)
    {
        if (sr == null) return;
        sr.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
        sr.transform.localScale = new Vector3(Mathf.Max(0f, x1 - x0), Mathf.Max(0f, y1 - y0), 1f);
    }

    private static Vector2 PointOnRect(Rect r, float d)
    {
        float w = r.width, h = r.height;
        if (d < w) return new Vector2(r.xMin + d, r.yMin);
        d -= w; if (d < h) return new Vector2(r.xMax, r.yMin + d);
        d -= h; if (d < w) return new Vector2(r.xMax - d, r.yMax);
        d -= w; return new Vector2(r.xMin, r.yMax - Mathf.Min(d, h));
    }

    private static Sprite white;
    private static Sprite WhiteSprite()
    {
        if (white != null) return white;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color32[16];
        for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px); tex.Apply();
        white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        return white;
    }

    // ─────────────────────────────────────────────
    // 두 번째 목숨
    // ─────────────────────────────────────────────

    protected override bool InterceptDeath(bool fromHarvest)
    {
        if (!secondLifeEnabled || SecondLife || reviving) return false;
        reviving = true;
        IsInvulnerable = true;
        StartCoroutine(RunScripted(ReviveShow()));
        return true;
    }

    private IEnumerator ReviveShow()
    {
        // 하던 공격·기믹을 전부 거둔다
        CancelAttack();
        ClearGimmicks();

        // 쓰러진다. 처형으로 쓰러졌으면 처형 연출이 끝날 때까지 누운 채 기다린다
        if (animator != null)
        {
            animator.speed = 1f;
            animator.SetBool("isFollow", false);
            PlayState("Death");
        }
        CameraShake.Shake(0.4f);
        float t = 0f;
        while (t < 1.5f || (HarvestManager.Instance != null && HarvestManager.Instance.IsHarvesting))
        {
            t += Time.deltaTime;
            yield return null;
        }

        // 죽음을 거부한다 — 쓰러진 몸이 붉은 기운에 휩싸여 꿈틀대는 동안 체력이 다시 찬다
        ToastManager.Show("사신이 죽음을 저항한다!", ToastManager.Kind.Warn);
        CameraShake.Shake(0.6f);
        if (maxHp > 0) maxHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * secondLifeHpRatio));
        SecondLife = true;
        EnemyHealthBar.ShowBoss(this);          // 체력바 경계를 절반 한 줄로 다시 긋는다

        if (animator != null) animator.enabled = false;
        Sprite[] loop = cloneRed;
        float frame = 0f, nextBurst = 0f;
        var bursts = new List<PixelVfx>();      // 반복 이펙트라 스스로 안 꺼진다 — 일어설 때 거둔다
        t = 0f;
        while (t < reviveRefillTime)
        {
            t += Time.deltaTime;
            frame += Time.deltaTime * 8f;
            if (loop != null && loop.Length > 0 && spriteRenderer != null) spriteRenderer.sprite = loop[(int)frame % loop.Length];

            // 처음에 빠르고 끝에서 느려진다 — 체력바가 빨려 들어가듯 찬다
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / reviveRefillTime), 2f);
            int want = Mathf.RoundToInt(maxHp * k);
            if (want > hp) Heal(want - hp);

            if (t >= nextBurst)
            {
                nextBurst = t + 0.35f;
                Vector2 at = Chest + Random.insideUnitCircle * 1.4f * BodyScale;
                PixelVfx b = PixelVfx.Play(Random.value < 0.5f ? "ReaperRedFlameRise" : "ReaperRedFlameGround", at);
                if (b != null) bursts.Add(b);
            }
            yield return null;
        }
        if (hp < maxHp) Heal(maxHp - hp);

        // 일어선다 — 쓰러지던 동작을 거꾸로
        if (deathFrames != null && spriteRenderer != null)
            for (int i = deathFrames.Length - 1; i >= 0; i--)
            {
                if (deathFrames[i] != null) spriteRenderer.sprite = deathFrames[i];
                yield return new WaitForSeconds(1f / 12f);
            }
        foreach (var b in bursts) if (b != null) b.Stop();
        if (animator != null) { animator.enabled = true; PlayState("Idle"); }

        // 마지막 페이즈에 이르기 전에 쓰러졌어도 두 번째 목숨은 마지막 페이즈 규칙 위에서 싸운다
        ForcePhase(PhaseCount - 1);
        if (aura.Count == 0) LightAura();

        // 시간이 멈추고 화면이 사신에게 당겨진다
        ToastManager.Show("사신이 두 번째 목숨을 불태운다", ToastManager.Kind.Warn);
        yield return PlayPhaseShow(1.8f);
        SetTempo(secondTempo.x, secondTempo.y, secondTempo.z);

        // 결계는 곧바로 끝까지 조여 있다
        if (!barrierOn) StartBarrier();
        barrierStart = Time.time - barrierTime;
        reviving = false;
    }

    private IEnumerator FrenzyShow()
    {
        ToastManager.Show("사신이 마지막 발악을 한다!", ToastManager.Kind.Warn);
        yield return PlayPhaseShow(1.6f);
        SetTempo(frenzyTempo.x, frenzyTempo.y, frenzyTempo.z);
    }

    // 쓰러질 때 남아 있던 패턴의 흔적을 치운다 (분신·가짜·예고·숨은 몸·기절)
    private void ClearGimmicks()
    {
        foreach (var c in clones) if (c != null && !c.isDead) c.Vanish();
        clones.Clear();
        foreach (var z in FindObjectsOfType<DangerZone>()) if (z != null) z.Cancel();
        if (Hidden) SetHidden(false);
        stunned = false;
        FindActive = false;
        RealHit = false;
        ExecutionWindowOpen = false;
        if (animator != null) animator.speed = 1f;
    }

    // ─────────────────────────────────────────────
    // 정리
    // ─────────────────────────────────────────────

    private void OnReaperDied(Enemy _)
    {
        if (Hidden) SetHidden(false);
        Cleanup();
    }

    private void Cleanup()
    {
        foreach (var c in clones) if (c != null) c.Vanish();
        clones.Clear();
        foreach (var v in aura) if (v != null) v.Stop();
        aura.Clear();
        foreach (var v in darkAura) if (v != null) v.Stop();
        darkAura.Clear();
        foreach (var v in barrierFlames) if (v != null) v.Stop();
        barrierFlames.Clear();
        for (int i = 0; i < 4; i++) if (barrierCurtains[i] != null) { Destroy(barrierCurtains[i].gameObject); barrierCurtains[i] = null; }
        barrierOn = false;
    }
}
