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
    [System.Serializable]
    public class EnemyEntry
    {
        public EnemyType type;
        public EnemyInfo info;
        public GameObject prefab;
    }

    [Header("몬스터 목록")]
    [SerializeField] private List<EnemyEntry> entries;

    [Header("스폰 설정")]
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private int maxSpawnCount = 5;

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
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnRandomEnemy()
    {
        if (entries == null || entries.Count == 0) return;

        int randomIndex = Random.Range(0, entries.Count);
        SpawnEnemy(entries[randomIndex]);
    }

    public Enemy SpawnEnemy(EnemyType type)
    {
        EnemyEntry entry = entries.Find(e => e.type == type);
        return entry != null ? SpawnEnemy(entry) : null;
    }

    private Enemy SpawnEnemy(EnemyEntry entry)
    {
        if (entry.prefab == null) return null;

        GameObject go = Instantiate(entry.prefab, transform.position, Quaternion.identity);
        Enemy enemy = go.GetComponent<Enemy>();

        if (enemy == null)
        {
            Debug.LogError($"Spawner: {entry.prefab.name} 프리팹에 Enemy 컴포넌트가 없음");
            return null;
        }

        if (entry.info != null)
        {
            enemy.Initialize(entry.info);
            go.name = entry.info.EnemyName;
        }

        return enemy;
    }
}
