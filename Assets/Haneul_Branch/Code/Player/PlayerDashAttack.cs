using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 대시 공격 (기본 X키 — 나중에 스킬 슬롯으로 옮길 예정).
//
// 공격 방향(8방향)으로 빠르게 미끄러지며 지나가는 길의 몬스터를 한 번씩 벤다.
//  · 맵 밖으로 안 나가게: 이동 전에 플레이어 콜라이더를 경로로 쏴(Cast) 벽·맵 콜라이더까지의 거리를 잰다.
//    매 물리 스텝마다 다시 재서, 도중에 막히면 그 자리에서 멈춘다. 몬스터는 뚫고 지나간다.
//  · 판정: 지나간 길(몸 주변 원) + 끝 지점 베기(평타 판정 박스). 몬스터마다 한 번만 맞는다
//  · 연출: 평타 3타(막타) 애니메이션, 끝 지점에 붉은 베기 이펙트, 맞은 몬스터에는 분홍 피격 이펙트
//  · 잔상: 100PixelVFX energy13 기둥을 눕혀 이동 거리만큼 늘리고 붉게 칠한다. 2배속, 대시가 끝나면 사라진다
public class PlayerDashAttack : MonoBehaviour
{
    [Header("입력 · 쿨타임")]
    [SerializeField] private KeyCode key = KeyCode.X;
    [SerializeField] private float cooldown = 0.9f;

    [Header("이동")]
    [Tooltip("최대 대시 거리(월드 단위). 벽이 가까우면 그 앞에서 멈춘다")]
    [SerializeField] private float distance = 3.5f;
    [Tooltip("대시에 걸리는 시간(초)")]
    [SerializeField] private float duration = 0.14f;
    [Tooltip("벽과 남겨 둘 틈")]
    [SerializeField] private float wallSkin = 0.05f;
    [Tooltip("대시 공격 전체 길이(초) — 이동 + 끝 지점 베기 모션(평타 3타). 끝나면 잔상이 사라지고 조작이 풀린다")]
    [SerializeField] private float totalTime = 0.5f;
    [Tooltip("대시 중(과 직후 조금 더) 무적 시간(초)")]
    [SerializeField] private float invincibleTime = 0.22f;

    [Header("공격")]
    [Tooltip("평타 한 번 굴림 × 이 배율")]
    [SerializeField] private float damageMultiplier = 1.5f;
    [Tooltip("지나가며 몬스터를 맞히는 판정 반경 (몸 중심 기준)")]
    [SerializeField] private float hitRadius = 0.85f;
    [Tooltip("끝 지점 베기 이펙트 재질 (M_Slash_Red — 잔상과 같은 붉은색). 비우면 평타와 같은 색")]
    [SerializeField] private Material slashMaterial;

    [Header("잔상 (100PixelVFX energy13)")]
    [SerializeField] private GameObject trailPrefab;
    [SerializeField] private string trailState = "VFX_energy13";
    [Tooltip("잔상 색 (원본 보라·흰색에 곱해진다)")]
    [SerializeField] private Color trailColor = new Color(1f, 0.28f, 0.25f, 1f);
    [Tooltip("잔상 애니메이션 재생 배율")]
    [SerializeField] private float trailAnimSpeed = 2f;
    [Tooltip("잔상 두께 (월드 단위)")]
    [SerializeField] private float trailThickness = 0.35f;
    [Tooltip("원본 64px 칸에서 기둥이 차지하는 길이(픽셀). energy13은 뿌리 59px ~ 꼭대기 4px")]
    [SerializeField] private float trailBeamPixels = 55f;
    [Tooltip("칸 중심(32px)에서 기둥 뿌리까지(픽셀). 뿌리를 대시 시작점에 맞춘다")]
    [SerializeField] private float trailRootPixels = 27.5f;
    [Tooltip("기둥의 가장 굵은 폭(픽셀)")]
    [SerializeField] private float trailBeamWidthPixels = 17f;

