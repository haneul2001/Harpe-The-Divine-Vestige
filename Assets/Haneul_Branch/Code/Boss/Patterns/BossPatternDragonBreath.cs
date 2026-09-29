using System.Collections.Generic;
using System.Collections;
using UnityEngine;

// 용의 부채꼴 브레스. 입 앞으로 넓은 부채꼴을 예고하고, 그 안을 한동안 불로 채운다.
//
// 피하는 방법은 부채꼴 밖(옆·뒤)으로 나가는 것이다. 뿜는 동안 계속 타므로
// 예고가 끝난 뒤 늦게 빠져나가도 조금씩 탄다.
// 3페이즈(어둠)에는 뿜는 동안 방 곳곳에 어둠 마법진이 같이 깔린다 — 부채꼴 밖이라고 안전하지 않다.
public class BossPatternDragonBreath : BossPattern
{
    [Header("브레스")]
    [Tooltip("불이 닿는 길이 (예고 부채꼴 반지름)")]
    [Min(1f)] [SerializeField] private float radius = 12f;
    [Tooltip("부채꼴 반각(도). 불 그림은 이 각도에 맞춰 위아래로 늘어난다")]
    [Range(5f, 60f)] [SerializeField] private float halfAngle = 13f;

    [Tooltip("플레이어가 위아래에 있을 때 입을 얼마나 틀 수 있는지(도). 옆모습 용이라 크게 못 돈다")]
    [Range(0f, 45f)] [SerializeField] private float maxTilt = 25f;

    [Min(0.1f)] [SerializeField] private float breathDuration = 1.2f;
    [Min(0.05f)] [SerializeField] private float tickInterval = 0.3f;
    [Min(1)] [SerializeField] private int tickDamage = 8;

    [Header("3페이즈 — 어둠 마법진 동시")]
    [Min(0)] [SerializeField] private int darkCircles = 4;

    public override bool UsesHitBox { get { return false; } }
    public override bool DrawsOwnSlash { get { return true; } }

    public override DangerShape[] DangerShapes(BossEnemy boss)
    {
        return new DangerShape[] { DangerShape.Sector(radius, halfAngle, DangerOrigin.Boss) };
    }

