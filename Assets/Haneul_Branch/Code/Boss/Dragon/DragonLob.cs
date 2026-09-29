using UnityEngine;

// 용이 던지는 화염구. 입에서 떠서 포물선을 그리며 바닥의 한 점에 떨어진다.
//
// 떨어질 자리는 던지는 순간 원으로 예고한다 — 날아가는 공을 눈으로 좇지 않아도
// "저 원이 다 차면 터진다"만 보면 된다. 원이 다 차는 순간이 곧 착탄이다.
// 착탄 순간은 패링할 수 있고(공격자 = 용), 그 뒤 남는 불장판은 패링할 수 없다.
public class DragonLob : MonoBehaviour
{
    private BossEnemy owner;
    private Vector3 from;
    private Vector2 to;
    private float flightTime;
    private float arc;
    private float radius;
    private int damage;
    private bool dark;
    private float fireDuration;
    private int fireTick;
    private float t;
    private PixelVfx ball;
    private DangerZone zone;

    public static DragonLob Launch(BossEnemy owner, Vector3 from, Vector2 to, float flightTime, float arcHeight,
                                   float radius, int damage, float ballScale, bool dark,
                                   float fireDuration = 0f, int fireTick = 0)
    {
        var go = new GameObject("DragonLob");
        go.transform.position = from;
        var lob = go.AddComponent<DragonLob>();
        lob.owner = owner;
        lob.from = from;
        lob.to = to;
        lob.flightTime = Mathf.Max(0.1f, flightTime);
        lob.arc = arcHeight;
        lob.radius = radius;
        lob.damage = damage;
        lob.dark = dark;
        lob.fireDuration = fireDuration;
        lob.fireTick = fireTick;

        lob.zone = DangerZone.Circle(to, radius, lob.flightTime);
        if (dark && lob.zone != null) lob.zone.Dark();

        lob.ball = dark
            ? DragonFx.Play("DragonDarkOrb", from, ballScale)
            : DragonFx.Play("DragonFireball", from, ballScale);
        if (lob.ball != null) lob.ball.transform.SetParent(go.transform, true);
        return lob;
    }

    private void Update()
    {
        if (owner == null || owner.isDead)
        {
            if (zone != null) zone.Cancel();
            Destroy(gameObject);
            return;
        }

        t += Time.deltaTime / flightTime;
        float k = Mathf.Clamp01(t);

        // 땅 위 직선 + 위로 볼록한 호. 높이는 처음(입 높이)에서 0(바닥)으로 줄어든다
        Vector3 flat = Vector3.Lerp(from, to, k);
        float h = arc * 4f * k * (1f - k);
        Vector3 pos = flat + Vector3.up * h;

        // 진행 방향으로 공을 돌린다 (원본 그림은 오른쪽으로 날아가는 모양)
        Vector3 delta = pos - transform.position;
        if (delta.sqrMagnitude > 0.00001f && ball != null)
            ball.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        transform.position = pos;

        if (k < 1f) return;

        Impact();
    }

    private void Impact()
    {
        if (zone != null) zone.CompleteNow();

        if (dark) DragonFx.Play("DragonDarkBurst", to, radius / 1.3f);
        else DragonFx.Play("DragonFireBurst", to, radius / 1.2f);
        CameraShake.Shake(0.18f);

        owner.DamagePlayerInRadius(to, radius, damage, true);

        if (fireDuration > 0f)
            DragonFireGround.Spawn(owner, to, radius, fireDuration, fireTick, 0.5f, dark);

        Destroy(gameObject);
    }
}
