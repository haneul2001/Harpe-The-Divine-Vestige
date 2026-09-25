using System.Collections;
using UnityEngine;

// 광역 내려찍기. 제자리에서 판정을 한 번 또는 여러 번 연다.
//
// 피하는 법이 "거리를 벌린다"인 패턴이라, 보스의 기본기로 쓰기 좋다.
public class BossPatternSlam : BossPattern
{
    [Header("내려찍기")]
    [Tooltip("연속으로 몇 번 찍을지")]
    [Min(1)] [SerializeField] private int hitCount = 1;

    [Tooltip("한 번의 판정이 열려 있는 시간(초)")]
    [Min(0.05f)] [SerializeField] private float activeDuration = 0.3f;

    [Tooltip("연속 타격 사이의 간격(초). 이 동안은 판정이 닫힌다")]
    [Min(0.05f)] [SerializeField] private float hitInterval = 0.45f;

    [Tooltip("타격마다 쓸 공격 애니메이션 번호. 예: 0,1,0 이면 내려찍기 → 가로베기 → 내려찍기.\n"
           + "비우면 매번 같은 동작을 되감아 쓴다")]
    [SerializeField] private int[] hitAnimations;

    [Header("연출")]
    [SerializeField] private float shake = 0.35f;
    [Tooltip("타격마다 띄울 이펙트 id. 비우면 안 띄운다")]
    [SerializeField] private string vfxId = "SlashHit";

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        // 다음 타격의 예고는 "이번 칼이 들어가기 전에" 깔아 둔다.
        // 한 대 맞고 나서야 다음 칸이 뜨면 연타가 뚝뚝 끊겨 보인다 —
        // 두 칸이 잠깐 같이 떠 있어야 "계속 이어진다"로 읽힌다.
        DangerZone cur = null;       // 이번 타격의 칸 (첫 타는 베이스가 띄운 것이라 없다)
        DangerZone next = null;
        int curIndex = AnimationIndex, nextIndex = AnimationIndex;
        Vector2 curAim = dirToPlayer, nextAim = dirToPlayer;

        for (int i = 0; i < hitCount && !boss.isDead; i++)
        {
            if (i > 0)
            {
                boss.CloseHitBox();
                yield return Hold(boss, hitInterval);
                if (boss.isDead) break;

                // 판정만 다시 열면 보스는 한 번 휘두르고 마는데 피해는 세 번 들어간다.
                // 몇 대 맞았는지 눈으로 셀 수 있어야 하므로 동작도 같이 다시 건다.
                boss.PlayAttackAnimation(curIndex);

                // 다음 칸은 바로 여기서 깐다. 이번 칼이 닿기까지 아직 시간이 남아 있으므로
                // 그 동안 두 칸이 겹쳐 뜬다.
                if (i + 1 < hitCount)
                {
                    nextIndex = AnimationFor(i + 1);
                    nextAim = boss.AimAtPlayer();
                    next = boss.ShowComboDanger(nextAim, boss.AttackHitTimeOf(curIndex)
                        + activeDuration + hitInterval + boss.AttackHitTimeOf(nextIndex));
                }

                // 판정과 이펙트는 이 동작의 타격 이벤트에 맞춘다.
                // 고정 시간으로 맞추면 동작마다 칼이 닿는 순간이 달라 장판과 따로 논다.
                yield return boss.WaitForNextAnimHit(boss.AttackHitTimeOf(curIndex) + 1f);
                if (boss.isDead) break;

                if (cur != null) { cur.CompleteNow(); cur = null; }

                boss.RearmHitBox();
                if (shake > 0f) CameraShake.Shake(shake);
                boss.PlaySlashNow(curAim);
            }
            else
            {
                boss.RearmHitBox();
                if (shake > 0f) CameraShake.Shake(shake);
                // 첫 타격의 이펙트는 베이스가 히트박스를 열면서 이미 띄웠다.

                // 두 번째 칸은 지금이 가장 이른 시점이다 — 첫 동작은 베이스가 이미 걸어 버렸다
                if (hitCount > 1)
                {
                    nextIndex = AnimationFor(1);
                    nextAim = boss.AimAtPlayer();
                    next = boss.ShowComboDanger(nextAim,
                        activeDuration + hitInterval + boss.AttackHitTimeOf(nextIndex));
                }
            }

            cur = next; next = null;
            curIndex = nextIndex; curAim = nextAim;

            yield return Hold(boss, activeDuration);
        }

        if (cur != null) cur.Cancel();
        if (next != null) next.Cancel();
    }

    private int AnimationFor(int hitIndex)
    {
        return (hitAnimations != null && hitIndex < hitAnimations.Length)
            ? hitAnimations[hitIndex] : AnimationIndex;
    }

    // 제자리 패턴이라 속도를 매 프레임 눌러 둔다.
    // 보스는 발동 구간에 위치 잠금이 풀려 있어, 안 누르면 넉백이나 다른 적에게 밀린다.
    private IEnumerator Hold(BossEnemy boss, float duration)
    {
        float t = duration;
        while (t > 0f && !boss.isDead)
        {
            t -= Time.deltaTime;
            if (boss.rb != null) boss.rb.velocity = Vector2.zero;
            yield return null;
        }
    }
}
