using System.Collections;
using UnityEngine;

// 역처형. 플레이어의 처형을 그대로 되돌려 준다.
//
// 플레이어 체력이 30% 이하가 되면 머리 위에 붉은 해골 표식을 걸고, 잠시 뒤 순간이동해 처형 일격을 날린다.
// 받아치면 사신이 크게 다치고 기절한다 — 죽음을 받아친 자에게 주는 보상이다.
// 다른 패턴보다 먼저 뽑히도록 가중치가 크다. 재사용 대기로 연달아 오지는 않는다.
public class BossPatternReaperExecution : BossPattern
{
    [Range(0.05f, 0.9f)] [SerializeField] private float triggerHpRatio = 0.3f;
    [Tooltip("두 번째 목숨의 사신은 더 일찍 목을 노린다")]
    [Range(0.05f, 0.9f)] [SerializeField] private float triggerHpRatioSecondLife = 0.4f;
    [SerializeField] private float markTime = 1.5f;
    [Tooltip("최대 체력 대비 피해")]
    [Range(0.05f, 1f)] [SerializeField] private float damageRatio = 0.4f;
    [SerializeField] private float strikeRadius = 4.5f;
    [SerializeField] private float strikeHalfAngle = 80f;
    [Tooltip("받아쳤을 때 사신이 받는 피해 (최대 체력 대비)")]
    [SerializeField] private float parryPunishRatio = 0.05f;
    [SerializeField] private float parryStun = 2f;

    public override bool UsesHitBox { get { return false; } }
    public override bool DrawsOwnSlash { get { return true; } }
    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration) { return null; }

    public override DangerShape[] DangerShapes(BossEnemy boss)
    {
        return new DangerShape[] { DangerShape.Circle(1.2f, DangerOrigin.Player) };
    }

    public override bool IsUsable(BossEnemy boss)
    {
        if (!base.IsUsable(boss)) return false;
        var ps = PlayerStatus();
        var r = boss as ReaperBoss;
        float trig = r != null && r.SecondLife ? triggerHpRatioSecondLife : triggerHpRatio;
        return ps != null && ps.MaxHp > 0 && (float)ps.CurrentHp / ps.MaxHp <= trig;
    }

    private static PlayerStatus PlayerStatus()
    {
        Transform p = ReaperFx.Player();
        return p != null ? p.GetComponent<PlayerStatus>() : null;
    }

    private static Sprite skull;

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        var r = boss as ReaperBoss;
        Transform p = ReaperFx.Player();
        if (r == null || p == null) yield break;

        ToastManager.Show("처형 표식 — 받아쳐라!", ToastManager.Kind.Warn);

        // 머리 위 붉은 해골 + 발밑을 따라다니는 원
        if (skull == null) skull = Resources.Load<Sprite>("UI/HarvestSkull");
        var markGo = new GameObject("ReaperExecutionMark");
        var msr = markGo.AddComponent<SpriteRenderer>();
        msr.sprite = skull;
        msr.color = new Color(1f, 0.35f, 0.35f, 1f);
        msr.sortingLayerName = "Skill";
        msr.sortingOrder = 60;
        if (skull != null) markGo.transform.localScale = Vector3.one * (1.1f / Mathf.Max(0.01f, skull.bounds.size.x));
        DangerZone ring = DangerZone.Circle(p.position, 1.2f, markTime, p);

        Vector2 from = boss.transform.position;
        float t = 0f;
        bool hidden = false;
        while (t < markTime && !boss.isDead)
        {
            t += Time.deltaTime;
            markGo.transform.position = p.position + Vector3.up * (1.6f + Mathf.Sin(Time.time * 10f) * 0.08f);
            if (!hidden && t >= r.ClipLength("TeleportIn") * 0.7f)
            {
                hidden = true;
                r.SetHidden(true);
                ReaperFx.Burst(r.Chest, "red", 1.8f);
            }
            yield return null;
        }
        if (ring != null) ring.CompleteNow();
        if (boss.isDead) { Destroy(markGo); yield break; }

        // 등 뒤에서 솟아오르며 벤다
        Vector2 pp = p.position;
        Vector2 away = pp - from;
        if (away.sqrMagnitude < 0.01f) away = Vector2.right;
        Vector2 at = r.ClampToArena(pp + away.normalized * 2.2f, 1.2f);
        r.TeleportTo(at);
        r.SetHidden(false);
        ReaperFx.Burst(r.Chest, "red", 2f);
        r.PlayState("TeleportOut");

        r.ExecutionParried = false;
        r.ExecutionWindowOpen = true;
        yield return boss.WaitForNextAnimHit(0.9f);

        Vector2 toP = (Vector2)p.position - at;
        float angle = Mathf.Atan2(toP.y, toP.x) * Mathf.Rad2Deg;
        var ps = PlayerStatus();
        int dmg = ps != null ? Mathf.CeilToInt(ps.MaxHp * damageRatio) : 30;
        ReaperFx.CrescentHit(boss, at, strikeRadius, strikeHalfAngle, angle, dmg);
        CameraShake.Shake(0.6f);
        r.ExecutionWindowOpen = false;
        Destroy(markGo);

        if (r.ExecutionParried && !boss.isDead)
        {
            boss.TakeDamage(Mathf.RoundToInt(boss.maxHp * parryPunishRatio), true, new Color(1f, 0.85f, 0.3f));
            yield return r.Stun(parryStun, "처형을 받아쳤다!");
        }
        else yield return ReaperFx.Wait(boss, 0.3f);
    }
}
