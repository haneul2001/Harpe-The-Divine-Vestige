using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [대시 세트] 날렵함 — 대시 쿨타임 단축
[CreateAssetMenu(fileName = "Synergy_DashCooldown", menuName = "Harpe/Ability/Synergy/날렵함")]
public class DashCooldownEffect : AbilityEffect
{
    [Tooltip("0.2 = 2초 → 1.6초")]
    [SerializeField] private float reduction = 0.2f;

    public override float DashCooldownReduction(AbilityContext ctx) { return reduction; }
}
