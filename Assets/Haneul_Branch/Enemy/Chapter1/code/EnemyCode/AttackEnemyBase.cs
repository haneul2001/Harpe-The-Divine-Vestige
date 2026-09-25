using System.Collections;
using UnityEngine;

// 근접 공격 적의 공통 베이스.
// 공통 흐름: 정지 → 방향 전환 → 공격 예고 → 공격 애니/히트박스 → (공격 중 동작) → 회복.
// "공격 중 동작"(돌진 / 제자리 등)만 파생 클래스가 AttackActivePhase로 구현한다.
public abstract class AttackEnemyBase : Enemy, IEnemyAttack
{
    [Header("공격 오브젝트")]
    [SerializeField] protected GameObject attackHitBox;
    [SerializeField] protected GameObject attackRangeBox;
    [SerializeField] protected GameObject attackPivot;

    [Header("공격 타이밍")]
    [Tooltip("공격 애니메이션 중간에 걸린 이벤트를 기다렸다 히트박스를 켠다.\n" +
             "끄면 애니메이션 시작과 동시에 켜진다(예전 방식).")]
    [SerializeField] protected bool waitForAnimationHit = true;

    [Tooltip("이벤트가 오지 않을 때를 대비한 최대 대기 시간(초).\n" +
             "애니메이션에 이벤트를 안 걸었거나 클립이 바뀌어도 공격이 먹통이 되지 않게 한다.")]
    [SerializeField] protected float hitEventTimeout = 0.7f;

    // 애니메이션 이벤트가 이 프레임에 도달했는지
    private bool animHitReceived;

    // ── 애니메이션 이벤트에서 호출 ──────────────────────────
    // 공격 클립 중간 프레임에 Function 이름으로 "AnimAttackHit"을 걸어 두면 된다.
    // Animator와 같은 오브젝트에 이 컴포넌트가 있어야 호출된다 (둘 다 적 루트에 있다).
    public void AnimAttackHit()
    {
        animHitReceived = true;
    }

    [Header("공격 방향")]
    [Tooltip("플레이어처럼 좌우로만 공격한다. 끄면 플레이어 쪽으로 임의 각도로 회전한다.\n" +
             "탑다운이지만 캐릭터·이펙트가 전부 좌우 기준으로 그려져 있어 켜 두는 편이 자연스럽다.")]
    [SerializeField] protected bool horizontalAttackOnly = true;

    [Tooltip("좌우 공격일 때 높이가 이만큼 안에 들어와야 공격한다.\n" +
             "너무 좁으면 적이 줄을 맞추느라 계속 따라다니고, 너무 넓으면 빗나가는 게 눈에 보인다.")]
    [SerializeField] protected float verticalTolerance = 1.2f;

    [Header("공격 예고 표시")]
    [Tooltip("공격 전 범위 표시를 띄운다. 끄면 예고 시간(attackWarningDuration)은 그대로 두고 표시만 안 나온다 — "
           + "리듬은 유지하되 화면을 깔끔하게 두고 싶을 때 쓴다.")]
    [SerializeField] protected bool showAttackWarning = false;

    // 지금 떠 있는 예고 표식. 적이 예고 도중 죽으면 코루틴이 finally 없이 끊기므로
    // 여기 들고 있다가 OnDisable에서 확실히 지운다.
    private PixelVfx activeTelegraph;

    [Header("공격 공통 설정")]
    [Tooltip("지상 전용 공격이면 체크 (플레이어가 공중이면 회피됨)")]
    [SerializeField] protected bool groundOnly = false;
    [Tooltip("공격 후 회복(경직) 시간(초)")]
    [SerializeField] protected float recoverTime = 1f;

    [Header("사거리 자동 맞춤")]
    [Tooltip("공격 시작 거리(attackRange)와 예고 표시를 실제 히트박스가 닿는 범위에 맞춘다.\n" +
             "히트박스는 스폰 크기 배율(Room.SpawnEntry.scale)을 따라 커지지만 attackRange는 숫자라 따라가지 않는다.\n" +
             "그래서 인스펙터에서 손으로 맞춰도 방에 스폰되는 순간 다시 어긋난다.\n" +
             "히트박스가 없는 원거리 적은 이 값과 무관하게 인스펙터 값을 그대로 쓴다.")]
    [SerializeField] protected bool matchAttackRangeToHitBox = true;

