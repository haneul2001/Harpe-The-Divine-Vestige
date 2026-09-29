using System.Collections;
using UnityEngine;

// 거대 사신의 분신. 두 종류가 있다.
//
//   · 색 분신: 온몸이 색 불꽃에 휩싸인 모습. 색마다 공격이 하나로 정해져 있다
//             (파랑=방사 탄막, 초록=나선 탄막, 빨강=순간이동 베기, 주황=조준 연사).
//             정해진 시간이 지나면 사라지고, 몇 대 맞으면 먼저 흩어진다.
//   · 가짜(진짜 찾기): 본체와 똑같이 생겼지만 그림자가 없다. 한 대 맞으면 폭발한다.
//
// 방의 적 목록에는 들어가지 않는다 — 분신이 남았다고 방이 안 끝나면 안 된다.
// 처형할 수 없다. 공격의 주인은 본체라서, 분신 탄을 받아쳐도 반격은 본체에 들어간다.
public class ReaperClone : Enemy
{
    public enum Kind { Blue, Green, Red, Brown, Decoy }

    public Kind CloneKind { get; private set; }
    public System.Action<ReaperClone> Popped;   // 가짜가 맞아 터질 때

    private ReaperBoss boss;
    private float lifeEnd;
    private Sprite[] loopFrames;
    private float frameT;
    private bool vanishing;