    // 예고할 때 정한 방향을 그대로 뿜는다 — 예고 뒤에 다시 겨누면 "예고와 다른 곳에 불이 온다"가 된다
    private float aimAngle;
    private Vector2 apex;

    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration)
    {
        var dragon = boss as DragonBoss;
        apex = dragon != null ? (Vector2)dragon.GroundMouthPoint : (Vector2)boss.transform.position;
        aimAngle = AimAngle(boss, apex);
        return DangerZone.Sector(apex, radius, halfAngle, aimAngle, duration);
    }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        var dragon = boss as DragonBoss;

        // 입을 벌린 자세로 굳혀 둔다 — 뿜는 동안 동작이 끝나 입을 닫으면 불만 허공에서 나온다
        if (boss.animator != null) boss.animator.speed = 0f;

        Vector3 mouth = dragon != null ? dragon.MouthPosition : boss.transform.position;

        // 굵은 불줄기 하나. 입에서 예고 부채꼴의 끝(바닥)까지 비스듬히 내리꽂는다 —
        // 입은 공중에 있고 예고는 바닥에 깔리므로, 수평으로 뿜으면 불이 예고보다 위로 떠 보인다
        Vector2 dirA = new Vector2(Mathf.Cos(aimAngle * Mathf.Deg2Rad), Mathf.Sin(aimAngle * Mathf.Deg2Rad));
        Vector2 target = apex + dirA * radius;
        Vector2 flameVec = target - (Vector2)mouth;
        float flameAngle = Mathf.Atan2(flameVec.y, flameVec.x) * Mathf.Rad2Deg;
        float flameLen = flameVec.magnitude;

        PixelVfx flame = SpawnFlame("DragonBreathStart", mouth, flameAngle, flameLen, StartFlame);
        CameraShake.Shake(0.3f);

        if (dragon != null && dragon.IsDarkPhase && darkCircles > 0)
            dragon.ScatterDarkCircles(darkCircles, 0.25f);

        float start = Time.time;
        float end = start + breathDuration;
        float next = 0f;
        bool looping = false;

        // 발치 바닥 불꽃 — 입에서 나온 불줄기는 공중(입 높이)에 그려져서, 바닥에 깔린 예고 부채꼴의
        // 발치 쪽은 불 그림이 비어 보였다. 발밑에서 앞으로 퍼져 나가며 바닥을 태운다 (피해는 브레스 판정이 준다)
        var ground = new List<GroundFire>();
        float groundTime = 0f;
        while (Time.time < end && !boss.isDead)
        {
            groundTime = Time.time - start;
            SpreadGroundFire(ground, dirA, groundTime);

            // 불이 다 뻗은 뒤에는 계속 타오르는 그림으로 갈아 끼운다
            if (!looping && Time.time - start >= 0.45f)
            {
                looping = true;
                if (flame != null) flame.Stop();
                flame = SpawnFlame("DragonBreath", mouth, flameAngle, flameLen, LoopFlame);
            }

            if (Time.time >= next)
            {
                next = Time.time + tickInterval;
                boss.DamagePlayerInSector(apex, radius, halfAngle, aimAngle, tickDamage);
            }
            if (boss.rb != null) boss.rb.velocity = Vector2.zero;
            yield return null;
        }

        if (flame != null) flame.Stop();
        if (!boss.isDead) SpawnFlame("DragonBreathEnd", mouth, flameAngle, flameLen, LoopFlame);
        foreach (var g in ground)
        {
            if (g.vfx != null) g.vfx.Stop();
            if (!boss.isDead) PlayGroundFire("DragonFireEnd", g.pos, g.r);
        }

        if (boss.animator != null) boss.animator.speed = 1f;
    }

    [Header("발치 바닥 불꽃")]
    [Tooltip("예고 부채꼴 길이 중 이 비율까지 바닥 불꽃을 깐다 (나머지는 공중의 불줄기가 덮는다)")]
    [Range(0.2f, 1f)] [SerializeField] private float groundFireReach = 0.7f;
    [Tooltip("바닥 불꽃이 앞으로 퍼지는 빠르기(칸/초)")]
    [SerializeField] private float groundFireSpeed = 22f;

    private class GroundFire { public Vector2 pos; public float r; public float at; public PixelVfx vfx; public bool looping; }

    // 발밑에서 앞으로 크기를 키워 가며 불 자리를 잡는다. 부채꼴이 넓어지는 만큼 불도 커진다
    private void SpreadGroundFire(List<GroundFire> list, Vector2 dir, float elapsed)
    {
        if (list.Count == 0)
        {
            float tan = Mathf.Tan(halfAngle * Mathf.Deg2Rad);
            float d = 1.2f;
            while (d < radius * groundFireReach)
            {
                float r = Mathf.Clamp(d * tan * 0.95f, 0.9f, 3.2f);
                list.Add(new GroundFire { pos = apex + dir * d, r = r, at = d / Mathf.Max(1f, groundFireSpeed) });
                d += r * 1.25f;
            }
        }

        foreach (var g in list)
        {
            if (g.vfx == null && !g.looping && elapsed >= g.at)
            {
                g.vfx = PlayGroundFire("DragonFireStart", g.pos, g.r);
                continue;
            }
            // 붙는 그림이 끝나면 타오르는 그림으로 갈아 끼운다
            if (g.vfx != null && !g.looping && elapsed >= g.at + 0.45f)
            {
                g.vfx.Stop();
                g.vfx = PlayGroundFire("DragonFireLoop", g.pos, g.r);
                g.looping = true;
            }
        }
    }

    // 불타는 바닥 그림을 반지름에 맞춰 늘린다 (160x128 프레임 중 실제로 타는 곳은 가운데 90x64)
    private static PixelVfx PlayGroundFire(string id, Vector2 at, float r)
    {
        PixelVfx v = PixelVfx.Play(id, at);
        if (v == null) return null;
        // 반복 그림은 스스로 꺼지지 않는다. 패턴이 중간에 끊겨도 바닥에 남지 않게 수명을 걸어 둔다
        Object.Destroy(v.gameObject, 5f);
        var sr = v.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            Vector2 b = sr.sprite.bounds.size;
            Vector3 s = v.transform.localScale;
            float w = b.x * (90f / 160f) * s.x, h = b.y * (64f / 128f) * s.y;
            v.transform.localScale = new Vector3(s.x * r * 2.1f / w, s.y * r * 1.5f / h, 1f);
        }
        return v;
    }

    // 불 그림에서 분사구 자리(그림 가운데 기준, 원본 px/100)와 불이 뻗는 길이.
    // 시작 그림은 불이 더 길게 뻗어 나가므로 따로 잰다 — 같은 배율을 쓰면 예고 끝을 넘어간다
    private struct FlameArt { public Vector2 nozzle; public float length; }
    private static readonly FlameArt StartFlame = new FlameArt { nozzle = new Vector2(-0.79f, -0.18f), length = 1.69f };
    private static readonly FlameArt LoopFlame  = new FlameArt { nozzle = new Vector2(-0.83f, -0.12f), length = 1.36f };

    // 불 그림은 왼쪽 끝(분사구)에서 오른쪽으로 뻗는다. 분사구가 입에 오도록 옮기고,
    // 불 끝이 목표까지 닿도록 가로세로를 같은 배율로 키운다 (길어진 만큼 굵어진다).
    // 왼쪽을 볼 때는 180도 돌리지 않고 좌우로 뒤집는다 — 돌리면 불이 위아래로 뒤집혀 보인다
    private PixelVfx SpawnFlame(string id, Vector3 mouth, float angle, float length, FlameArt art)
    {
        bool left = Mathf.Cos(angle * Mathf.Deg2Rad) < 0f;
        PixelVfx v = DragonFx.Play(id, mouth, 1f, left ? angle - 180f : angle);
        if (v == null) return null;

        float s = length / art.length;
        // 불 그림은 길이의 약 0.44배 굵기(반각 13도)다. 예고 부채꼴이 그보다 넓으면 그만큼 위아래로 늘려
        // 예고 칸과 불이 같은 폭으로 보이게 한다
        float thick = Mathf.Max(1f, Mathf.Tan(halfAngle * Mathf.Deg2Rad) / Mathf.Tan(13f * Mathf.Deg2Rad));
        v.transform.localScale = new Vector3(left ? -s : s, s * thick, 1f);
        v.transform.position = mouth - v.transform.TransformVector(art.nozzle);
        return v;
    }

    private float AimAngle(BossEnemy boss, Vector2 from)
    {
        float facing = boss.IsFacingRight ? 0f : 180f;
        Transform p = DragonFx.Player();
        if (p == null) return facing;

        Vector2 to = (Vector2)p.position - from;
        float want = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        float delta = Mathf.Clamp(Mathf.DeltaAngle(facing, want), -maxTilt, maxTilt);
        return facing + delta;
    }
}
