using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [원거리 세트] 탄막 확장 — 투사체 크기·사거리 증가
[CreateAssetMenu(fileName = "Synergy_ProjectileMod", menuName = "Harpe/Ability/Synergy/탄막 확장")]
public class ProjectileModEffect : AbilityEffect
{
    [SerializeField] private float size = 0.2f;
    [SerializeField] private float range = 0.1f;

    public override float ProjectileSizeBonus(AbilityContext ctx) { return size; }
    public override float ProjectileRangeBonus(AbilityContext ctx) { return range; }
}