    [Tooltip("자동 계산에 더할 여유. 거리는 서로의 중심끼리 재므로 플레이어 몸 반지름\n" +
             "(캡슐 폭 0.8 → 0.4)만큼 줘야 몸이 닿는 순간 공격이 시작된다")]
    [SerializeField] protected float attackRangePadding = 0.4f;

    protected AttackRangeSet attackRangeSet;
    protected EnemyHitBox hitBox;

    public bool IsRunning => isAttacking;
    public bool IsGroundOnly => groundOnly;

    // 좌우 공격이면 높이가 맞아야 실제로 닿는다. 안 맞으면 계속 접근해 줄을 맞춘다.
    public override bool IsAttackAligned()
    {
        if (!horizontalAttackOnly || player == null) return true;
        return Mathf.Abs(player.position.y - transform.position.y) <= verticalTolerance;
    }

    // 플레이어 쪽 방향. 좌우 공격이면 x 부호만 남긴다.
    protected Vector2 AimDirection()
    {
        Vector2 raw = player != null
            ? ((Vector2)(player.position - transform.position))
            : (IsFacingRight ? Vector2.right : Vector2.left);

        if (!horizontalAttackOnly) return raw.sqrMagnitude > 0.0001f ? raw.normalized : Vector2.right;

        return raw.x >= 0f ? Vector2.right : Vector2.left;
    }

    protected override void Start()
    {
        base.Start();

        // 근접 히트박스/범위 표시는 선택적 (원거리 적은 사용 안 함)
        if (attackPivot != null) attackRangeSet = attackPivot.GetComponent<AttackRangeSet>();
        if (attackHitBox != null)
        {
            hitBox = attackHitBox.GetComponent<EnemyHitBox>();
            if (hitBox != null) hitBox.Initialize(this);
            attackHitBox.SetActive(false);
            ShrinkHitBox();
        }
        if (attackRangeBox != null) attackRangeBox.SetActive(false);

        OnStart();

        // 스폰 시 바뀐 localScale은 Start 시점엔 이미 적용돼 있으므로 여기서 재면 실제 크기가 반영된다.
        MatchAttackRangeToHitBox();
    }

    // 파생 클래스 초기화 훅
    protected virtual void OnStart() { }

    [Header("판정 여유")]
    [Tooltip("실제 맞는 상자를 보이는 범위보다 이 비율만큼 줄인다. 0.1이면 10% 작게.\n"
           + "예고 표시와 베기 이펙트는 원래 크기 그대로라, 가장자리에서 아슬아슬하게 피한 것이 실제로 빗나가 준다")]
    [Range(0f, 0.5f)] [SerializeField] protected float hitBoxShrink = 0.1f;

    // 줄이기 전 콜라이더 크기(로컬). 월드 크기는 그때그때 배율을 곱해 낸다 —
    // 적은 크기 0에서 부풀며 스폰되므로, Start에서 잰 월드 크기를 붙들면 0이 박힌다.
    private Vector2 hitBoxLocalSize;

    // 줄이기 전, 화면에 보이는 크기(월드 단위). 예고와 이펙트는 이 값을 쓴다.
    public Vector2 HitBoxDisplaySize
    {
        get
        {
            if (attackHitBox == null || hitBoxLocalSize.x <= 0.0001f) return Vector2.zero;

            Vector3 ls = attackHitBox.transform.lossyScale;
            return new Vector2(hitBoxLocalSize.x * Mathf.Abs(ls.x), hitBoxLocalSize.y * Mathf.Abs(ls.y));
        }
    }

    [Tooltip("근접 판정을 부채꼴로 만든다. 끄면 예전처럼 네모 상자로 때린다.\n"
           + "칼은 호를 그리며 지나가므로, 네모로 때리면 모서리에서 '안 닿았는데 맞았다'가 나온다")]
    [SerializeField] protected bool fanHitBox = true;

