using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 영혼 흡수. 살아 있는 부하를 빨아들여 체력을 회복한다.
//
// 소환에 의미를 주는 패턴이다 — 부하를 방치하면 보스가 회복하므로,
// "잡몹부터 치울 것인가, 보스를 계속 때릴 것인가"를 고르게 된다.
// 부하가 없으면 아예 뽑히지 않는다.
public class BossPatternDrain : BossPattern
{
    [Header("흡수")]
    [Tooltip("이 거리 안의 부하만 빨아들인다")]
    [Min(1f)] [SerializeField] private float range = 9f;

    [Tooltip("한 번에 빨아들일 최대 수")]
    [Min(1)] [SerializeField] private int maxTargets = 4;

    [Tooltip("부하 하나당 회복량 (보스 최대 체력의 %)")]
    [Range(0.5f, 20f)] [SerializeField] private float healPercentPerMinion = 3f;

    [Tooltip("빨려 들어가는 시간(초). 이 동안 보스는 제자리에 묶인다 — 때릴 틈이다")]
    [Min(0.2f)] [SerializeField] private float pullTime = 1.1f;

    [Header("연출")]
    [SerializeField] private float shake = 0.2f;
    [SerializeField] private string vfxId = "EnemyHit";

    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration)
    {
        // 플레이어가 맞는 공격은 아니지만, "지금 뭔가 크게 온다"는 건 보여 준다
        return DangerZone.Circle(boss.transform.position, range, duration, boss.transform);
    }

    public override bool IsUsable(BossEnemy boss)
    {
        return base.IsUsable(boss) && FindMinions(boss).Count > 0;
    }

    private List<Enemy> FindMinions(BossEnemy boss)
    {
        var found = new List<Enemy>();
        foreach (Enemy e in Object.FindObjectsOfType<Enemy>())
        {
            if (e == null || e == boss || e.isDead) continue;
            if (e.Grade == EnemyGrade.Boss) continue;     // 다른 보스를 먹지는 않는다
            if (e.GetComponent<ShopPanel>() != null) continue;   // 상인은 부하가 아니다

            if (Vector2.Distance(e.transform.position, boss.transform.position) <= range)
                found.Add(e);
        }
        return found;
    }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        boss.CloseHitBox();

        var targets = FindMinions(boss);
        if (targets.Count > maxTargets) targets.RemoveRange(maxTargets, targets.Count - maxTargets);
        if (targets.Count == 0) yield break;

        var starts = new List<Vector3>();
        for (int i = 0; i < targets.Count; i++) starts.Add(targets[i].transform.position);

        if (shake > 0f) CameraShake.Shake(shake);

        float t = 0f;
        while (t < 1f && !boss.isDead)
        {
            t += Time.deltaTime / pullTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            for (int i = 0; i < targets.Count; i++)
            {
                Enemy e = targets[i];
                if (e == null || e.isDead) continue;

                // 끌려오는 동안 스스로 움직이지 못하게 속도를 눌러 둔다
                if (e.rb != null) e.rb.velocity = Vector2.zero;
                e.transform.position = Vector3.Lerp(starts[i], boss.transform.position, k);
            }

            if (boss.rb != null) boss.rb.velocity = Vector2.zero;
            yield return null;
        }

        if (boss.isDead) yield break;

        int healed = 0;
        int perMinion = Mathf.Max(1, Mathf.RoundToInt(boss.maxHp * healPercentPerMinion * 0.01f));

        for (int i = 0; i < targets.Count; i++)
        {
            Enemy e = targets[i];
            if (e == null || e.isDead) continue;

            if (!string.IsNullOrEmpty(vfxId)) PixelVfx.Play(vfxId, e.transform.position);
            e.TakeDamage(e.hp);          // 흡수 = 부하의 죽음
            healed += perMinion;
        }

        boss.HealBoss(healed);
        ToastManager.Show("해골왕이 부하를 삼켰다", ToastManager.Kind.Warn);
    }
}
