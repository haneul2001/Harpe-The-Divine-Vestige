using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class EnemyAttackDataClass
{
    public string attackName;

    public float damage;
    public float cooldown;
    public float range;

    public GameObject effect; // 이펙트 (선택)
}