    private void ShrinkHitBox()
    {
        var box = attackHitBox.GetComponent<BoxCollider2D>();
        if (box == null) return;

        hitBoxLocalSize = box.size;

        float keep = 1f - Mathf.Clamp01(hitBoxShrink);
        if (!fanHitBox)
        {
            box.size = box.size * keep;
            return;
        }

        // 상자를 부채꼴로 바꾼다. 꼭짓점은 주인(적)의 발밑 —
        // 히트박스는 앞으로 밀려 있는 자식이라, 그 자리를 로컬 좌표로 되짚는다.
        Vector3 ls = attackHitBox.transform.localScale;
        var apex = new Vector2(
            -attackHitBox.transform.localPosition.x / Mathf.Max(0.0001f, ls.x),
            -attackHitBox.transform.localPosition.y / Mathf.Max(0.0001f, ls.y));

        // 반각은 로컬 값만으로 나온다 — 배율은 위아래로 똑같이 곱해져 약분된다
        float halfAngle = Mathf.Atan2(hitBoxLocalSize.y * 0.5f, hitBoxLocalSize.x);
        float radius = hitBoxLocalSize.x * keep;

        var poly = attackHitBox.GetComponent<PolygonCollider2D>();
        if (poly == null) poly = attackHitBox.gameObject.AddComponent<PolygonCollider2D>();
        poly.isTrigger = true;
        poly.points = WedgePoints(apex, radius, halfAngle, 12);

        // 상자는 지운다. EnemyHitBox가 Collider2D 하나를 집어 쓰므로 둘이 남으면 엉뚱한 쪽을 본다
        Destroy(box);

        ShowFanDebug(apex, radius, halfAngle);
    }

