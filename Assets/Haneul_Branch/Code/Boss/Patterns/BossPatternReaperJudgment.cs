using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 심판 (전멸기). 낫을 치켜든 채 영창하는 동안 방 전체가 보라 장판이 되고,
// 빛나는 안전지대 몇 곳만 남는다. 영창이 끝나는 순간 안전지대 밖은 모두 벌을 받는다(받아칠 수 없다).
public class BossPatternReaperJudgment : BossPattern
{
    [SerializeField] private float channel = 4f;
    [SerializeField] private float channelLate = 3.5f;
    [SerializeField] private int safeCount = 3;
    [SerializeField] private int safeCountLate = 2;
    [SerializeField] private int lateFromPhase = 3;
    [SerializeField] private float safeRadius = 1.6f;
    [Tooltip("두 번째 목숨의 안전지대 반지름")]
    [SerializeField] private float safeRadiusSecondLife = 1.3f;
    [SerializeField] private float safeSpacing = 5f;
    [Tooltip("최대 체력 대비 피해")]
    [Range(0.05f, 1f)] [SerializeField] private float damageRatio = 0.45f;

    public override bool UsesHitBox { get { return false; } }
    public override bool DrawsOwnSlash { get { return true; } }
    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration) { return null; }

    public override DangerShape[] DangerShapes(BossEnemy boss)
    {
        return new DangerShape[] { DangerShape.Circle(safeRadius, DangerOrigin.Player).Scattered(safeCount, safeSpacing) };
    }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        var r = boss as ReaperBoss;
        if (r == null) yield break;

        bool late = boss.PhaseIndex >= lateFromPhase;
        float dur = late ? channelLate : channel;
        int n = late ? safeCountLate : safeCount;
        float safeR = r.SecondLife ? safeRadiusSecondLife : safeRadius;

        ToastManager.Show("심판 — 빛나는 원 안으로!", ToastManager.Kind.Warn);
        r.PlayState("CastLoop");

        var spots = new List<Vector2>();
        int guard = 0;
        while (spots.Count < n && guard++ < 200)
        {
            Vector2 s = r.RandomArenaPoint(safeR + 0.6f);
            bool ok = true;
            foreach (var o in spots) if (Vector2.Distance(o, s) < safeSpacing) { ok = false; break; }
            if (ok) spots.Add(s);
        }

        Rect arena = r.Arena;
        DangerZone all = DangerZone.Box(arena.center, arena.size + Vector2.one * 2f, 0f, dur);
        if (all != null) all.Dark().HoldUntilHit();
        var safes = new List<DangerZone>();
        foreach (var s in spots)
        {
            DangerZone z = DangerZone.Circle(s, safeR, dur);
            if (z != null) safes.Add(z.Safe().HoldUntilHit());
        }

        yield return ReaperFx.Wait(boss, dur);
        if (all != null) all.CompleteNow();
        foreach (var z in safes) if (z != null) z.CompleteNow();
        if (boss.isDead) yield break;

        // 벌 — 방 곳곳이 터진다
        CameraShake.Shake(0.8f);
        for (int i = 0; i < 10; i++) ReaperFx.Burst(r.RandomArenaPoint(0.5f), "purple", 2.2f);

        Transform p = ReaperFx.Player();
        bool safe = false;
        if (p != null)
            foreach (var s in spots)
                if (Vector2.Distance(p.position, s) <= safeR) { safe = true; break; }
        if (p != null && !safe)
        {
            var ps = p.GetComponent<PlayerStatus>();
            int dmg = ps != null ? Mathf.CeilToInt(ps.MaxHp * damageRatio) : 40;
            boss.DamagePlayer(dmg, false);
        }

        yield return ReaperFx.Wait(boss, 0.4f);
        if (!boss.isDead) r.PlayState("Idle");
    }
}
