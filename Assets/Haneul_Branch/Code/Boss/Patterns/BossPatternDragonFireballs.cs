using System.Collections;
using UnityEngine;

// 용의 화염구 연사. 입에서 불덩이 여러 발을 부채꼴로 흩뿌린다.
//
// 떨어질 자리마다 원이 먼저 뜨고, 떨어진 자리에는 잠깐 불장판이 남는다 —
// 피할 자리를 좁혀 다음 공격(할퀴기·브레스)을 피하기 어렵게 만드는 패턴이다.
// 첫 발은 플레이어 발밑, 나머지는 그 주위로 흩어진다.
// 3페이즈에는 발 수가 늘고 불이 보라색 어둠 불꽃으로 바뀐다.
public class BossPatternDragonFireballs : BossPattern
{
    [Header("화염구")]
    [Min(1)] [SerializeField] private int count = 3;
    [Min(1)] [SerializeField] private int darkPhaseCount = 5;
    [Min(0.3f)] [SerializeField] private float radius = 1.2f;
    [Min(0f)] [SerializeField] private float spread = 3.2f;
    [Min(0.2f)] [SerializeField] private float flightTime = 0.9f;
    [Min(0f)] [SerializeField] private float stagger = 0.14f;
    [Min(0f)] [SerializeField] private float arcHeight = 2.5f;
    [Min(1)] [SerializeField] private int damage = 12;

    [Header("불장판")]
    [Min(0f)] [SerializeField] private float fireDuration = 3f;
    [Min(1)] [SerializeField] private int fireTickDamage = 5;

    public override bool UsesHitBox { get { return false; } }
    public override bool DrawsOwnSlash { get { return true; } }

    public override DangerShape[] DangerShapes(BossEnemy boss)
    {
        return new DangerShape[] { DangerShape.Circle(radius, DangerOrigin.Player).Scattered(count, spread) };
    }

    // 예고는 화염구가 저마다 떨어질 자리에 띄운다
    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration) { return null; }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        var dragon = boss as DragonBoss;
        bool dark = dragon != null && dragon.IsDarkPhase;
        int n = dark ? darkPhaseCount : count;

        Transform p = DragonFx.Player();
        Vector2 center = p != null ? (Vector2)p.position : (Vector2)boss.transform.position + dirToPlayer * 4f;

        for (int i = 0; i < n && !boss.isDead; i++)
        {
            Vector2 at = i == 0 ? center : center + Random.insideUnitCircle * spread;
            if (dragon != null) at = dragon.ClampToArena(at, radius);

            Vector3 mouth = dragon != null ? dragon.MouthPosition : boss.transform.position;
            DragonLob.Launch(boss, mouth, at, flightTime + i * 0.05f, arcHeight, radius, damage, 1f, dark,
                             fireDuration, fireTickDamage);
            CameraShake.Shake(0.08f);

            float w = stagger;
            while (w > 0f && !boss.isDead) { w -= Time.deltaTime; yield return null; }
        }

        // 마지막 발이 떨어질 때까지 입을 벌린 채 버틴다
        float wait = flightTime * 0.6f;
        while (wait > 0f && !boss.isDead)
        {
            wait -= Time.deltaTime;
            if (boss.rb != null) boss.rb.velocity = Vector2.zero;
            yield return null;
        }
    }
}
