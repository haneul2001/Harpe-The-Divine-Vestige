using UnityEngine;

// 용이 남기는 불장판. 서 있으면 일정 간격으로 탄다.
//
// 패링으로 막을 수 없다 — 떨어지는 순간(화염구·메테오)은 받아칠 수 있지만,
// 그 뒤 바닥에 남은 불은 "피해서 나가는 것"만 답이어야 한다.
// 용이 죽으면 같이 꺼진다. 보스를 잡았는데 불이 남아 플레이어를 태우면 억울하다.
public class DragonFireGround : MonoBehaviour
{
    // 불 그림에서 실제로 타는 부분의 비율 (160x128 프레임 중 가운데 90x64)
    private const float VisibleW = 90f / 160f;
    private const float VisibleH = 64f / 128f;

    private BossEnemy owner;
    private float radius;
    private float endTime;
    private int tickDamage;
    private float tickInterval;
    private bool dark;
    private float nextTick;
    private float loopAt;
    private PixelVfx current;
    private bool ending;

    public static DragonFireGround Spawn(BossEnemy owner, Vector2 at, float radius, float duration,
                                         int tickDamage, float tickInterval, bool dark)
    {
        var go = new GameObject(dark ? "DragonDarkFire" : "DragonFire");
        go.transform.position = at;
        var f = go.AddComponent<DragonFireGround>();
        f.owner = owner;
        f.radius = radius;
        f.endTime = Time.time + duration;
        f.tickDamage = tickDamage;
        f.tickInterval = Mathf.Max(0.1f, tickInterval);
        f.dark = dark;
        f.nextTick = Time.time + 0.15f;   // 떨어지는 순간의 피해와 겹치지 않게 한 박자 뒤부터
        f.current = f.PlayFire("DragonFireStart");
        f.loopAt = Time.time + 0.5f;
        return f;
    }

    private PixelVfx PlayFire(string id)
    {
        // 어둠 불꽃은 색을 곱해서는 안 나온다(주황에 보라를 곱하면 검붉어진다) — 보라로 다시 칠한 시트를 따로 쓴다
        if (dark) id = id.Replace("DragonFire", "DragonDarkFire");
        PixelVfx v = DragonFx.Play(id, transform.position, 1f, 0f);
        if (v == null) return null;

        // 원형 판정에 맞춰 그림을 늘린다 — 원본은 옆으로 납작한 타원이라 세로를 조금 더 늘린다
        var sr = v.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            Vector2 b = sr.sprite.bounds.size;
            Vector3 s = v.transform.localScale;
            float w = b.x * VisibleW * s.x, h = b.y * VisibleH * s.y;
            v.transform.localScale = new Vector3(s.x * radius * 2.1f / w, s.y * radius * 1.7f / h, 1f);
        }
        v.transform.SetParent(transform, true);
        return v;
    }

    private void Update()
    {
        bool ownerGone = owner == null || owner.isDead;

        if (!ending && (ownerGone || Time.time >= endTime))
        {
            ending = true;
            if (current != null) current.Stop();
            current = PlayFire("DragonFireEnd");
            Destroy(gameObject, 0.6f);
            return;
        }
        if (ending) return;

        if (loopAt > 0f && Time.time >= loopAt)
        {
            loopAt = -1f;
            if (current != null) current.Stop();
            current = PlayFire("DragonFireLoop");
        }

        if (Time.time >= nextTick)
        {
            // 맞으면 다음 틱까지 기다리고, 안 맞았으면 바로 다음 프레임에 다시 본다 —
            // 불 위로 걸어 들어오는 순간 곧바로 타야 "불 위에 서면 안 된다"가 읽힌다
            if (owner.DamagePlayerInRadius(transform.position, radius, tickDamage, false))
                nextTick = Time.time + tickInterval;
        }
    }
}