    [Tooltip("몸 중심 높이 (발 기준)")]
    [SerializeField] private float bodyHeight = 0.3f;

    public bool IsDashing { get; private set; }

    // 상태 아이콘(대시 공격 쿨타임) 표시용
    public float Cooldown => cooldown;
    public KeyCode Key => key;
    public float CooldownRemaining => Mathf.Max(0f, nextUseTime - Time.time);

    // 처형하면 쿨타임이 초기화된다 (HarvestManager가 호출)
    public void ResetCooldown()
    {
        nextUseTime = 0f;
    }

    // 세트(잔영): 남은 쿨타임을 줄인다
    public void ReduceCooldown(float seconds)
    {
        nextUseTime -= seconds;
    }

    private Rigidbody2D rb;
    private Collider2D body;
    private PlayerMove move;
    private PlayerCombat combat;
    private PlayerStatus status;
    private PlayerAim aim;
    private PlayerSlashVfx slashVfx;
    private PlayerHitSparkVfx hitSpark;
    private Animator anim;
    private SpriteRenderer sprite;

    private float nextUseTime;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
    private readonly Collider2D[] overlapHits = new Collider2D[16];
    private readonly HashSet<Enemy> struck = new HashSet<Enemy>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<Collider2D>();
        move = GetComponent<PlayerMove>();
        combat = GetComponent<PlayerCombat>();
        status = GetComponent<PlayerStatus>();
        aim = GetComponent<PlayerAim>();
        slashVfx = GetComponent<PlayerSlashVfx>();
        hitSpark = GetComponent<PlayerHitSparkVfx>();
        anim = GetComponentInChildren<Animator>();
        sprite = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(key)) return;
        if (!CanDash()) return;
        StartCoroutine(Dash());
    }

    private bool CanDash()
    {
        if (IsDashing || Time.time < nextUseTime) return false;
        if (move == null || rb == null) return false;
        if (move.isExecuting || move.inputLocked || move.IsControlLocked) return false;   // 처형·패링 반격·UI·패링 직후
        if (status != null && status.IsDead) return false;
        return true;
    }

    private IEnumerator Dash()
    {
        IsDashing = true;
        nextUseTime = Time.time + cooldown;
        struck.Clear();

        // 휘두르던 평타는 끊고 대시로
        if (combat != null) combat.CancelAttack();
        PlayerStealth.BreakStealth();

        Vector2 dir = DashDirection();
        float angle = Mathf.Round(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg / 45f) * 45f;
        if (Mathf.Abs(dir.x) > 0.01f && sprite != null) sprite.flipX = dir.x < 0f;

        // 세로는 걷기와 같은 0.7 보정 — 위아래 대시가 옆보다 멀리 가 보이지 않게
        Vector2 velocityDir = new Vector2(dir.x, dir.y * 0.7f);
        float plannedDistance = distance * velocityDir.magnitude;
        velocityDir.Normalize();

        // 조작·일반 이동을 멈추고(PlayerMove가 속도를 0으로 둔다) 여기서 직접 위치를 민다
        move.isExecuting = true;

        // 몬스터(발밑 콜라이더)는 뚫고 지나가야 한다 — 대시 동안만 충돌 제외
        int savedExclude = body != null ? body.excludeLayers.value : 0;
        if (body != null) body.excludeLayers = savedExclude | LayerMask.GetMask("Enemy", "EnemyFoot");
        if (status != null) status.GrantInvincibility(invincibleTime);

        float startTime = Time.time;

        // 애니메이션: 평타 3타(막타) 모션을 빌린다. 평타 판정 이벤트는 대시가 끝날 때까지 막는다
        if (combat != null) combat.PlayExternalAttackAnimation(2, totalTime + 0.3f);

        Vector2 start = rb.position;
        float speed = plannedDistance / Mathf.Max(0.01f, duration);   // 속도는 최대 거리 기준 — 짧게 끊기면 시간도 그만큼 짧다
        float travelled = 0f;

        // 발동 순간 도착 지점을 미리 정한다:
        //  1) 벽·맵까지 갈 수 있는 거리
        //  2) 그 도착 지점에서 캐릭터가 몬스터 콜라이더와 겹치면, 그 몬스터 콜라이더 바깥에서 멈춘다
        //     (겹치지 않으면 중간 몬스터는 전부 관통)
        plannedDistance = PlanDistance(start, velocityDir, plannedDistance);

        Vector3 bodyOffset = new Vector3(0f, bodyHeight, 0f);

        // 잔상은 발동 순간 도착 지점까지 길이로 깔고, 대시가 끝나면 지운다
        GameObject trail = SpawnTrail((Vector3)start + bodyOffset, velocityDir, plannedDistance);

        StrikeAround(rb.position, start);

        while (travelled < plannedDistance)
        {
            yield return new WaitForFixedUpdate();

            float step = Mathf.Min(speed * Time.fixedDeltaTime, plannedDistance - travelled);
            float free = FreeDistance(velocityDir, step);
            bool blocked = free < step;

            Vector2 next = rb.position + velocityDir * Mathf.Max(0f, free);
            rb.MovePosition(next);
            travelled += Mathf.Max(0f, free);

            StrikeAround(next, start);

            if (blocked) break;   // 벽 — 그 자리에서 대시 끝
        }

        yield return new WaitForFixedUpdate();

        Vector2 end = rb.position;
        if (body != null) body.excludeLayers = savedExclude;

        // 도중에 막혀 계획보다 덜 갔으면 잔상도 실제 거리로 줄인다
        float moved = Vector2.Distance(start, end);
        if (trail != null && moved < plannedDistance - 0.05f)
            FitTrail(trail, (Vector3)start + bodyOffset, velocityDir, moved);

        // 끝 지점에서 베기 — 평타처럼 공격 방향 앞쪽에 이펙트, 그 박스 범위도 판정
        StrikeFinish(end, angle, start);

        // 3타 모션이 끝날 때까지 기다렸다가 풀어 준다
        float remain = totalTime - (Time.time - startTime);
        if (remain > 0f) yield return new WaitForSeconds(remain);

        if (trail != null) Destroy(trail);
        if (combat != null) combat.EndExternalAttack();
        move.isExecuting = false;
        IsDashing = false;
    }

    // 끝 지점 베기: 평타 판정 박스(PlayerCombat.boxSize)를 공격 방향으로 눕혀 판정하고 베기 이펙트를 그린다
    private void StrikeFinish(Vector2 feet, float angle, Vector2 from)
    {
        float rad = angle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        float reach = combat != null ? combat.AttackReachWorld : 0.8f;
        float pivotY = combat != null ? combat.AttackPivotYWorld : bodyHeight;
        Vector2 boxCenter = feet + new Vector2(0f, pivotY) + dir * reach;
        Vector2 boxSize = combat != null ? combat.boxSize : new Vector2(3.2f, 2f);

        if (slashVfx != null) slashVfx.Play(boxCenter, angle, false, slashMaterial);

        int n = Physics2D.OverlapBoxNonAlloc(boxCenter, boxSize, angle, overlapHits, LayerMask.GetMask("Enemy"));
        for (int i = 0; i < n; i++) HitEnemy(overlapHits[i], from);
    }

    // 누르고 있는 방향키가 있으면 그쪽(8방향), 없으면 조준(마지막 공격 방향)
    private Vector2 DashDirection()
    {
        Vector2 raw = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        if (raw.sqrMagnitude > 0.01f)
        {
            float a = Mathf.Atan2(raw.y, raw.x) * Mathf.Rad2Deg;
            int i = ((Mathf.RoundToInt(a / 45f) % 8) + 8) % 8;
            return PlayerAim.Directions8[i];
        }
        if (aim != null) return aim.Direction;
        return sprite != null && sprite.flipX ? Vector2.left : Vector2.right;
    }

    private float PlanDistance(Vector2 start, Vector2 dir, float maxDistance)
    {
        if (body == null) return maxDistance;

        float reach = FreeDistance(dir, maxDistance);

        // 도착 지점에서 캐릭터 콜라이더가 몬스터와 겹치는지 — 콜라이더를 그 자리로 옮겨 보는 대신,
        // 경로를 따라 몬스터 콜라이더를 쏴서 "reach 안에서 시작해 reach 너머까지 걸쳐 있는" 콜라이더를 찾는다
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(LayerMask.GetMask("Enemy", "EnemyFoot"));
        int n = body.Cast(dir, filter, castHits, reach);

        float stop = reach;
        for (int i = 0; i < n; i++)
        {
            var hit = castHits[i];
            if (hit.collider == null) continue;
            if (hit.collider.GetComponentInParent<Enemy>() is Enemy e && e.isDead) continue;

            // 이 몬스터 콜라이더가 도착 지점의 캐릭터 콜라이더와 겹치면, 그 몬스터에 닿기 직전에서 멈춘다
            if (OverlapsAt(start + dir * reach, hit.collider))
                stop = Mathf.Min(stop, Mathf.Max(0f, hit.distance - wallSkin));
        }
        return stop;
    }

    private readonly Collider2D[] destHits = new Collider2D[16];

    // 캐릭터 콜라이더를 pos로 옮겼을 때 other와 겹치는지 (실제로 옮기지 않고, 같은 모양으로 겹침 검사)
    private bool OverlapsAt(Vector2 pos, Collider2D other)
    {
        int mask = 1 << other.gameObject.layer;
        Vector3 s = transform.lossyScale;
        Vector2 scale = new Vector2(Mathf.Abs(s.x), Mathf.Abs(s.y));
        int n;

        if (body is CapsuleCollider2D cap)
        {
            Vector2 center = pos + Vector2.Scale(cap.offset, scale);
            n = Physics2D.OverlapCapsuleNonAlloc(center, Vector2.Scale(cap.size, scale), cap.direction, 0f, destHits, mask);
        }
        else
        {
            Bounds b = body.bounds;
            Vector2 center = (Vector2)b.center + (pos - rb.position);
            n = Physics2D.OverlapBoxNonAlloc(center, b.size, 0f, destHits, mask);
        }

        for (int i = 0; i < n; i++)
            if (destHits[i] == other) return true;
        return false;
    }

    // 몸 콜라이더를 dir로 step만큼 밀었을 때 벽·맵에 닿기 전까지 갈 수 있는 거리.
    // 몬스터에 딸린 콜라이더(Default 레이어의 BackPosition 등)는 벽으로 치지 않는다.
    private float FreeDistance(Vector2 dir, float step)
    {
        if (body == null) return step;

        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(BlockingMask());

        int n = body.Cast(dir, filter, castHits, step + wallSkin);
        float free = step;
        for (int i = 0; i < n; i++)
        {
            var c = castHits[i].collider;
            if (c == null || c.GetComponentInParent<Enemy>() != null) continue;
            if (c.attachedRigidbody != null && c.attachedRigidbody == rb) continue;
            free = Mathf.Min(free, castHits[i].distance - wallSkin);
        }
        return Mathf.Max(0f, free);
    }

    // 플레이어가 원래 부딪히는 레이어 중, 몬스터(발밑·몸)·플레이어·이펙트는 뚫고 지나간다
    private int BlockingMask()
    {
        int mask = Physics2D.GetLayerCollisionMask(gameObject.layer);
        mask &= ~LayerMask.GetMask("Player", "Enemy", "EnemyFoot", "Skill", "UI");
        return mask;
    }

    private void StrikeAround(Vector2 feet, Vector2 from)
    {
        int enemyMask = LayerMask.GetMask("Enemy");
        Vector2 center = feet + new Vector2(0f, bodyHeight);
        int n = Physics2D.OverlapCircleNonAlloc(center, hitRadius, overlapHits, enemyMask);

        for (int i = 0; i < n; i++) HitEnemy(overlapHits[i], from);
    }

    // 대시 한 번에 몬스터마다 한 번만 맞는다 (지나간 길 + 끝 지점 베기 합쳐서)
    private void HitEnemy(Collider2D col, Vector2 from)
    {
        var enemy = col.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.isDead || !struck.Add(enemy)) return;

        bool crit;
        int dmg = AbilityHooks.RollDamage(status, damageMultiplier, DamageKind.DashAttack, enemy, out crit);

        enemy.TakeDamage(dmg, crit);

        // 대시 시작점 → 몬스터 방향으로 몬스터 외곽에 분홍 피격 이펙트
        Vector3 fromBody = (Vector3)from + new Vector3(0f, bodyHeight, 0f);
        if (hitSpark != null) hitSpark.Play(col.bounds, fromBody, enemy);

        AbilityHooks.NotifyHit(new HitInfo
        {
            kind = DamageKind.DashAttack, enemy = enemy, damage = dmg, critical = crit,
            point = col.bounds.center, from = fromBody,
        });
    }

    // energy13 기둥을 대시 방향으로 눕혀, 뿌리는 시작점에 두고 도착 지점까지 늘린다.
    // 대시가 끝나면 Dash()가 직접 지운다 — 여기서는 수명을 걸지 않는다.
    private GameObject SpawnTrail(Vector3 start, Vector2 dir, float length)
    {
        if (trailPrefab == null || length < 0.1f) return null;

        GameObject go = Instantiate(trailPrefab);
        var sr = go.GetComponent<SpriteRenderer>();

        FitTrail(go, start, dir, length);

        if (sr != null)
        {
            sr.color = trailColor;
            if (sprite != null)
            {
                // 캐릭터 바로 뒤 — 몸을 가리지 않게
                sr.sortingLayerID = sprite.sortingLayerID;
                sr.sortingOrder = sprite.sortingOrder - 1;
            }
        }

        var animator = go.GetComponent<Animator>();
        if (animator != null)
        {
            animator.Play(trailState, 0, 0f);
            animator.speed = trailAnimSpeed;
        }

        // 혹시 대시가 중간에 끊겨(컴포넌트 비활성 등) 못 지워도 남지 않게 안전장치
        Destroy(go, totalTime + 1f);
        return go;
    }

    private void FitTrail(GameObject go, Vector3 start, Vector2 dir, float length)
    {
        var sr = go.GetComponent<SpriteRenderer>();

        // 시트의 PPU(100PixelVFX는 32)로 픽셀 치수를 월드 단위로 바꾼다 — PPU를 가정하면 길이가 어긋난다
        float ppu = sr != null && sr.sprite != null ? sr.sprite.pixelsPerUnit : 32f;
        float beamUnits = trailBeamPixels / ppu;
        float rootUnits = trailRootPixels / ppu;
        float widthUnits = trailBeamWidthPixels / ppu;

        float lengthScale = Mathf.Max(0.01f, length) / Mathf.Max(0.01f, beamUnits);
        float widthScale = trailThickness / Mathf.Max(0.01f, widthUnits);

        // 원본 기둥은 +Y로 서 있으므로 (방향 각 - 90도) 돌린다
        float rot = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        go.transform.rotation = Quaternion.Euler(0f, 0f, rot);
        go.transform.localScale = new Vector3(widthScale, lengthScale, 1f);
        go.transform.position = start + (Vector3)(dir * rootUnits * lengthScale);
    }

    private void OnDisable()
    {
        if (IsDashing && move != null) move.isExecuting = false;
        if (IsDashing && combat != null) combat.EndExternalAttack();
        IsDashing = false;
    }
}