    public static ReaperClone Create(ReaperBoss boss, Kind kind, Vector2 pos, float life, int hp)
    {
        var go = new GameObject(kind == Kind.Decoy ? "ReaperDecoy" : "ReaperClone_" + kind);
        go.layer = LayerMask.NameToLayer("Enemy");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * boss.BodyScale;

        // 가짜는 사신의 애니메이터를 그대로 쓴다. 클립이 "Body" 자식의 그림을 바꾸도록 묶여 있어서
        // 루트에 그림을 달면 애니메이션이 안 먹고 생성 순간의 프레임에 굳는다
        SpriteRenderer sr;
        if (kind == Kind.Decoy)
        {
            var bodyGo = new GameObject("Body");
            bodyGo.layer = go.layer;
            bodyGo.transform.SetParent(go.transform, false);
            sr = bodyGo.AddComponent<SpriteRenderer>();
        }
        else sr = go.AddComponent<SpriteRenderer>();
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        var col = go.AddComponent<CapsuleCollider2D>();
        col.size = new Vector2(1.6f, 3.2f);
        col.offset = new Vector2(0f, 1.8f);

        // 색 분신도 컨트롤러는 단다 — Enemy가 애니메이터를 요구해서 빈 것이 붙으면
        // 시작할 때 SetBool이 "컨트롤러가 없다"는 오류를 낸다. 색 분신의 그림은 루트에 있어서
        // "Body"를 움직이는 클립이 닿지 않으니 모습은 그대로다
        var an = go.GetComponent<Animator>();
        if (an == null) an = go.AddComponent<Animator>();
        an.runtimeAnimatorController = boss.animator.runtimeAnimatorController;

        var c = go.AddComponent<ReaperClone>();
        c.spriteRenderer = sr;
        c.boss = boss;
        c.CloneKind = kind;
        c.maxHp = Mathf.Max(1, hp);
        c.lifeEnd = life > 0f ? Time.time + life : float.MaxValue;
        c.detectRange = 0f;
        typeof(Enemy).GetField("harvestable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(c, false);

        if (kind == Kind.Decoy) sr.sprite = boss.BodySprite;
        else c.loopFrames = boss.CloneFrames(kind);
        if (c.loopFrames != null && c.loopFrames.Length > 0) sr.sprite = c.loopFrames[0];

        c.Died += c.OnDied;
        return c;
    }

    // 사신 클립에 박힌 타격 이벤트를 받는 자리. 가짜가 같은 컨트롤러로 베는 흉내만 내므로 할 일은 없다
    public void AnimAttackHit() { }

    // 분신·가짜는 사신의 일부다 — 베어도 골드·처치 수가 오르지 않는다
    protected override bool CountsAsKill { get { return false; } }

    protected override void Start()
    {
        base.Start();
        if (CloneKind != Kind.Decoy) StartCoroutine(AttackLoop());
        else
        {
            if (animator != null) animator.Play("Emerge", 0, 0f);
            MakeGlow();
        }
        ReaperFx.Burst(transform.position + Vector3.up * 1.2f * boss.BodyScale, ColorName, 1.6f);
    }

    // 상태 기계(추격·공격)를 돌리지 않는다 — 분신은 자기 공격만 한다
    protected override void Update()
    {
        if (isDead || vanishing) return;

        if (loopFrames != null && loopFrames.Length > 0)
        {
            frameT += Time.deltaTime * 8f;
            spriteRenderer.sprite = loopFrames[(int)frameT % loopFrames.Length];
        }

        Transform p = ReaperFx.Player();
        if (p != null && Mathf.Abs(p.position.x - transform.position.x) > 0.05f)
        {
            bool right = p.position.x > transform.position.x;
            if (right != IsFacingRight) SetFacing(right);
        }

        if (boss == null || boss.isDead || Time.time >= lifeEnd) Vanish();
    }

    protected override bool CanBeStaggered { get { return false; } }

    public string ColorName
    {
        get
        {
            switch (CloneKind)
            {
                case Kind.Blue: return "blue";
                case Kind.Green: return "green";
                case Kind.Red: return "red";
                case Kind.Brown: return "brown";
                default: return "aqua";
            }
        }
    }

    private Vector2 Chest { get { return (Vector2)transform.position + Vector2.up * 1.6f * boss.BodyScale; } }

    private IEnumerator AttackLoop()
    {
        yield return ReaperFx.Wait(this, 0.9f);
        while (!isDead && !vanishing && boss != null && !boss.isDead)
        {
            switch (CloneKind)
            {
                case Kind.Blue:
                    yield return ReaperFx.Radial(boss, () => Chest, 12, 2, 0.55f, 5.5f, 8, "blue"); break;
                case Kind.Green:
                    yield return ReaperFx.Spiral(boss, () => Chest, 1.6f, 3, 6f, 110f, 5f, 7, "green"); break;
                case Kind.Brown:
                    yield return ReaperFx.Aimed(boss, () => Chest, 4, 0.22f, 10f, 8, "brown"); break;
                case Kind.Red:
                    yield return RedSlash(); break;
            }
            yield return ReaperFx.Wait(this, 2.2f);
        }
    }

    // 빨강: 사라졌다가 플레이어 옆에서 튀어나와 벤다
    private IEnumerator RedSlash()
    {
        ReaperFx.Burst(Chest, "red", 1.4f);
        SetVisible(false);
        yield return ReaperFx.Wait(this, 0.35f);
        if (isDead) yield break;

        Vector2 pp = ReaperFx.PlayerPos(transform.position) - Vector2.up * 0.4f;
        Vector2 at = boss.ClampToArena(pp + Random.insideUnitCircle.normalized * 2.8f, 1f);
        transform.position = at;
        SetVisible(true);
        ReaperFx.Burst(Chest, "red", 1.4f);

        Vector2 d = pp - at;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        DangerZone zone = DangerZone.Sector(at, 4f, 70f, angle, 0.75f);
        yield return ReaperFx.Wait(this, 0.75f);
        if (isDead) { if (zone != null) zone.Cancel(); yield break; }
        ReaperFx.CrescentHit(boss, at, 4f, 70f, angle, 12);
    }

    // 가짜는 아주 살짝 밝다 — 같은 그림을 흰 단색으로 옅게 한 겹 더 그린다 (피격 번쩍임과 같은 머티리얼).
    // 진짜와 나란히 놓고 보면 겨우 구별되는 정도. 그림자만으로는 너무 어렵다는 의견을 반영했다.
    // 선형 색 공간이라 어두운 몸에 흰색을 조금만 얹어도 크게 밝아진다 — 0.1이면 유령처럼 하얘졌고 0.02도 또렷했다
    private const float DecoyGlow = 0.01f;
    private SpriteRenderer glow;

    private void MakeGlow()
    {
        var mat = Resources.Load<Material>("VFX/M_HitFlash");
        if (mat == null || spriteRenderer == null) return;
        var go = new GameObject("DecoyGlow");
        go.transform.SetParent(spriteRenderer.transform, false);
        go.layer = spriteRenderer.gameObject.layer;
        glow = go.AddComponent<SpriteRenderer>();
        glow.sharedMaterial = mat;
        glow.color = new Color(1f, 1f, 1f, DecoyGlow);
    }

    private void LateUpdate()
    {
        if (glow == null || spriteRenderer == null) return;
        glow.sprite = spriteRenderer.sprite;
        glow.flipX = spriteRenderer.flipX;
        glow.enabled = spriteRenderer.enabled;
        glow.sortingLayerID = spriteRenderer.sortingLayerID;
        glow.sortingOrder = spriteRenderer.sortingOrder + 1;
    }

    private void SetVisible(bool on)
    {
        if (spriteRenderer != null) spriteRenderer.enabled = on;
        foreach (var c in GetComponents<Collider2D>()) c.enabled = on;
    }

    public void Vanish()
    {
        if (vanishing) return;
        vanishing = true;
        if (!isDead) ReaperFx.Burst(Chest, ColorName, 1.4f);
        Destroy(gameObject);
    }

    private void OnDied(Enemy _)
    {
        if (CloneKind == Kind.Decoy && Popped != null) Popped(this);
        ReaperFx.Burst(Chest, ColorName, 1.8f);
        vanishing = true;
        Destroy(gameObject);
    }
}
