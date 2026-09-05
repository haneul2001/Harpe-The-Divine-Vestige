using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 소환. 보스 주위에 잡몹을 불러낸다.
//
// 부하는 방의 클리어 판정에 넣지 않는다. 넣으면 보스를 잡고도 부하가 남아 문이 안 열리고,
// 부하를 계속 부르는 보스라면 방이 영영 안 끝난다. 대신 보스가 죽을 때 같이 정리한다.
public class BossPatternSummon : BossPattern
{
    [Header("소환")]
    [Tooltip("불러낼 적 프리팹. Rigidbody2D가 있는 것을 쓸 것 — 없는 원본은 죽을 때 터진다")]
    [SerializeField] private GameObject[] minionPrefabs;

    [Min(1)] [SerializeField] private int countPerCast = 2;

    [Tooltip("보스 중심에서 이만큼 떨어진 곳에 원형으로 배치한다")]
    [Min(0.5f)] [SerializeField] private float spawnRadius = 2.5f;

    [Tooltip("동시에 살아 있을 수 있는 부하 수. 넘으면 이 패턴은 안 뽑힌다")]
    [Min(1)] [SerializeField] private int maxAlive = 6;

    [Tooltip("소환 크기 배율. 방의 잡몹과 같은 크기로 맞춘다")]
    [Min(0.1f)] [SerializeField] private float minionScale = 3f;

    [Header("연출")]
    [Tooltip("불러내기까지 버티는 시간(초). 이 틈에 보스를 때리는 것이 정답이 된다")]
    [Min(0f)] [SerializeField] private float castTime = 0.7f;
    [SerializeField] private float shake = 0.3f;
    [Tooltip("소환 지점에 띄울 이펙트 id. 비우면 안 띄운다")]
    [SerializeField] private string vfxId = "EnemyHit";

    private readonly List<Enemy> minions = new List<Enemy>();
    private bool hookedDeath;

    public override bool IsUsable(BossEnemy boss)
    {
        if (!base.IsUsable(boss)) return false;
        if (minionPrefabs == null || minionPrefabs.Length == 0) return false;

        return AliveCount() < maxAlive;
    }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        HookBossDeath(boss);

        // 시전 — 제자리에서 버틴다
        float t = castTime;
        while (t > 0f && !boss.isDead)
        {
            t -= Time.deltaTime;
            if (boss.rb != null) boss.rb.velocity = Vector2.zero;
            yield return null;
        }
        if (boss.isDead) yield break;

        int room = maxAlive - AliveCount();
        int spawn = Mathf.Min(countPerCast, Mathf.Max(0, room));

        for (int i = 0; i < spawn; i++)
        {
            GameObject prefab = minionPrefabs[Random.Range(0, minionPrefabs.Length)];
            if (prefab == null) continue;

            if (prefab.GetComponent<Rigidbody2D>() == null)
            {
                Debug.LogError("[BossPatternSummon] " + prefab.name
                    + " 에 Rigidbody2D가 없다. 죽을 때 터지므로 Variant를 쓸 것", this);
                continue;
            }

            // 원형으로 흩뿌린다. 한 점에 겹쳐 놓으면 분리 스티어링이 서로를 밀어내며 튕긴다.
            float ang = (360f / spawn) * i + Random.Range(-20f, 20f);
            Vector3 offset = Quaternion.Euler(0f, 0f, ang) * Vector3.right * spawnRadius;
            Vector3 pos = boss.transform.position + offset;

            GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity, boss.transform.parent);
            go.transform.localScale = Vector3.one * minionScale;

            Enemy e = go.GetComponent<Enemy>();
            if (e != null)
            {
                e.player = boss.player;
                minions.Add(e);
            }

            if (!string.IsNullOrEmpty(vfxId)) PixelVfx.Play(vfxId, pos);
        }

        if (shake > 0f && spawn > 0) CameraShake.Shake(shake);
    }

    private int AliveCount()
    {
        for (int i = minions.Count - 1; i >= 0; i--)
            if (minions[i] == null || minions[i].isDead) minions.RemoveAt(i);

        return minions.Count;
    }

    // 보스가 죽으면 부하도 함께 정리한다.
    // 방 클리어에 안 잡히는 적들이라, 안 치우면 빈 방에서 계속 얻어맞는다.
    private void HookBossDeath(BossEnemy boss)
    {
        if (hookedDeath) return;
        hookedDeath = true;

        boss.Died += _ => ClearMinions();
    }

    private void ClearMinions()
    {
        for (int i = 0; i < minions.Count; i++)
            if (minions[i] != null && !minions[i].isDead) minions[i].TakeDamage(999999);

        minions.Clear();
    }
}
