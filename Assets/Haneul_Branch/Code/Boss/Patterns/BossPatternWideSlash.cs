using System.Collections;
using UnityEngine;

// 긴 베기. 대검을 멀리까지 휘둘러 한 줄을 통째로 쓸어 낸다.
//
// 한 번에 세 방향을 덮지 않는다. 정면을 베고, 왼쪽을 베고, 오른쪽을 벤다 —
// 매번 벨 자리를 먼저 붉게 보여 주므로, 어디로 빠질지 고를 시간이 세 번 생긴다.
// 세 칸을 동시에 깔면 "피할 곳이 없다"가 되고, 순서대로 깔면 "읽고 움직여라"가 된다.
//
// 근접 판정 상자는 쓰지 않는다. 벨 자리가 보스 앞 한 칸이 아니라 길쭉한 줄이라,
// 상자를 돌려 쓰는 것보다 자기 칸을 직접 그리는 편이 예고와 어긋나지 않는다.
public class BossPatternWideSlash : BossPattern
{
    [Header("베기")]
    [Tooltip("한 번의 베기가 덮는 자리 (가로x세로, 월드 단위)")]
    [SerializeField] private Vector2 slashSize = new Vector2(9f, 3.2f);

    [Tooltip("벨 방향. 겨눈 쪽을 0으로 보고 각도를 더한다.\n"
           + "정면 → 왼쪽 → 오른쪽 순서라면 0, 40, -40")]
    [SerializeField] private float[] angles = { 0f, 40f, -40f };

    [Tooltip("한 번 벨 때마다 보여 주는 예고 시간(초)")]
    [Min(0.1f)] [SerializeField] private float warnTime = 0.5f;

    [Tooltip("베고 나서 다음 예고까지 쉬는 시간(초). 0이면 쉼 없이 이어 벤다")]
    [Min(0f)] [SerializeField] private float betweenSlashes = 0.18f;

    [Tooltip("첫 베기 전에 칼을 드는 시간(초)")]
    [Min(0f)] [SerializeField] private float castTime = 0.15f;

    [Header("연출")]
    [Tooltip("벨 때 띄울 이펙트 id. 예고한 칸을 그대로 채운다")]
    [SerializeField] private string slashVfxId = "BossSlash";
    [SerializeField] private float shake = 0.25f;

    // 자기 칸을 직접 그리고 직접 피해를 굴린다
    public override bool UsesHitBox { get { return false; } }
    public override bool DrawsOwnSlash { get { return true; } }

    // 툴에는 한 번의 베기 자리를 보여 준다 (세 번 다 같은 크기다)
    public override DangerShape[] DangerShapes(BossEnemy boss)
    {
        return new DangerShape[] { DangerShape.SectorFromBox(slashSize, DangerOrigin.Boss) };
    }

    // 그림은 예고보다 조금 크게 그린다
    public override DangerShape SlashShape(BossEnemy boss)
    {
        return DangerShape.Box(slashSize, slashSize.x * 0.5f, DangerOrigin.Boss);
    }

    // 예고는 벨 때마다 하나씩 낸다. 공용 예고를 띄우면 첫 칸이 두 번 뜬다.
    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration) { return null; }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        boss.CloseHitBox();

        if (castTime > 0f) yield return Hold(boss, castTime);
        if (boss.isDead) yield break;

        Vector2 aim = dirToPlayer.sqrMagnitude > 0.0001f ? dirToPlayer.normalized : boss.AimAtPlayer();

        for (int i = 0; i < angles.Length && !boss.isDead; i++)
        {
            // 매번 다시 겨눈다 — 첫 방향만 쓰면 옆으로 빠진 플레이어를 영영 못 따라간다
            if (i > 0) aim = boss.AimAtPlayer();

            float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            float angle = baseAngle + angles[i];

            var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            DangerShape fan = DangerShape.SectorFromBox(slashSize, DangerOrigin.Boss);

            // ① 벨 자리를 보여 준다 — 칼이 호를 그리니 부채꼴이다
            DangerZone zone = DangerZone.Sector(boss.transform.position, fan.Radius, fan.halfAngle,
                                                angle, warnTime, boss.transform);
            yield return Hold(boss, warnTime);

            if (boss.isDead)
            {
                if (zone != null) zone.Cancel();
                yield break;
            }

            // ② 벤다 — 표시가 붙어 다녔으니 자리도 다시 잡는다.
            //    그림만 예고보다 조금 크게, 판정은 예고 크기 그대로(그 안에서 또 10% 줄어든다).
            float draw = boss.SlashOverdraw;
            Vector2 center = (Vector2)boss.transform.position + dir * (slashSize.x * 0.5f);
            if (zone != null) zone.CompleteNow();

            if (!string.IsNullOrEmpty(slashVfxId))
                PixelVfx.PlayStretched(slashVfxId,
                    (Vector2)boss.transform.position + dir * (slashSize.x * 0.5f * draw),
                    angle, slashSize * draw);

            if (shake > 0f) CameraShake.Shake(shake);
            boss.DamagePlayerInBox(center, slashSize, angle);

            if (betweenSlashes > 0f) yield return Hold(boss, betweenSlashes);
        }
    }

    // 제자리 패턴이라 속도를 매 프레임 눌러 둔다.
    // 발동 구간엔 위치 잠금이 풀려 있어, 안 누르면 넉백이나 다른 적에게 밀린다.
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
