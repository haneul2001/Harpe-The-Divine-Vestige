using UnityEngine;

// 몬스터가 죽으면 그 자리에 골드 동전을 떨어뜨린다.
//
// 적 프리팹마다 드랍 설정을 붙이면 반드시 빠뜨리는 프리팹이 생기므로,
// 죽음 이벤트(Enemy.AnyDied) 하나만 듣고 등급에 따라 뿌린다.
// 씬에 아무것도 놓지 않아도 되도록 스스로 생긴다.
public class GoldDropper : MonoBehaviour
{
    private const string CoinResourcePath = "Loot/GoldCoin";

    [System.Serializable]
    public class DropRule
    {
        public int minCoins = 1;
        public int maxCoins = 3;
        public int minValue = 1;
        public int maxValue = 2;
    }

    [Tooltip("일반 잡몹")]
    public DropRule normal = new DropRule { minCoins = 1, maxCoins = 3, minValue = 1, maxValue = 2 };
    [Tooltip("정예")]
    public DropRule elite = new DropRule { minCoins = 4, maxCoins = 6, minValue = 2, maxValue = 4 };
    [Tooltip("보스")]
    public DropRule boss = new DropRule { minCoins = 10, maxCoins = 14, minValue = 4, maxValue = 8 };

    public static GoldDropper Instance { get; private set; }

    private GameObject coinPrefab;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        if (FindObjectOfType<GoldDropper>() != null) return;

        var go = new GameObject("GoldDropper");
        go.AddComponent<GoldDropper>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        coinPrefab = Resources.Load<GameObject>(CoinResourcePath);
        if (coinPrefab == null)
            Debug.LogWarning("[GoldDropper] Resources/" + CoinResourcePath + " 동전 프리팹이 없다", this);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        Enemy.AnyDied += OnEnemyDied;
    }

    private void OnDisable()
    {
        Enemy.AnyDied -= OnEnemyDied;
    }

    private void OnEnemyDied(Enemy enemy)
    {
        if (enemy == null || coinPrefab == null) return;
        Drop(enemy.transform.position, RuleFor(enemy.Grade));
    }

    private DropRule RuleFor(EnemyGrade grade)
    {
        switch (grade)
        {
            case EnemyGrade.Boss: return boss;
            case EnemyGrade.Elite: return elite;
            default: return normal;
        }
    }

    public void Drop(Vector3 position, DropRule rule)
    {
        if (coinPrefab == null || rule == null) return;

        int count = Random.Range(rule.minCoins, rule.maxCoins + 1);
        for (int i = 0; i < count; i++)
            GoldCoin.Spawn(coinPrefab, position, Random.Range(rule.minValue, rule.maxValue + 1));
    }
}
