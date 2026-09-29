using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 색별 분신. 온몸이 색 불꽃에 휩싸인 분신을 불러낸다. 색이 곧 공격이다.
//   파랑 = 방사 탄막 / 초록 = 나선 탄막 / 빨강 = 순간이동 베기 / 주황 = 조준 연사
// 2페이즈부터 두 마리, 4페이즈부터 네 마리. 정해진 시간이 지나거나 몇 대 맞으면 흩어진다.
public class BossPatternReaperClones : BossPattern
{
    [SerializeField] private int count = 2;
    [SerializeField] private int countLate = 4;
    [SerializeField] private int lateFromPhase = 3;
    [SerializeField] private float life = 8f;
    [SerializeField] private float lifeSecondLife = 10f;
    [SerializeField] private int cloneHp = 300;
    [SerializeField] private float minDistanceFromPlayer = 4f;

    public override bool UsesHitBox { get { return false; } }
    public override bool DrawsOwnSlash { get { return true; } }
    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration) { return null; }

    public override DangerShape[] DangerShapes(BossEnemy boss)
    {
        return new DangerShape[] { DangerShape.Circle(1f, DangerOrigin.Boss) };
    }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        var r = boss as ReaperBoss;
        if (r == null) yield break;

        ToastManager.Show("분신의 색이 곧 공격이다!", ToastManager.Kind.Warn);
        r.PlayState("CastLoop");
        yield return ReaperFx.Wait(boss, 0.6f);

        var kinds = new List<ReaperClone.Kind> { ReaperClone.Kind.Blue, ReaperClone.Kind.Green, ReaperClone.Kind.Red, ReaperClone.Kind.Brown };
        for (int i = kinds.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var tmp = kinds[i]; kinds[i] = kinds[j]; kinds[j] = tmp; }

        int n = Mathf.Min(kinds.Count, boss.PhaseIndex >= lateFromPhase ? countLate : count);
        Vector2 player = ReaperFx.PlayerPos(boss.transform.position);
        for (int i = 0; i < n && !boss.isDead; i++)
        {
            Vector2 at = r.RandomArenaPoint(1.5f);
            for (int k = 0; k < 10 && Vector2.Distance(at, player) < minDistanceFromPlayer; k++) at = r.RandomArenaPoint(1.5f);
            r.SpawnClone(kinds[i], at, r.SecondLife ? lifeSecondLife : life, cloneHp);
            yield return ReaperFx.Wait(boss, 0.15f);
        }
        yield return ReaperFx.Wait(boss, 0.4f);
        if (!boss.isDead) r.PlayState("Idle");
    }
}
