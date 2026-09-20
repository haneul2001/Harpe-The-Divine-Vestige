using UnityEngine;

// 플레이어가 입히는 피해의 종류. 특성 효과가 "어떤 공격에만 붙는지"를 가르는 데 쓴다.
public enum DamageKind
{
    Basic,          // 평타 1·2타
    Finisher,       // 평타 막타(3타)
    Charged,        // 차징 공격
    DashAttack,     // 대시 공격(X)
    Parry,          // 패링 반격
    Projectile,     // 특성이 쏘는 투사체 (검기·파편·영혼탄)
    Area,           // 특성이 일으키는 광역 (폭발·장판·충격파)
    Skill,          // 스킬
}

public static class DamageKindUtil
{
    // 근접 공격 — "공격 적중 시" 계열 효과가 발동하는 공격
    public static bool IsMelee(this DamageKind k)
    {
        return k == DamageKind.Basic || k == DamageKind.Finisher || k == DamageKind.Charged || k == DamageKind.DashAttack;
    }

    // 평타 계열 — "다음 공격" 계열 효과가 소모되는 공격
    public static bool IsAttack(this DamageKind k)
    {
        return k == DamageKind.Basic || k == DamageKind.Finisher || k == DamageKind.Charged || k == DamageKind.DashAttack;
    }
}

// 피해 한 번을 계산할 때 효과에게 묻는 정보
public struct DamageQuery
{
    public DamageKind kind;
    public Enemy target;        // 광역처럼 대상이 여럿이면 null일 수 있다
    public bool consume;        // 실제 타격이면 true — "다음 공격 1회" 같은 효과가 여기서 소모된다

    public DamageQuery(DamageKind kind, Enemy target, bool consume)
    {
        this.kind = kind;
        this.target = target;
        this.consume = consume;
    }
}

// 플레이어가 적을 맞힌 사실
public struct HitInfo
{
    public DamageKind kind;
    public Enemy enemy;
    public int damage;
    public bool critical;
    public Vector3 point;       // 맞은 자리 (적 몸통 중심)
    public Vector3 from;        // 공격이 날아온 자리
    public bool echo;           // 환영 투사체(복제탄)가 맞힌 것 — 복제탄은 다시 복제하지 않는다
}

// 평타 한 번이 휘둘러진 사실 (적을 맞혔든 안 맞혔든)
public struct SwingInfo
{
    public DamageKind kind;     // Basic / Finisher / Charged
    public Vector3 origin;      // 몸 중심
    public float angle;         // 공격 방향 (도, → 0 반시계)
}

// 게임 코드 ↔ 특성 효과 사이의 유일한 창구.
//
// 게임 쪽(전투·이동·은신·처형…)은 특성이 무엇이 있는지 모른다. 여기 있는 함수만 부른다:
//   · Notify…  : "이런 일이 일어났다" (적중, 처치, 패링 성공, 대시, 소울 획득 …)
//   · 수치 조회 : "지금 피해 배율/공속/이속/치명타 보너스는?"
// 플레이어가 없거나 특성이 하나도 없으면 전부 기본값(배율 1, 보너스 0)을 돌려준다.
public static class AbilityHooks
{
    public static AbilityEffectRunner Runner { get; internal set; }

    // ─── 수치 조회 ───

    // 피해 배율 (여러 효과는 곱한다)
    public static float DamageMultiplier(DamageKind kind, Enemy target, bool consume)
    {
        return Runner != null ? Runner.DamageMultiplier(new DamageQuery(kind, target, consume)) : 1f;
    }

    // 무조건 치명타인가
    public static bool ForcesCrit(DamageKind kind)
    {
        return Runner != null && Runner.ForcesCrit(kind);
    }

    public static float AttackSpeedBonus()   { return Runner != null ? Runner.Sum(e => e.AttackSpeedBonus(Runner.Context)) : 0f; }
    public static float MoveSpeedBonus()     { return Runner != null ? Runner.Sum(e => e.MoveSpeedBonus(Runner.Context)) : 0f; }
    public static float CritRateBonus()      { return Runner != null ? Runner.Sum(e => e.CritRateBonus(Runner.Context)) : 0f; }
    public static float CritDamageBonus()    { return Runner != null ? Runner.Sum(e => e.CritDamageBonus(Runner.Context)) : 0f; }
    public static int   MaxHpBonus()         { return Runner != null ? Mathf.RoundToInt(Runner.Sum(e => e.MaxHpBonus(Runner.Context))) : 0; }
    public static int   DefenseBonus()       { return Runner != null ? Mathf.RoundToInt(Runner.Sum(e => e.DefenseBonus(Runner.Context))) : 0; }
    public static float HarvestThresholdBonus() { return Runner != null ? Runner.Sum(e => e.HarvestThresholdBonus(Runner.Context)) : 0f; }
    public static float HarvestSoulBonus()   { return Runner != null ? Runner.Sum(e => e.HarvestSoulBonus(Runner.Context)) : 0f; }
    public static bool  ProjectilesPierce()  { return Runner != null && Runner.Any(e => e.GrantsProjectilePierce(Runner.Context)); }

