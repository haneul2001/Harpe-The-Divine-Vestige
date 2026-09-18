using UnityEngine;

// 한 판(run) 동안의 기록. 결과 화면이 이걸 읽어 보여 준다.
//
// 씬을 다시 로드하면 통째로 사라진다 — "이번 판"과 수명이 정확히 같아서
// 재시작할 때 따로 초기화할 것이 없다. 층 이동은 같은 씬에서 방만 갈아엎는
// 방식이라, 층이 넘어가도 이 기록은 이어진다.
public class RunStats : MonoBehaviour
{
    public static RunStats Instance { get; private set; }

    [Tooltip("현재 층. 층 이동 시스템이 붙으면 SetFloor로 올린다")]
    [SerializeField] private int floor = 1;

    public int Floor { get { return floor; } }
    public int Kills { get; private set; }

    // 실제로 흐른 시간. 일시정지는 빼고, 보스 처치 슬로우는 그대로 센다.
    public float Elapsed { get; private set; }

    // 방 정보는 RoomManager가 이미 정확히 들고 있다.
    // 여기서 또 세면 두 곳이 어긋났을 때 어느 쪽이 맞는지 알 수 없게 된다.
    public int RoomsCleared
    {
        get
        {
            var rm = RoomManager.Instance;
            if (rm == null || rm.AllRooms == null) return 0;

            int n = 0;
            for (int i = 0; i < rm.AllRooms.Count; i++)
                if (rm.AllRooms[i] != null && rm.AllRooms[i].IsCleared) n++;
            return n;
        }
    }

    public int TotalRooms
    {
        get
        {
            var rm = RoomManager.Instance;
            return rm != null && rm.AllRooms != null ? rm.AllRooms.Count : 0;
        }
    }

    public int CardsOwned
    {
        get { return AbilityInventory.Instance != null ? AbilityInventory.Instance.Owned.Count : 0; }
    }

    // 씬에 미리 놓아 둘 것이 없다. 전투 씬이면 알아서 하나 생긴다.
    // 재시작하면 씬과 함께 사라졌다가 새 판의 기록으로 다시 생긴다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        RuntimeSingletons.EnsureEachScene(Spawn);
    }

    private static void Spawn()
    {
        if (Instance != null) return;
        // 플레이어가 없는 씬(타이틀·컷신)에서는 셀 것이 없다
        if (FindObjectOfType<PlayerStatus>() == null) return;

        new GameObject("RunStats").AddComponent<RunStats>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        Enemy.AnyDied += OnEnemyDied;
    }

    private void OnDestroy()
    {
        Enemy.AnyDied -= OnEnemyDied;
        if (Instance == this) Instance = null;
    }

    private void OnEnemyDied(Enemy e)
    {
        Kills++;
    }

    // timeScale이 0인 동안(일시정지·결과 화면)은 세지 않는다.
    // unscaled로 재는 이유는 보스 처치 슬로우가 "오래 걸린 판"으로 둔갑하지 않게 하기 위해서다.
    private void Update()
    {
        if (Time.timeScale > 0f) Elapsed += Time.unscaledDeltaTime;
    }

    public void SetFloor(int value)
    {
        floor = Mathf.Max(1, value);
    }

    // 12:34 꼴. 한 시간을 넘길 판은 없다고 보고 분:초까지만 쓴다.
    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }
}
