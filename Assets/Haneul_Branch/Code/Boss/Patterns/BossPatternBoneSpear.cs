using System.Collections;
using UnityEngine;

// 뼈창 부채꼴. 멀어진 플레이어에게 투사체를 뿌린다.
//
// 이 패턴이 없으면 "거리를 벌리고 기다린다"가 모든 상황의 정답이 된다.
// 정답은 옆으로 파고드는 것 — 부채꼴 사이로 들어오면 오히려 안전하다.
public class BossPatternBoneSpear : BossPattern
{
    [Header("발사")]
    [SerializeField] private EnemyBullet bulletPrefab;
    [Min(1)] [SerializeField] private int volleyCount = 1;      // 몇 번에 나눠 쏠지
    [Min(0.05f)] [SerializeField] private float volleyInterval = 0.35f;

    [Min(1)] [SerializeField] private int bulletsPerVolley = 3;
    [Tooltip("부채꼴 전체 각도")]
    [Min(0f)] [SerializeField] private float spreadAngle = 40f;
    [Min(1f)] [SerializeField] private float bulletSpeed = 9f;
    [Tooltip("보스 중심에서 이만큼 앞에서 생긴다")]
    [Min(0f)] [SerializeField] private float muzzleForward = 0.9f;

    [Tooltip("발사 전 버티는 시간(초)")]
    [Min(0f)] [SerializeField] private float castTime = 0.25f;

    [Header("예고")]
    [Tooltip("위험지역으로 그릴 길이/폭 (월드 단위)")]
    [SerializeField] private Vector2 dangerSize = new Vector2(9f, 3.2f);

    [Header("연출")]
    [SerializeField] private float shake = 0.15f;
    [Tooltip("날아가는 뼈창에 붙일 이펙트 id. 비우면 총알 프리팹의 기본값을 쓴다")]
    [SerializeField] private string bulletVfxId = "BossSpear";

    public override bool IsUsable(BossEnemy boss)
    {
        return bulletPrefab != null && base.IsUsable(boss);
    }

    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration)
    {
        float angle = Mathf.Atan2(dirToPlayer.y, dirToPlayer.x) * Mathf.Rad2Deg;
        Vector2 center = (Vector2)boss.transform.position + dirToPlayer.normalized * (dangerSize.x * 0.5f);
        return DangerZone.Box(center, dangerSize, angle, duration);
    }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        // 투사체가 나가는 패턴이라 근접 판정은 닫아 둔다
        boss.CloseHitBox();

        for (int v = 0; v < volleyCount && !boss.isDead; v++)
        {
            if (v > 0) yield return Hold(boss, volleyInterval);
            if (boss.isDead) yield break;

            if (castTime > 0f && v == 0) yield return Hold(boss, castTime);
            if (boss.isDead) yield break;

            // 발사마다 다시 겨눈다 — 첫 방향만 쓰면 두 번째 발사가 허공을 지른다
            Vector2 dir = v == 0 ? dirToPlayer : boss.AimAtPlayer();
            Fire(boss, dir);

            if (shake > 0f) CameraShake.Shake(shake);
        }
    }

    private void Fire(BossEnemy boss, Vector2 dir)
    {
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float step = bulletsPerVolley > 1 ? spreadAngle / (bulletsPerVolley - 1) : 0f;
        float start = baseAngle - spreadAngle * 0.5f;

        for (int i = 0; i < bulletsPerVolley; i++)
        {
            float a = bulletsPerVolley > 1 ? start + step * i : baseAngle;
            Vector2 d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));

            Vector3 at = boss.transform.position + (Vector3)(d * muzzleForward);
            EnemyBullet bullet = Object.Instantiate(bulletPrefab, at, Quaternion.identity);
            bullet.SetVfx(bulletVfxId);          // Init이 이펙트를 띄우므로 그 전에 정해 준다
            bullet.Init(d, bulletSpeed, boss.AttackDamage, boss);
        }
    }

    private IEnumerator Hold(BossEnemy boss, float seconds)
    {
        float t = seconds;
        while (t > 0f && !boss.isDead)
        {
            t -= Time.deltaTime;
            if (boss.rb != null) boss.rb.velocity = Vector2.zero;
            yield return null;
        }
    }
}