    // 세트 시너지
    public static float StatAmplify()        { return 1f + (Runner != null ? Runner.Sum(e => e.StatAmplify(Runner.Context)) : 0f); }
    public static float ProjectileSizeMult() { return 1f + (Runner != null ? Runner.Sum(e => e.ProjectileSizeBonus(Runner.Context)) : 0f); }
    public static float ProjectileRangeMult(){ return 1f + (Runner != null ? Runner.Sum(e => e.ProjectileRangeBonus(Runner.Context)) : 0f); }
    public static float SoulGainMult()       { return 1f + (Runner != null ? Runner.Sum(e => e.SoulGainBonus(Runner.Context)) : 0f); }
    public static float DashCooldownMult()   { return Mathf.Max(0.1f, 1f - (Runner != null ? Runner.Sum(e => e.DashCooldownReduction(Runner.Context)) : 0f)); }
    public static float ParryLockReduction() { return Runner != null ? Runner.Sum(e => e.ParryLockReduction(Runner.Context)) : 0f; }
    public static float SkillCooldownReduction(PlayerSkill skill) { return Runner != null ? Runner.Sum(e => e.SkillCooldownReduction(Runner.Context, skill)) : 0f; }
    public static bool  HarvestInvincible()  { return Runner != null && Runner.Any(e => e.GrantsHarvestInvincibility(Runner.Context)); }

    // 평타 전진 거리 추가분. 0보다 크면 이동키를 안 눌러도 전진한다. consume이면 1회용 효과가 소모된다
    public static float LungeBonus(bool consume)
    {
        return Runner != null ? Runner.Sum(e => e.LungeBonus(Runner.Context, consume)) : 0f;
    }

    // ─── 사건 알림 ───

    public static void NotifyHit(HitInfo hit)             { if (Runner != null) Runner.Dispatch(e => e.OnEnemyHit(Runner.Context, hit)); }
    public static void NotifySwing(SwingInfo swing)       { if (Runner != null) Runner.Dispatch(e => e.OnSwing(Runner.Context, swing)); }
    public static void NotifyParrySuccess(Enemy attacker) { if (Runner != null) Runner.Dispatch(e => e.OnParrySuccess(Runner.Context, attacker)); }
    public static void NotifyDashStart(Vector2 start, Vector2 dir) { if (Runner != null) Runner.Dispatch(e => e.OnDashStart(Runner.Context, start, dir)); }
    public static void NotifyDashEnd(Vector2 start, Vector2 end)   { if (Runner != null) Runner.Dispatch(e => e.OnDashEnd(Runner.Context, start, end)); }
    public static void NotifyStealthEnter()               { if (Runner != null) Runner.Dispatch(e => e.OnStealthEnter(Runner.Context)); }
    public static void NotifyStealthExit()                { if (Runner != null) Runner.Dispatch(e => e.OnStealthExit(Runner.Context)); }
    public static void NotifySoulGained(int amount)       { if (Runner != null) Runner.Dispatch(e => e.OnSoulGained(Runner.Context, amount)); }

    // ─── 피해 계산 공용 ───

    // 스탯으로 굴린 피해 × 계수 × 특성 배율. 치명타도 여기서 정한다 (무조건 치명타 효과 포함)
    public static int RollDamage(PlayerStatus status, float coefficient, DamageKind kind, Enemy target, out bool crit)
    {
        crit = false;
        if (status == null || status.Stats == null) return 1;

        crit = status.Stats.RollCritChance() || ForcesCrit(kind);
        float dmg = status.Stats.RollPhysicalDamage(crit) * coefficient * DamageMultiplier(kind, target, true);
        return Mathf.Max(1, Mathf.RoundToInt(dmg));
    }

    // 적에게 피해를 넣고, 분홍 피격 이펙트를 띄우고, 적중을 알린다
    public static void Deal(Enemy enemy, int damage, bool crit, DamageKind kind, Vector3 from, Transform player, bool echo = false)
    {
        if (enemy == null || enemy.isDead) return;

        enemy.TakeDamage(damage, crit);

        Bounds body = PlayerHitSparkVfx.BodyBounds(enemy);
        var spark = PlayerHitSparkVfx.On(player);
        if (spark != null) spark.Play(body, from, enemy);

        NotifyHit(new HitInfo
        {
            kind = kind, enemy = enemy, damage = damage, critical = crit,
            point = body.center, from = from, echo = echo,
        });
    }

    private static readonly Collider2D[] buffer = new Collider2D[48];

    // 원 안의 살아 있는 적 (콜라이더가 여러 개여도 한 번씩만)
    public static System.Collections.Generic.List<Enemy> EnemiesInRadius(Vector2 center, float radius)
    {
        var result = new System.Collections.Generic.List<Enemy>();
        int n = Physics2D.OverlapCircleNonAlloc(center, radius, buffer, LayerMask.GetMask("Enemy"));
        for (int i = 0; i < n; i++)
        {
            Enemy e = buffer[i] != null ? buffer[i].GetComponentInParent<Enemy>() : null;
            if (e != null && !e.isDead && !result.Contains(e)) result.Add(e);
        }
        return result;
    }

    public static Enemy NearestEnemy(Vector2 from, float maxDistance, Enemy except = null)
    {
        Enemy best = null;
        float bestD = maxDistance;
        foreach (Enemy e in EnemiesInRadius(from, maxDistance))
        {
            if (e == except) continue;
            float d = Vector2.Distance(from, e.transform.position);
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    // 광역 피해: 반경 안의 적마다 따로 굴린다. 맞힌 수를 돌려준다
    public static int AreaDamage(PlayerStatus status, Vector2 center, float radius, float coefficient, Enemy exclude, Transform player)
    {
        int hits = 0;
        foreach (Enemy e in EnemiesInRadius(center, radius))
        {
            if (e == exclude) continue;
            bool crit;
            int dmg = RollDamage(status, coefficient, DamageKind.Area, e, out crit);
            Deal(e, dmg, crit, DamageKind.Area, center, player);
            hits++;
        }
        return hits;
    }
}
