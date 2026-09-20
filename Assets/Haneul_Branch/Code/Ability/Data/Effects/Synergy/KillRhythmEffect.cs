using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [처치 세트] 사냥의 리듬 — 처치 시 잠깐 이동속도 증가
[CreateAssetMenu(fileName = "Synergy_KillRhythm", menuName = "Harpe/Ability/Synergy/사냥의 리듬")]
public class KillRhythmEffect : AbilityEffect
{
    [SerializeField] private float moveSpeed = 0.15f;
    [SerializeField] private float duration = 2f;

    private class S { public float until; }

    public override void OnEnemyKilled(AbilityContext ctx, Enemy enemy) { ctx.State<S>(this).until = Time.time + duration; }
    public override float MoveSpeedBonus(AbilityContext ctx) { return Time.time < ctx.State<S>(this).until ? moveSpeed : 0f; }
}
