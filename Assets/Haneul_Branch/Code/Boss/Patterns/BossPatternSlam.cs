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

    [Header("연출")]
    [SerializeField] private float shake = 0.35f;
    [Tooltip("타격마다 띄울 이펙트 id. 비우면 안 띄운다")]
    [SerializeField] private string vfxId = "SlashHit";

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        for (int i = 0; i < hitCount && !boss.isDead; i++)
        {
            // 두 번째 타격부터는 잠깐 닫았다 다시 연다.
            // 계속 열어 두면 한 번 맞은 플레이어가 나머지 타격을 공짜로 흘려보낸다.
            if (i > 0)
            {
                boss.CloseHitBox();
                yield return Hold(boss, hitInterval);
                if (boss.isDead) yield break;
            }

            boss.RearmHitBox();

            if (shake > 0f) CameraShake.Shake(shake);

            // 첫 타격의 이펙트는 베이스가 히트박스를 열면서 이미 띄웠다.
            // 여기서 또 띄우면 같은 자리에 두 장이 겹친다.
            if (i > 0 && !string.IsNullOrEmpty(vfxId) && boss.HitBoxObject != null)
                PixelVfx.Play(vfxId, boss.HitBoxObject.transform.position);

            yield return Hold(boss, activeDuration);
        }
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
