using System.Collections;
using UnityEngine;

// 원거리 좀비: 이동 없이 플레이어 방향으로 불릿을 발사.
public class RangedEnemy : AttackEnemyBase
{
    [Header("원거리 발사 설정")]
    [Tooltip("발사할 불릿 프리팹 (EnemyBullet)")]
    [SerializeField] private EnemyBullet bulletPrefab;
    [Tooltip("불릿 속도")]
    [SerializeField] private float bulletSpeed = 8f;
    [Tooltip("한 번에 발사하는 불릿 수")]
    [SerializeField] private int bulletCount = 1;
    [Tooltip("여러 발일 때 전체 부채꼴 각도(도)")]
    [SerializeField] private float spreadAngle = 0f;
    [Tooltip("발사 지점을 적 중심에서 앞으로 밀어낸 거리")]
    [SerializeField] private float muzzleForward = 0.6f;

    protected override IEnumerator AttackActivePhase(Vector2 dir)
    {
        rb.velocity = Vector2.zero; // 제자리 발사

        if (bulletPrefab != null)
            FireBullets(dir);

        yield break;
    }

    private void FireBullets(Vector2 dir)
    {
        Vector3 spawnPos = transform.position + (Vector3)(dir * muzzleForward);
        int count = Mathf.Max(1, bulletCount);

        // 부채꼴: -spread/2 ~ +spread/2 로 균등 분배
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float start = (count == 1) ? baseAngle : baseAngle - spreadAngle * 0.5f;
        float step = (count == 1) ? 0f : spreadAngle / (count - 1);

        for (int i = 0; i < count; i++)
        {
            float ang = (start + step * i) * Mathf.Deg2Rad;
            Vector2 shotDir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));

            EnemyBullet b = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
            b.Init(shotDir, bulletSpeed, AttackDamage, this);
        }
    }
}
