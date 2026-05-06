using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyType
{
    zombie,bat
}

public class Spawner : MonoBehaviour
{
    [SerializeField]
    private List<EnemyInfo> enemy;
    [SerializeField]
    private List<GameObject> enemyPrefabs;

    void Start()
    {
        for(int i =0; i < enemy.Count; i++)
        {
            var enemy = SpawnEnemy((EnemyType)i);//0이 좀비, 1이 박쥐
            enemy.PrintEnemyData();
        }
    }

    public EnemyDataFuntion SpawnEnemy(EnemyType type)
    {
        EnemyDataFuntion newEnemy = Instantiate(enemyPrefabs[(int)type]).GetComponent<EnemyDataFuntion>();
        newEnemy.enemyInfo = enemy[(int)type];
        newEnemy.name = newEnemy.enemyInfo.EnemyName;
        return newEnemy;
    }
}
