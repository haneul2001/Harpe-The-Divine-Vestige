using System.Collections;
using UnityEngine;

// 거대 사신과 그 분신들이 같이 쓰는 공격 조각.
//
// 분신은 색마다 공격이 하나로 정해져 있고(파랑=방사, 초록=나선, 빨강=순간이동 베기, 주황=조준 연사),
// 본체는 그 전부를 쓴다. 같은 공격을 두 곳에서 따로 짜면 한쪽만 고쳐지므로 여기 모은다.
// 피해의 주인은 늘 본체다 — 분신 탄을 받아쳐도 반격은 본체에 들어간다.
public static class ReaperFx
{
    private static EnemyBullet bulletPrefab;

    private static EnemyBullet Bullet
    {
        get
        {
            if (bulletPrefab == null)
            {
                var go = Resources.Load<GameObject>("Dungeon/ReaperSkullBullet");
                if (go != null) bulletPrefab = go.GetComponent<EnemyBullet>();
            }
            return bulletPrefab;
        }
    }

    public static Transform Player()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        return p != null ? p.transform : null;
    }

    public static Vector2 PlayerPos(Vector2 fallback)
    {
        Transform p = Player();
        return p != null ? (Vector2)p.position + Vector2.up * 0.4f : fallback;
    }

    // 해골 한 발. color는 이펙트 이름 끝 (aqua, blue, green, red, brown, purple)
    public static void Skull(BossEnemy owner, Vector2 from, Vector2 dir, float speed, int damage, string color)
    {
        EnemyBullet b = Bullet;
        if (b == null || owner == null || owner.isDead) return;
        EnemyBullet s = Object.Instantiate(b, from, Quaternion.identity);
        s.SetVfx("ReaperSkull_" + color);
        s.Init(dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right, speed, damage, owner);
    }

    // 방사 탄막 — 한 바퀴씩, 파마다 반 칸씩 엇갈려 틈 자리가 바뀐다
    public static IEnumerator Radial(BossEnemy owner, System.Func<Vector2> origin, int count, int waves, float gap,
                                     float speed, int damage, string color)
    {
        float offset = Random.value * 360f;
        for (int w = 0; w < waves && owner != null && !owner.isDead; w++)
        {
            Vector2 o = origin();
            float step = 360f / Mathf.Max(1, count);
            float a0 = offset + (w % 2) * step * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float a = (a0 + step * i) * Mathf.Deg2Rad;
                Skull(owner, o, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), speed, damage, color);
            }
            Burst(o, color, 0.8f);
            yield return Wait(owner, gap);
        }
    }

    // 나선 탄막 — arms 갈래가 빙글빙글 돌며 뿌려진다
    public static IEnumerator Spiral(BossEnemy owner, System.Func<Vector2> origin, float duration, int arms, float rate,
                                     float turnSpeed, float speed, int damage, string color)
    {
        float angle = Random.value * 360f;
        float end = Time.time + duration;
        float interval = 1f / Mathf.Max(1f, rate);
        while (Time.time < end && owner != null && !owner.isDead)
        {
            Vector2 o = origin();
            for (int i = 0; i < arms; i++)
            {
                float a = (angle + 360f / arms * i) * Mathf.Deg2Rad;
                Skull(owner, o, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), speed, damage, color);
            }
            angle += turnSpeed * interval;
            yield return Wait(owner, interval);
        }
    }

    // 조준 연사 — 발마다 다시 겨눈다
    public static IEnumerator Aimed(BossEnemy owner, System.Func<Vector2> origin, int shots, float gap, float speed,
                                    int damage, string color)
    {
        for (int i = 0; i < shots && owner != null && !owner.isDead; i++)
        {
            Vector2 o = origin();
            Skull(owner, o, PlayerPos(o) - o, speed, damage, color);
            yield return Wait(owner, gap);
        }
    }

    // 초승달 베기의 피해 + 그림. 부채꼴 판정, 그림은 사신 전용 초승달 궤적을 예고 칸만큼 늘린다
    public static void CrescentHit(BossEnemy owner, Vector2 apex, float radius, float halfAngle, float angle, int damage)
    {
        if (owner == null || owner.isDead) return;
        // 사신 전용 궤적 — 그림 한가운데가 부채꼴 꼭짓점이고, 호가 반지름 끝을 따라 그려진다
        PixelVfx.PlayStretched("ReaperCrescent", apex, angle, Vector2.one * radius * 2.08f);
        CameraShake.Shake(0.35f);
        owner.DamagePlayerInSector(apex, radius, halfAngle, angle, damage);
    }

    public static void Burst(Vector2 at, string color, float scale)
    {
        PixelVfx v = PixelVfx.Play("ReaperBurst_" + color, at);
        if (v != null) v.transform.localScale *= scale;
    }

    public static IEnumerator Wait(Enemy owner, float seconds)
    {
        float t = seconds;
        while (t > 0f && owner != null && !owner.isDead) { t -= Time.deltaTime; yield return null; }
    }
}
