using System.Collections.Generic;
using UnityEngine;

// [은신] 암살자의 흔적 — 은신 공격에 표식, 표식 대상을 처치하면 은신 재사용 대기 초기화
[CreateAssetMenu(fileName = "Effect_AssassinMark", menuName = "Harpe/Ability/Effect/암살자의 흔적")]
public class AssassinMarkEffect : AbilityEffect
{
    [Tooltip("은신이 풀린 뒤 이 시간 안의 적중을 '은신 공격'으로 친다")]
    [SerializeField] private float stealthWindow = 0.6f;
    [SerializeField] private float markDuration = 6f;

    private class S
    {
        public float window;
        public Dictionary<Enemy, float> marks = new Dictionary<Enemy, float>();
        public Dictionary<Enemy, FxMark> icons = new Dictionary<Enemy, FxMark>();
    }

    public override void OnStealthExit(AbilityContext ctx)
    {
        ctx.State<S>(this).window = Time.time + stealthWindow;
    }

    public override void OnEnemyHit(AbilityContext ctx, HitInfo hit)
    {
        var st = ctx.State<S>(this);
        bool fromStealth = PlayerStealth.IsHidden || Time.time < st.window;
        if (!fromStealth || !hit.kind.IsAttack() || hit.enemy == null) return;
        st.marks[hit.enemy] = Time.time + markDuration;

        FxMark icon;
        if (st.icons.TryGetValue(hit.enemy, out icon) && icon != null) icon.Extend(markDuration);
        else st.icons[hit.enemy] = SpriteFx.Mark(hit.enemy, markDuration);
    }

    public override void OnEnemyKilled(AbilityContext ctx, Enemy enemy)
    {
        var st = ctx.State<S>(this);
        float until;
        if (!st.marks.TryGetValue(enemy, out until)) return;
        st.marks.Remove(enemy);
        if (Time.time > until || PlayerStealth.Instance == null) return;

        PlayerStealth.Instance.ResetCooldown();
        TraitUtil.Text(ctx.playerTransform.position, "은신 초기화");
    }
}
