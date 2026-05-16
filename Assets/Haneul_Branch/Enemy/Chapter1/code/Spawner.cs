using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyType
{
    zombie,
    bat
}

public class Spawner : MonoBehaviour
{
    [SerializeField]
    private List<EnemyInfo> enemyInfos;

    [SerializeField]
    private List<GameObject> enemyPrefabs;

    [Header("스폰 설정")]
    [SerializeField]
    private float spawnInterval = 5f;

    [SerializeField]
    private int maxSpawnCount = 5;

    private int currentSpawnCount;

    private void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        while (currentSpawnCount < maxSpawnCount)
        {
            SpawnRandomEnemy();

            currentSpawnCount++;
            Debug.Log(currentSpawnCount);
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnRandomEnemy()
    {
        int randomIndex = Random.Range(0, enemyPrefabs.Count);

        SpawnEnemy((EnemyType)randomIndex);
    }

    public EnemyDataFuntion SpawnEnemy(EnemyType type)
    {
        EnemyDataFuntion newEnemy =
            Instantiate(enemyPrefabs[(int)type], transform.position, Quaternion.identity)
            .GetComponent<EnemyDataFuntion>();

        newEnemy.enemyInfo = enemyInfos[(int)type];

        newEnemy.name = newEnemy.enemyInfo.EnemyName;

        return newEnemy;
    }
}