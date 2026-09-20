using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [대시 세트] 잔영 — 대시(Shift)를 쓸 때마다 대시 공격(X) 쿨타임 감소
[CreateAssetMenu(fileName = "Synergy_Afterimage", menuName = "Harpe/Ability/Synergy/잔영")]
public class AfterimageEffect : AbilityEffect
{
    [SerializeField] private float seconds = 1f;

    public override void OnDashStart(AbilityContext ctx, Vector2 start, Vector2 dir)
    {
        var dashAttack = ctx.player != null ? ctx.player.GetComponent<PlayerDashAttack>() : null;
        if (dashAttack != null) dashAttack.ReduceCooldown(seconds);
    }
}
