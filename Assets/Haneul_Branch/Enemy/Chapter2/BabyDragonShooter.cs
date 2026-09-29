using System.Collections;
using UnityEngine;

// 노란·빨간 새끼 용 — 입에서 불꽃 구체를 플레이어 쪽으로 쏜다.
//
// 노란 용은 한 발, 빨간 용은 짧은 간격으로 세 발. 발마다 다시 겨누므로
// 연사는 옆으로 움직이는 플레이어를 따라온다 — 멈춰 있으면 세 발 다 맞는다.
public class BabyDragonShooter : BabyDragonBase
{
    [Header("사격")]
    [SerializeField] private EnemyBullet bulletPrefab;
    [SerializeField] private string boltVfxId = "BabyBoltGold";
    [SerializeField] private int shots = 1;
    [SerializeField] private float shotInterval = 0.25f;
    [SerializeField] private float bulletSpeed = 7f;
    [Tooltip("입 자리 — 몸 가운데에서 앞으로, 위로 (월드 단위)")]
    [SerializeField] private Vector2 muzzle = new Vector2(0.55f, 0.75f);

    protected override void OnAttackWarning(Vector2 dir, float warningDuration)
    {
        FaceDir(DirToPlayer());
    }

    protected override IEnumerator AttackActivePhase(Vector2 _)
    {
        if (rb != null) rb.velocity = Vector2.zero;

        for (int i = 0; i < Mathf.Max(1, shots) && !isDead; i++)
        {
            if (i > 0)
            {
                float w = shotInterval;
                while (w > 0f && !isDead) { w -= Time.deltaTime; yield return null; }
                if (isDead) yield break;
            }
            Fire();
        }
    }

    private void Fire()
    {
        if (bulletPrefab == null) return;

        Vector2 d0 = DirToPlayer();
        FaceDir(d0);
        Vector3 from = transform.position + new Vector3(muzzle.x * (IsFacingRight ? 1f : -1f), muzzle.y, 0f);
        Vector2 dir = PlayerPos + Vector2.up * 0.4f - (Vector2)from;   // 몸통 높이를 노린다
        if (dir.sqrMagnitude < 0.0001f) dir = d0;

        EnemyBullet b = Instantiate(bulletPrefab, from, Quaternion.identity);
        b.SetVfx(boltVfxId);
        b.Init(dir.normalized, bulletSpeed, AttackDamage, this);
    }
}
