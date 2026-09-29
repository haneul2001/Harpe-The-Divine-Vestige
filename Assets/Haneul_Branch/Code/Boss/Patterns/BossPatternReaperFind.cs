using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 진짜 찾기. 땅속으로 사라졌다가 플레이어를 둘러싸고 넷으로 솟아오른다.
//
// 진짜만 바닥에 그림자를 드리운다. 진짜를 베면 사신이 무너져 기절하고,
// 가짜를 베면 그 자리에서 터진다. 제한 시간 안에 못 찾으면 넷이 한꺼번에 초승달을 긋는다.
public class BossPatternReaperFind : BossPattern
{
    [Tooltip("플레이어를 둘러싸는 타원의 가로·세로 반지름. 사신 키가 7칸이라 위아래로 멀면 화면 밖으로 잘린다")]
    [SerializeField] private Vector2 ringRadius = new Vector2(5f, 2.2f);
    [SerializeField] private float timeLimit = 6f;
    [Tooltip("두 번째 목숨: 사신 수와 제한 시간")]
    [SerializeField] private int countSecondLife = 6;
    [SerializeField] private float timeLimitSecondLife = 5f;
    [SerializeField] private int decoyBlastDamage = 15;
    [SerializeField] private float decoyBlastRadius = 2.2f;
    [SerializeField] private float stunTime = 3f;
    [SerializeField] private float slashRadius = 6f;
    [SerializeField] private float slashHalfAngle = 60f;
    [SerializeField] private float slashWarn = 0.9f;
    [SerializeField] private int slashDamage = 18;

    public override bool UsesHitBox { get { return false; } }
    public override bool DrawsOwnSlash { get { return true; } }
    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration) { return null; }

    public override DangerShape[] DangerShapes(BossEnemy boss)
    {
        return new DangerShape[] { DangerShape.Circle(ringRadius.x, DangerOrigin.Player) };
    }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        var r = boss as ReaperBoss;
        if (r == null) yield break;

        ToastManager.Show("그림자 있는 진짜를 베어라!", ToastManager.Kind.Warn);
        yield return ReaperFx.Wait(boss, r.ClipLength("TeleportIn") * 0.6f);
        if (boss.isDead) yield break;
        r.SetHidden(true);
        ReaperFx.Burst(r.Chest, "purple", 1.8f);
        yield return ReaperFx.Wait(boss, 0.4f);

        // 플레이어를 둘러싼 네 자리
        Vector2 center = ReaperFx.PlayerPos(boss.transform.position) - Vector2.up * 0.4f;
        // 넷이면 대각선 네 자리, 여섯이면 타원을 여섯으로 나눈다(조금씩 흔든다) — 정위·정아래 자리는 몸이 화면을 벗어난다
        int count = r.SecondLife ? Mathf.Max(4, countSecondLife) : 4;
        float limit = r.SecondLife ? timeLimitSecondLife : timeLimit;
        var spots = new List<Vector2>();
        for (int i = 0; i < count; i++)
        {
            float a = count == 4 ? 45f + 90f * i + Random.Range(-15f, 15f) : 360f / count * i + Random.Range(-10f, 10f);
            float k = count == 4 ? 1.414f : 1f;
            a *= Mathf.Deg2Rad;
            spots.Add(r.ClampToArena(center + new Vector2(Mathf.Cos(a) * ringRadius.x, Mathf.Sin(a) * ringRadius.y) * k, 1.2f));
        }
        int real = Random.Range(0, count);

        var decoys = new List<ReaperClone>();
        for (int i = 0; i < count; i++)
        {
            if (i == real) continue;
            var d = r.SpawnClone(ReaperClone.Kind.Decoy, spots[i], 0f, 1);
            if (d == null) continue;
            d.Popped += c =>
            {
                ToastManager.Show("가짜다!", ToastManager.Kind.Warn);
                boss.DamagePlayerInRadius(c.transform.position + Vector3.up * 1.2f, decoyBlastRadius, decoyBlastDamage, true);
                CameraShake.Shake(0.3f);
            };
            decoys.Add(d);
        }
        r.TeleportTo(spots[real]);
        r.SetHidden(false);
        r.PlayState("Emerge");

        r.RealHit = false;
        r.FindActive = true;
        float t = 0f;
        while (t < limit && !boss.isDead && !r.RealHit) { t += Time.deltaTime; yield return null; }
        r.FindActive = false;
        if (boss.isDead) { Clear(decoys); yield break; }

        if (r.RealHit)
        {
            Clear(decoys);
            yield return r.Stun(stunTime, "진짜다! 사신이 무너진다");
            yield break;
        }

        // 못 찾았다 — 넷이 한꺼번에 벤다
        ToastManager.Show("사신들이 일제히 낫을 든다!", ToastManager.Kind.Warn);
        Vector2 target = ReaperFx.PlayerPos(center) - Vector2.up * 0.4f;
        var slashers = new List<Vector2> { boss.transform.position };
        foreach (var d in decoys) if (d != null && !d.isDead) slashers.Add(d.transform.position);

        var zones = new List<DangerZone>();
        var angles = new List<float>();
        foreach (var s in slashers)
        {
            Vector2 dir = target - s;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            angles.Add(ang);
            zones.Add(DangerZone.Sector(s, slashRadius, slashHalfAngle, ang, slashWarn + 0.3f));
        }
        yield return ReaperFx.Wait(boss, slashWarn * 0.5f);
        r.PlayState("Attack");
        foreach (var d in decoys) if (d != null && d.animator != null) d.animator.Play("Attack", 0, 0f);
        yield return ReaperFx.Wait(boss, slashWarn * 0.5f);
        for (int i = 0; i < slashers.Count; i++)
        {
            if (zones[i] != null) zones[i].CompleteNow();
            ReaperFx.CrescentHit(boss, slashers[i], slashRadius, slashHalfAngle, angles[i], slashDamage);
        }
        yield return ReaperFx.Wait(boss, 0.5f);
        Clear(decoys);
        if (!boss.isDead) r.PlayState("Idle");
    }

    private static void Clear(List<ReaperClone> decoys)
    {
        foreach (var d in decoys) if (d != null && !d.isDead) d.Vanish();
        decoys.Clear();
    }
}
