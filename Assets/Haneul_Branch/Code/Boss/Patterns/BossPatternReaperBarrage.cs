using System.Collections;
using UnityEngine;

// 해골 영혼 탄막. 낫을 치켜들고 해골을 뿌린다. 세 가지 모양이 있다.
//   · 방사: 한 바퀴씩, 파마다 틈이 엇갈린다 — 틈으로 빠져나가야 한다
//   · 나선: 두 갈래(4페이즈 세 갈래)가 빙글빙글 — 원을 그리며 버틴다
//   · 조준: 빠른 해골을 연달아 — 옆으로 계속 움직여야 한다
public class BossPatternReaperBarrage : BossPattern
{
    public enum Shape { Radial, Spiral, Aimed }

    [SerializeField] private Shape shape = Shape.Radial;
    [SerializeField] private string color = "aqua";

    [Header("방사")]
    [SerializeField] private int radialCount = 16;
    [SerializeField] private int radialWaves = 2;
    [SerializeField] private int radialWavesLate = 3;
    [SerializeField] private float radialGap = 0.5f;
    [SerializeField] private float radialSpeed = 6f;
    [SerializeField] private int radialDamage = 10;

    [Header("나선")]
    [SerializeField] private float spiralTime = 3f;
    [SerializeField] private int spiralArms = 2;
    [SerializeField] private int spiralArmsLate = 3;
    [SerializeField] private float spiralRate = 8f;
    [SerializeField] private float spiralTurn = 90f;
    [SerializeField] private float spiralSpeed = 5f;
    [SerializeField] private int spiralDamage = 8;

    [Header("조준")]
    [SerializeField] private int aimedShots = 5;
    [SerializeField] private float aimedGap = 0.16f;
    [SerializeField] private float aimedSpeed = 11f;
    [SerializeField] private int aimedDamage = 10;

    [Tooltip("이 페이즈(0부터)부터 '늦은' 값(파 수·갈래 수)을 쓴다")]
    [SerializeField] private int lateFromPhase = 2;

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
        bool late = boss.PhaseIndex >= lateFromPhase;
        r.PlayState("CastLoop");

        switch (shape)
        {
            case Shape.Radial:
                yield return ReaperFx.Radial(boss, () => r.Chest, radialCount, late ? radialWavesLate : radialWaves,
                                             radialGap, radialSpeed, radialDamage, color);
                break;
            case Shape.Spiral:
                yield return ReaperFx.Spiral(boss, () => r.Chest, spiralTime, late ? spiralArmsLate : spiralArms,
                                             spiralRate, spiralTurn, spiralSpeed, spiralDamage, color);
                break;
            case Shape.Aimed:
                yield return ReaperFx.Aimed(boss, () => r.Chest, aimedShots, aimedGap, aimedSpeed, aimedDamage, color);
                break;
        }
        if (!boss.isDead) r.PlayState("Idle");
    }
}