    private static Vector2[] WedgePoints(Vector2 apex, float radius, float halfAngle, int segments)
    {
        var pts = new Vector2[segments + 2];
        pts[0] = apex;

        for (int i = 0; i <= segments; i++)
        {
            float a = -halfAngle + (halfAngle * 2f) * i / segments;
            pts[i + 1] = apex + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
        return pts;
    }

    // 디버그(F2)에서 보이는 그림도 부채꼴로 바꾼다 — 네모가 그려지면 판정과 다른 말을 한다.
    // 사거리 표시(AttackRange)도 같이 바꾼다. 판정만 부채꼴이고 옆에 네모가 남으면
    // 어느 쪽이 진짜인지 알 수 없다.
    private void ShowFanDebug(Vector2 apex, float radius, float halfAngle)
    {
        SwapToFan(attackHitBox, "DebugHitFan", apex, radius, halfAngle);

        if (attackRangeBox != null)
        {
            // 사거리는 줄이지 않은 크기로 보여 준다 — 여기까지 닿는다는 뜻이라
            float full = hitBoxLocalSize.x;
            SwapToFan(attackRangeBox, "DebugRangeFan", apex, full, halfAngle);
        }
    }

    private void SwapToFan(GameObject host, string name, Vector2 apex, float radius, float halfAngle)
    {
        var boxSprite = host.GetComponent<SpriteRenderer>();
        if (boxSprite != null) boxSprite.enabled = false;

        if (host.transform.Find(name) != null) return;

        var go = new GameObject(name);
        go.transform.SetParent(host.transform, false);
        go.transform.localPosition = apex;
        go.transform.localScale = Vector3.one * radius * 2f;
        go.AddComponent<DebugVisualPart>();

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = DangerZone.WedgeEdgeSprite(halfAngle * Mathf.Rad2Deg);
        sr.color = boxSprite != null ? boxSprite.color : new Color(1f, 0.3f, 0.3f, 0.65f);
        sr.sortingLayerName = "Skill";
        sr.sortingOrder = boxSprite != null ? boxSprite.sortingOrder : 200;
        sr.enabled = DebugBoxManager.Visible;
    }

    // 반경으로 굴리는 피해(장판·솟구치기)도 같은 비율만큼 줄인다
    public float ShrinkRadius(float radius)
    {
        return radius * (1f - Mathf.Clamp01(hitBoxShrink));
    }

    public Vector2 ShrinkSize(Vector2 size)
    {
        return size * (1f - Mathf.Clamp01(hitBoxShrink));
    }

    // ─────────────────────────────────────────────
    // 공격 시작 거리 = 히트박스가 실제로 닿는 거리
    // ─────────────────────────────────────────────

    // 히트박스보다 더 멀리서 시작해야 하는 공격(돌진 등)이 추가로 확보하는 거리.
    protected virtual float ExtraAttackReach => 0f;

    protected virtual void MatchAttackRangeToHitBox()
    {
        if (!matchAttackRangeToHitBox || attackHitBox == null)
            return;

        Transform hitBoxTransform = attackHitBox.transform;

        if (!TryGetHitBoxTip(hitBoxTransform, out Vector3 tip))
            return;

        // 아직 한 번도 공격하지 않아 AttackPivot은 정면(로컬 +x)을 보고 있다.
        // transform.right는 좌우 반전(y 180도)에 따라 뒤집히므로 절대값으로 잡는다.
        float reach = Mathf.Abs(Vector3.Dot(tip - transform.position, transform.right));

        attackRange = reach + attackRangePadding + ExtraAttackReach;

        MatchRangeBoxToHitBox(hitBoxTransform);
    }

    // 히트박스는 꺼져 있어 Collider2D.bounds를 쓸 수 없다. 로컬 값으로 정면 끝점을 직접 구한다.
    private bool TryGetHitBoxTip(Transform hitBoxTransform, out Vector3 tip)
    {
        tip = Vector3.zero;

        Collider2D col = hitBoxTransform.GetComponent<Collider2D>();
        if (col == null)
            return false;

        Vector2 local = col.offset;

        if (col is BoxCollider2D box) local.x += box.size.x * 0.5f;
        else if (col is CircleCollider2D circle) local.x += circle.radius;
        else return false;

        tip = hitBoxTransform.TransformPoint(local);
        return true;
    }

    // 예고 표시를 히트박스와 같은 자리·같은 크기로 맞춘다.
    // 둘 다 AttackPivot의 자식이고 같은 1유닛 사각 스프라이트를 쓰므로 트랜스폼만 복사하면 정확히 겹친다.
    private void MatchRangeBoxToHitBox(Transform hitBoxTransform)
    {
        if (attackRangeBox == null)
            return;

        Transform rangeBox = attackRangeBox.transform;

        // 부모가 다르면 좌표계가 달라 그대로 복사할 수 없다.
        if (rangeBox.parent != hitBoxTransform.parent)
            return;

        rangeBox.localPosition = hitBoxTransform.localPosition;
        rangeBox.localRotation = hitBoxTransform.localRotation;
        rangeBox.localScale = hitBoxTransform.localScale;
    }

    // 베기 이펙트. 판정이 나가는 그 자리에, 그 크기로, 그 방향으로 띄운다.
    // 판정과 그림이 어긋나면 "분명히 피했는데 맞았다"가 된다.
    [Header("베기 이펙트")]
    [Tooltip("공격 판정이 열릴 때 띄울 이펙트 id (VfxLibrary)")]
    [SerializeField] protected string slashVfxId = "SlashHit";
    [Tooltip("좌향 전용 이펙트 id. 좌우 공격만 하는 적이 쓴다")]
    [SerializeField] protected string slashVfxLeftId = "SlashHitLeft";

    // 애니메이션의 타격 이벤트가 다시 올 때까지 기다린다.
    //
    // 연격이 한 번 휘두를 때마다 쓴다. 고정 시간으로 맞추면 애니메이션 속도가 바뀌는 순간
    // 칼과 판정이 따로 놀지만, 이벤트를 기다리면 클립이 빨라지든 느려지든 늘 칼끝에서 터진다.
    public IEnumerator WaitForNextAnimHit(float timeout)
    {
        animHitReceived = false;

        float elapsed = 0f;
        while (!animHitReceived && elapsed < timeout && !isDead)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // 이번 공격이 근접 판정 상자를 여는가. 기본은 연다.
    protected virtual bool OpensHitBox { get { return true; } }

    // 이번 공격이 자기 그림을 따로 그리면 기본 베기는 띄우지 않는다.
    // 둘 다 뜨면 큰 한 방 위에 작은 베기가 겹쳐 지저분해진다.
    protected virtual bool ShouldSpawnSlash { get { return true; } }

    protected void SpawnSlash(Vector2 dir)
    {
        if (attackHitBox == null || !ShouldSpawnSlash) return;

        // 좌향 전용 그림이 따로 있으면 그걸 쓰고, 없으면 같은 그림을 좌우로 뒤집는다.
        bool left = horizontalAttackOnly && dir.x < 0f;
        bool hasLeftArt = !string.IsNullOrEmpty(slashVfxLeftId) && slashVfxLeftId != slashVfxId;

        // 좌우 공격만 하는 적은 좌향일 때 180도 돌려 쓰고, 8방향 공격은 겨눈 각도 그대로 쓴다
        float angle = 0f;
        if (!horizontalAttackOnly) angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        else if (left && !hasLeftArt) angle = 180f;

        string id = left && hasLeftArt ? slashVfxLeftId : slashVfxId;

        // 공격이 덮을 자리를 아는 적(보스)은 예고한 그 칸을 그대로 채운다.
        // 예고는 길게 깔아 놓고 베기는 코앞에서만 나면 "저 끝은 안전한가"를 알 수 없다.
        Vector3 areaCenter; float areaAngle; Vector2 areaSize;
        if (TryGetSlashArea(dir, out areaCenter, out areaAngle, out areaSize))
        {
            var wide = PixelVfx.PlayStretched(id, areaCenter, areaAngle, areaSize);
            if (wide != null && SlashFollowsOwner) wide.Follow(transform);
            return;
        }

        var slash = PixelVfx.Play(id, attackHitBox.transform.position, angle);
        if (slash == null) return;

        // 돌진처럼 스스로 움직이는 공격은 그림도 같이 가야 한다.
        // 제자리에 남으면 "베고 나서 따로 이동한다"로 읽힌다.
        if (SlashFollowsOwner) slash.Follow(transform);

        // 보이는 높이에 맞춰 키운다. 줄어든 콜라이더가 아니라 원래 크기를 기준으로 한다 —
        // 판정만 줄이고 그림은 그대로여야 한다.
        float h = HitBoxDisplaySize.y;
        if (h <= 0.001f)
        {
            var box = attackHitBox.GetComponent<BoxCollider2D>();
            if (box != null) h = box.size.y * Mathf.Abs(attackHitBox.transform.lossyScale.y);
        }
        if (h > 0.001f)
        {
            float k = Mathf.Clamp(h / 2f, 0.4f, 2f);
            slash.transform.localScale = Vector3.Scale(slash.transform.localScale, new Vector3(k, k, 1f));
        }


    }

    // 공격 예고 표식. 히트박스 자리에 히트박스 크기로 띄운다.
    // 적을 부모로 삼지 않는다 — 예고는 "여기가 맞는다"는 표시라 적을 따라 움직이면 안 된다.
    private PixelVfx SpawnTelegraph()
    {
        if (attackHitBox == null) return null;

        ClearTelegraph();   // 이전 것이 남아 있으면 먼저 치운다

        var vfx = PixelVfx.Play("AttackTelegraph", attackHitBox.transform.position);
        activeTelegraph = vfx;
        if (vfx == null) return null;

        // 히트박스는 꺼져 있어 bounds를 못 쓴다. 콜라이더 크기 x 스케일로 직접 잰다.
        var box = attackHitBox.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Vector3 ls = attackHitBox.transform.lossyScale;
            float w = box.size.x * Mathf.Abs(ls.x);
            float h = box.size.y * Mathf.Abs(ls.y);
            // 프레임 원본이 2x2 유닛이다. 가로·세로를 따로 늘려 판정 박스 비율을 그대로 따라간다 —
            // 원형으로 두면 가로로 긴 판정과 표시가 어긋나 어디까지 맞는지 알 수 없다.
            vfx.transform.localScale = new Vector3(
                Mathf.Clamp(w / 2f, 0.3f, 5f),
                Mathf.Clamp(h / 2f, 0.3f, 5f),
                1f);
        }
        return vfx;
    }

    public void ShowRange(bool show)
    {
        attackRangeBox.SetActive(show);
    }

    public void Execute()
    {
        if (isAttacking)
            return;

        isAttacking = true;
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        try
        {
            StopMove();
            FaceToPlayer();

            Vector2 dir = AimDirection();
            if (attackRangeSet != null) attackRangeSet.SetDirection(dir);

            // 공격 예고 — 표시 여부와 무관하게 시간은 흐른다
            if (showAttackWarning)
            {
                if (attackRangeBox != null) attackRangeBox.SetActive(true);
                SpawnTelegraph();
            }

            // 예고가 시작되는 시점. 파생이 이번에 쓸 공격을 정하고 위험지역을 띄운다.
            // 공격이 정해지기 전에 예고를 띄우면 "무엇이 올지"를 표시할 방법이 없다.
            OnAttackWarning(dir, attackWarningDuration);

            // 통째로 기다리지 않고 매 프레임 사망을 확인한다.
            // WaitForSeconds로 묶으면 예고 도중 죽어도 그 시간이 다 흐를 때까지
            // 표식이 화면에 남고, 죽은 적이 뒤늦게 공격까지 낸다.
            float warned = 0f;
            while (warned < attackWarningDuration && !isDead)
            {
                warned += Time.deltaTime;
                yield return null;
            }

            if (attackRangeBox != null) attackRangeBox.SetActive(false);
            ClearTelegraph();

            if (isDead) yield break;   // 뒷정리는 finally가 한다

            // 애니메이션을 걸기 직전. 이번 공격이 무엇인지 여기서 정해야
            // 그에 맞는 클립을 재생할 수 있다 (보스의 패턴 선택이 여기 붙는다).
            OnAttackStart();

            // 공격 발동
            animator.SetTrigger("attack");

            // 애니메이션 중간 지점까지 기다렸다 판정을 켠다.
            // 휘두르기 시작과 동시에 맞는 것보다 훨씬 읽기 쉬운 공격이 된다.
            if (waitForAnimationHit)
                yield return WaitForAnimationHit();

            // 근접 판정을 안 쓰는 공격(소환·장판·투사체)은 아예 열지 않는다.
            // 열었다가 파생이 닫는 방식은 그 사이 한 프레임이 새어 붙어 있던 상대가 맞는다.
            if (attackHitBox != null && OpensHitBox)
            {
                hitBox.ResetHit();
                attackHitBox.SetActive(true);
                SpawnSlash(dir);
            }

            // 판정이 실제로 열리는 순간. 예고 표시를 여기서 닫아야 "다 차면 맞는다"가 지켜진다.
            OnAttackHit();

            // 공격 활성 동안의 동작 (돌진 / 제자리 / 발사 등) — 파생 구현
            // 이 구간엔 파생이 속도를 직접 제어하므로 상태머신의 분리 정렬을 끈다.
            IsAttackActive = true;
            // 스스로 이동하는 공격(돌진)만 이 구간에 위치 잠금을 푼다.
            if (!LockPositionDuringActivePhase) SetPositionLocked(false);

            yield return AttackActivePhase(dir);
            IsAttackActive = false;

            if (attackHitBox != null) attackHitBox.SetActive(false);

            // 회복 — 경직 중에도 밀려나지 않게 다시 잠근다
            if (LockPositionWhileAttacking) SetPositionLocked(true);
            yield return new WaitForSeconds(recoverTime);
        }
        finally
        {
            ClearTelegraph();
            SetPositionLocked(false);
            OnAttackFinally();
            lastAttackTime = Time.time;
            isAttacking = false;
            IsAttackActive = false;
        }
    }

    // 이번 공격이 스스로 움직이는가. 그렇다면 베기 그림도 주인을 따라가야 한다.
    protected virtual bool SlashFollowsOwner { get { return false; } }

    // 베기 그림이 덮을 자리. 돌려줄 게 있으면 그 칸을 가로세로 따로 늘려 채운다.
    // 기본은 없음 — 보통 적은 히트박스 높이에 맞춘 균등 배율로 충분하다.
    protected virtual bool TryGetSlashArea(Vector2 dir, out Vector3 center, out float angle, out Vector2 size)
    {
        center = Vector3.zero;
        angle = 0f;
        size = Vector2.zero;
        return false;
    }

    // 예고가 시작될 때 불린다. 기본은 아무것도 하지 않는다.
    protected virtual void OnAttackWarning(Vector2 dirToPlayer, float warningDuration) { }

    // 판정이 열리는 순간 불린다.
    protected virtual void OnAttackHit() { }

    // 애니메이션 이벤트를 기다린다.
    // 이벤트가 없거나(클립에 안 걸었거나 교체됐거나) 늦으면 타임아웃으로 넘어간다 —
    // 공격이 영영 안 나가는 것보다 조금 어긋나는 편이 낫다.
    private IEnumerator WaitForAnimationHit()
    {
        animHitReceived = false;

        float elapsed = 0f;
        while (!animHitReceived && elapsed < hitEventTimeout && !isDead)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // 공격 히트박스가 켜져 있는 동안의 동작. 파생 클래스가 구현.
    protected abstract IEnumerator AttackActivePhase(Vector2 dirToPlayer);

    // 발동 구간에도 위치를 잠글지. 제자리/원거리는 true(기본), 돌진처럼 스스로 이동하면 false.
    protected virtual bool LockPositionDuringActivePhase => LockPositionWhileAttacking;

    // 공격 종료 정리 훅 (예: 돌진의 물리 설정 복구)
    protected virtual void OnAttackFinally() { }

    // 공격 애니메이션을 걸기 직전에 불린다.
    // 이 시점 이후로는 LockPositionDuringActivePhase도 결정돼 있어야 한다.
    protected virtual void OnAttackStart() { }

    // 오브젝트가 꺼지거나 파괴되면 코루틴이 finally 없이 끊긴다.
    // 예고 표식은 적의 자식이 아니라서 그대로 화면에 남으므로 여기서 반드시 지운다.
    protected virtual void OnDisable()
    {
        if (attackHitBox != null) attackHitBox.SetActive(false);
        if (attackRangeBox != null) attackRangeBox.SetActive(false);
        ClearTelegraph();
    }

    private void ClearTelegraph()
    {
        if (activeTelegraph != null) activeTelegraph.Stop();
        activeTelegraph = null;
    }
}
