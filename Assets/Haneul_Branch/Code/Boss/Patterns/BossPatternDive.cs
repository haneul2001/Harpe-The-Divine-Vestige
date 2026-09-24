using System.Collections;
using UnityEngine;

// 지하 잠행. 땅으로 꺼졌다가 플레이어 발밑에서 솟는다.
//
// 도망만 다니는 장기전을 끊는 패턴이다. 다만 "보고 반응할 수 없는" 공격이 되면 안 되므로
// 솟을 자리를 먼저 붉게 보여 주고, 다 차오른 뒤에 올라온다.
public class BossPatternDive : BossPattern
{
    [Header("잠행")]
    [Tooltip("가라앉는 데 걸리는 시간(초)")]
    [Min(0.1f)] [SerializeField] private float sinkTime = 0.4f;

    [Tooltip("땅속에서 이동하는 시간(초). 이 동안 플레이어는 자리를 옮길 수 있다")]
    [Min(0.1f)] [SerializeField] private float travelTime = 0.8f;

    [Tooltip("솟을 자리를 보여 주는 시간(초)")]
    [Min(0.2f)] [SerializeField] private float surfaceWarning = 0.7f;

    [Tooltip("솟구칠 때의 피해 반경")]
    [Min(0.5f)] [SerializeField] private float burstRadius = 2.2f;

    [Header("연출")]
    [SerializeField] private float shake = 0.4f;
    [SerializeField] private string vfxId = "EnemyHit";

    public override bool MovesSelf { get { return true; } }

    // 잠행은 "지금 여기"가 위험한 게 아니라 "곧 저기"가 위험하다. 예고는 패턴 안에서 낸다.
    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration) { return null; }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        boss.CloseHitBox();

        SpriteRenderer sr = boss.spriteRenderer;
        Collider2D[] cols = boss.GetComponentsInChildren<Collider2D>();

        // ① 가라앉는다 — 이 동안은 때릴 수도, 맞을 수도 없다
        boss.IsInvulnerableExternal = true;
        yield return Fade(boss, sr, 1f, 0f, sinkTime);
        SetColliders(cols, false);

        if (boss.isDead) { Restore(boss, sr, cols); yield break; }

        // ② 땅속 이동 — 플레이어를 따라간다
        float t = travelTime;
        while (t > 0f && !boss.isDead)
        {
            t -= Time.deltaTime;
            if (boss.rb != null) boss.rb.velocity = Vector2.zero;
            yield return null;
        }

        Vector2 target = boss.transform.position;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) target = p.transform.position;

        boss.transform.position = target;

        // ③ 솟을 자리를 보여 준다
        DangerZone.Circle(target, burstRadius, surfaceWarning);
        t = surfaceWarning;
        while (t > 0f && !boss.isDead) { t -= Time.deltaTime; yield return null; }

        // ④ 솟구친다
        SetColliders(cols, true);
        yield return Fade(boss, sr, 0f, 1f, 0.15f);
        boss.IsInvulnerableExternal = false;

        if (boss.isDead) { Restore(boss, sr, cols); yield break; }

        if (shake > 0f) CameraShake.Shake(shake);
        if (!string.IsNullOrEmpty(vfxId)) PixelVfx.Play(vfxId, boss.transform.position);

        boss.DamagePlayerInRadius(boss.transform.position, burstRadius);
    }

    private static void SetColliders(Collider2D[] cols, bool on)
    {
        for (int i = 0; i < cols.Length; i++)
            if (cols[i] != null) cols[i].enabled = on;
    }

    private IEnumerator Fade(BossEnemy boss, SpriteRenderer sr, float from, float to, float duration)
    {
        if (sr == null) yield break;

        float t = 0f;
        while (t < 1f && !boss.isDead)
        {
            t += duration > 0f ? Time.deltaTime / duration : 1f;
            Color c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, Mathf.Clamp01(t)));
            if (boss.rb != null) boss.rb.velocity = Vector2.zero;
            yield return null;
        }
    }

    // 도중에 죽어도 투명한 시체나 꺼진 콜라이더로 남지 않게 한다
    private void Restore(BossEnemy boss, SpriteRenderer sr, Collider2D[] cols)
    {
        if (sr != null)
        {
            Color c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, 1f);
        }
        SetColliders(cols, true);
        boss.IsInvulnerableExternal = false;
    }
}
