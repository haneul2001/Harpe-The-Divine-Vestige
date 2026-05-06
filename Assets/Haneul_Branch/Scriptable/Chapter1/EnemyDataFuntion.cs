using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDataFuntion : MonoBehaviour
{

    public EnemyInfo enemyInfo;
public void PrintEnemyData()
{
    Debug.Log("-----------------------------------");
    Debug.Log("몬스터 이름 :: "+ enemyInfo.EnemyName);
    Debug.Log("체력 :: "+ enemyInfo.HP);
    foreach(var attack in enemyInfo.Attacks)
    {
        Debug.Log("몬스터 스킬 ::" + attack.attackName);
        Debug.Log("공격력 :: "+ attack.damage);
    }
    Debug.Log("경험치 :: "+ enemyInfo.exp);
    Debug.Log("이동 속도 :: "+ enemyInfo.Speed);
    Debug.Log("소울 :: "+ enemyInfo.Soul);
    Debug.Log("-----------------------------------");
}

   
}
