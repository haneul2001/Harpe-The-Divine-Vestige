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

    [Tooltip("방에 스폰 지점이 없을 때만 쓴다. 보스 중심에서 이만큼 떨어진 곳에 원형으로 배치")]
    [Min(0.5f)] [SerializeField] private float spawnRadius = 2.5f;

    [Tooltip("플레이어 바로 옆에는 안 띄운다. 이 거리 안의 스폰 지점은 건너뛴다")]
    [Min(0f)] [SerializeField] private float keepAwayFromPlayer = 2.5f;

    [Tooltip("올라올 자리를 보여 주는 표시의 반지름")]
    [Min(0.2f)] [SerializeField] private float spawnMarkRadius = 1.1f;

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

    // 부르기만 하는 패턴이다 — 가까이 있다고 맞을 이유가 없다
    public override DangerShape[] DangerShapes(BossEnemy boss) { return new DangerShape[0]; }
    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration) { return null; }
    public override bool DrawsOwnSlash { get { return true; } }

    // 근접 판정을 안 쓴다 — 부르기만 한다
    public override bool UsesHitBox { get { return false; } }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        HookBossDeath(boss);

        // 근접 판정은 쓰지 않는다. 베이스가 열어 둔 것을 바로 닫는다
        boss.CloseHitBox();

        int room = maxAlive - AliveCount();
        int spawn = Mathf.Min(countPerCast, Mathf.Max(0, room));

        Room bossRoom = boss.GetComponentInParent<Room>();
        List<Vector3> spots = PickSpots(boss, bossRoom, spawn);

        // 어디서 올라오는지 먼저 보여 준다 — 피해는 없지만 자리를 비켜설 시간은 줘야 한다
        for (int i = 0; i < spots.Count; i++)
            DangerZone.Circle(spots[i], spawnMarkRadius, Mathf.Max(0.1f, castTime));

        // 시전 — 제자리에서 버틴다
        float t = castTime;
        while (t > 0f && !boss.isDead)
        {
            t -= Time.deltaTime;
            boss.CloseHitBox();
            if (boss.rb != null) boss.rb.velocity = Vector2.zero;
            yield return null;
        }
        if (boss.isDead) yield break;

        int made = SpawnInto(boss, bossRoom, spots, spawn);
        if (shake > 0f && made > 0) CameraShake.Shake(shake);
    }

    // 패턴 밖에서 한 번에 불러낼 때 쓴다 — 페이즈 전환 연출 같은 경우다.
    //
    // 동시 생존 제한은 보지 않는다. 연출로 나오는 소환이라 "지금은 자리가 없다"로 빠지면
    // 단계가 올랐는데 아무 일도 안 일어나는 전환이 생긴다.
    // 대신 불러낸 부하는 여기에도 등록해 두어 보스가 죽을 때 같이 정리된다.
    public int SummonBurst(BossEnemy boss, int count)
    {
        if (boss == null || count <= 0) return 0;
        if (minionPrefabs == null || minionPrefabs.Length == 0) return 0;

        HookBossDeath(boss);

        Room bossRoom = boss.GetComponentInParent<Room>();
        List<Vector3> spots = PickSpots(boss, bossRoom, count);

        int made = SpawnInto(boss, bossRoom, spots, count);
        if (shake > 0f && made > 0) CameraShake.Shake(shake);
        return made;
    }

    // 자리를 받아 실제로 세운다. 자리가 부를 수보다 적으면 앞에서부터 돌려 쓴다.
    private int SpawnInto(BossEnemy boss, Room bossRoom, List<Vector3> spots, int count)
    {
        if (spots == null || spots.Count == 0) return 0;

        int made = 0;
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = minionPrefabs[Random.Range(0, minionPrefabs.Length)];
            if (prefab == null) continue;

            if (prefab.GetComponent<Rigidbody2D>() == null)
            {
                Debug.LogError("[BossPatternSummon] " + prefab.name
                    + " 에 Rigidbody2D가 없다. 죽을 때 터지므로 Variant를 쓸 것", this);
                continue;
            }

            Vector3 pos = spots[i % spots.Count];

            GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity, boss.transform.parent);

            // 체력·공격력·크기는 이 방이 잡몹을 스폰할 때 쓰는 값을 그대로 가져온다.
            // 프리팹 기본값으로 두면 층이 올라가도 부하만 1층 체력에 머문다.
            EnemyInfo info = null;
            float scale = 0f;
            if (bossRoom != null && !bossRoom.TryGetSpawnSetup(prefab, out info, out scale))
                bossRoom.TryGetAnyMinionSetup(out info, out scale);

            go.transform.localScale = Vector3.one * (scale > 0.01f ? scale : minionScale);

            Enemy e = go.GetComponent<Enemy>();
            if (e != null)
            {
                if (info != null) e.Initialize(info);
                e.player = boss.player;
                minions.Add(e);
            }

            if (!string.IsNullOrEmpty(vfxId)) PixelVfx.Play(vfxId, pos);
            made++;
        }
        return made;
    }

    // 부하가 설 자리. 방이 정해 둔 스폰 지점을 쓴다 —
    // 보스 둘레에 원형으로 뿌리면 방 모양에 따라 벽 너머나 방 밖에 떨어진다.
    private List<Vector3> PickSpots(BossEnemy boss, Room room, int howMany)
    {
        var spots = new List<Vector3>();
        Transform player = boss.player;

        if (room != null && room.SpawnPointList != null)
        {
            var usable = new List<Transform>();
            foreach (Transform t in room.SpawnPointList)
            {
                if (t == null) continue;
                if (player != null && keepAwayFromPlayer > 0f
                    && Vector2.Distance(t.position, player.position) < keepAwayFromPlayer) continue;
                usable.Add(t);
            }

            // 플레이어를 피하다 자리가 다 막히면 거리는 포기한다 — 안 나오는 것보다 낫다
            if (usable.Count == 0)
                foreach (Transform t in room.SpawnPointList)
                    if (t != null) usable.Add(t);

            // 섞어서 앞에서부터 쓴다. 매번 무작위로 뽑으면 같은 자리에 둘이 겹친다
            for (int i = usable.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                Transform tmp = usable[i]; usable[i] = usable[j]; usable[j] = tmp;
            }

            for (int i = 0; i < usable.Count && spots.Count < howMany; i++)
                spots.Add(usable[i].position);
        }

        // 방에 스폰 지점이 없는 경우에만 옛 방식으로 되돌아간다
        if (spots.Count == 0)
            for (int i = 0; i < Mathf.Max(1, howMany); i++)
            {
                float ang = (360f / Mathf.Max(1, howMany)) * i + Random.Range(-20f, 20f);
                spots.Add(boss.transform.position + Quaternion.Euler(0f, 0f, ang) * Vector3.right * spawnRadius);
            }

        return spots;
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
