using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyInfo", menuName = "Scriptable/EnemyeData")]
public class EnemyInfo : ScriptableObject
{
    [Header("Enemy Info")]
    [SerializeField] private string enemyName;
    public string EnemyName => enemyName;

    [SerializeField] private int hp;
    public int HP => hp;

    [SerializeField] private int damage;
    public int Damage => damage;

    [SerializeField] public float exp;
    public float Exp => exp;

    [SerializeField] private float speed;
    public float Speed => speed;

    [SerializeField] private int soul;
    public int Soul => soul;

    [Header("Attack Data")]
    [SerializeField]
    private List<EnemyAttackDataClass> attacks;
    public List<EnemyAttackDataClass> Attacks => attacks;
}