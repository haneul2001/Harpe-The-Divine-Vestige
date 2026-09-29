using System.Collections;
using UnityEngine;

// 3층 마법사 해골. 멀리서 지팡이를 치켜들어(atk2) 플레이어 발밑 땅을 두 번 터뜨린다.
//
// 겨누는 순간의 자리에 원형 예고가 깔리고 원이 다 차면 한 번, 곧바로 같은 자리에 다시 예고가 차올라 한 번 더 터진다.
// 첫 폭발만 보고 멈춰 서면 두 번째에 맞는다 — 원이 보이면 빠져나가야 한다.
public class MageSkeleton : AttackEnemyBase
{
    [Header("장판 폭발")]
    [SerializeField] private float blastRadius = 1.3f;
    [Tooltip("첫 폭발 뒤 두 번째 폭발까지(초). 이 동안 같은 자리에 예고가 다시 찬다")]
    [SerializeField] private float secondDelay = 0.55f;
    [SerializeField] private string dustVfxId = "MageDust";
    [SerializeField] private string flashVfxId = "MageBurst";

    private Vector2 target;
    private DangerZone zone;

    protected override bool OpensHitBox { get { return false; } }
    protected override bool ShouldSpawnSlash { get { return false; } }

    protected override void OnAttackWarning(Vector2 dir, float warningDuration)
    {
        target = PlayerPos();
        float dx = target.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.01f && (dx > 0f) != IsFacingRight) SetFacing(dx > 0f);

        // 경고 시간 + 지팡이를 치켜드는 동안 차오른다
        zone = DangerZone.Circle(target, blastRadius, warningDuration + hitEventTimeout);
        if (zone != null) zone.HoldUntilHit().FillIn(Mathf.Max(0.15f, warningDuration));
    }

    protected override IEnumerator AttackActivePhase(Vector2 _)
    {
        if (rb != null) rb.velocity = Vector2.zero;
        if (zone != null) { zone.CompleteNow(); zone = null; }
        Blast();

        // 같은 자리에 한 번 더
        zone = DangerZone.Circle(target, blastRadius, secondDelay);
        float t = secondDelay;
        while (t > 0f && !isDead)
        {
            t -= Time.deltaTime;
            if (rb != null) rb.velocity = Vector2.zero;
            yield return null;
        }
        if (zone != null) { zone.CompleteNow(); zone = null; }
        if (!isDead) Blast();
    }

    private void Blast()
    {
        PixelVfx dust = PixelVfx.Play(dustVfxId, target);
        if (dust != null) dust.transform.localScale *= blastRadius / 1.3f;
        PixelVfx.Play(flashVfxId, target + Vector2.up * 0.3f);
        CameraShake.Shake(0.1f);

        Transform p = PlayerTransform();
        if (p == null || Vector2.Distance(p.position, target) > ShrinkRadius(blastRadius)) return;
        PlayerStatus status = p.GetComponent<PlayerStatus>();
        if (status == null) status = p.GetComponentInParent<PlayerStatus>();
        if (status != null) status.TakeDamage(AttackDamage, this);
    }

    protected override void OnAttackFinally()
    {
        if (zone != null) { zone.Cancel(); zone = null; }
    }

    private Transform PlayerTransform()
    {
        if (player != null) return player;
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        return go != null ? go.transform : null;
    }

    private Vector2 PlayerPos()
    {
        Transform p = PlayerTransform();
        return p != null ? (Vector2)p.position : (Vector2)transform.position;
    }
}
