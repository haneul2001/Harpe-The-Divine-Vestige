using System.Collections;
using UnityEngine;

// 순간이동 베기. 보라 안개로 가라앉았다가 플레이어 등 뒤에서 솟아오르며 초승달을 긋는다.
// 솟아오를 자리와 벨 부채꼴을 미리 깐다 — 나올 곳을 보고 반대로 빠지는 것이 답이다.
// 3페이즈(인덱스 2)부터 세 번 연달아.
public class BossPatternReaperTeleport : BossPattern
{
    [SerializeField] private int repeats = 1;
    [SerializeField] private int repeatsLate = 3;
    [SerializeField] private int lateFromPhase = 2;
    [SerializeField] private float behindDistance = 3f;
    [SerializeField] private float emergeWarn = 0.75f;
    [SerializeField] private float slashRadius = 5f;
    [SerializeField] private float slashHalfAngle = 75f;
    [SerializeField] private int damage = 16;

    public override bool UsesHitBox { get { return false; } }
    public override bool DrawsOwnSlash { get { return true; } }
    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration) { return null; }

    public override DangerShape[] DangerShapes(BossEnemy boss)
    {
        return new DangerShape[] { DangerShape.Sector(slashRadius, slashHalfAngle, DangerOrigin.Player) };
    }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        return RunCount(boss, boss.PhaseIndex >= lateFromPhase ? repeatsLate : repeats);
    }

    // 몇 번 벨지 직접 준다 — 두 번째 목숨의 사신이 다른 공격 끝에 한 번씩 이어 붙인다
    public IEnumerator RunCount(BossEnemy boss, int n)
    {
        var r = boss as ReaperBoss;
        if (r == null) yield break;
        float warn = emergeWarn * (boss.CurrentPhase != null ? Mathf.Max(0.6f, boss.CurrentPhase.warningMultiplier) : 1f);

        for (int i = 0; i < n && !boss.isDead; i++)
        {
            if (i > 0) r.PlayState("TeleportIn");
            yield return ReaperFx.Wait(boss, r.ClipLength("TeleportIn") * (i == 0 ? 0.6f : 0.9f));
            if (boss.isDead) yield break;

            Vector2 from = boss.transform.position;
            r.SetHidden(true);
            ReaperFx.Burst(r.Chest, "purple", 1.8f);

            // 등 뒤 = 플레이어를 사이에 두고 사신이 있던 반대편
            Vector2 p = ReaperFx.PlayerPos(from) - Vector2.up * 0.4f;
            Vector2 away = p - from;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitCircle;
            Vector2 at = r.ClampToArena(p + away.normalized * behindDistance, 1.5f);
            Vector2 toP = p - at;
            float angle = Mathf.Atan2(toP.y, toP.x) * Mathf.Rad2Deg;

            DangerZone mark = DangerZone.Circle(at, 1.3f, warn).Dark();
            DangerZone slash = DangerZone.Sector(at, slashRadius, slashHalfAngle, angle, warn + 0.5f);
            if (slash != null) slash.HoldUntilHit();
            yield return ReaperFx.Wait(boss, warn);
            if (boss.isDead) { if (slash != null) slash.Cancel(); yield break; }

            r.TeleportTo(at);
            r.SetHidden(false);
            ReaperFx.Burst(r.Chest, "purple", 1.8f);
            r.PlayState("TeleportOut");
            yield return boss.WaitForNextAnimHit(0.9f);
            if (slash != null) slash.CompleteNow();
            ReaperFx.CrescentHit(boss, at, slashRadius, slashHalfAngle, angle, damage);
            yield return ReaperFx.Wait(boss, 0.25f);
        }
        if (!boss.isDead) r.PlayState("Idle");
    }
}
