using System.Collections;
using UnityEngine;

// 층과 층 사이를 잇는 진행 담당.
//
//   보스 방 클리어 → 포탈 등장 → 밟으면 다음 층
//
// 던전 생성기는 "한 층을 어떻게 짓는가"만 알고, 여기가 "몇 층째인가"를 안다.
// 씬은 그대로 두고 방만 갈아엎으므로 로딩 화면이 없다.
public class FloorFlow : MonoBehaviour
{
    public static FloorFlow Instance { get; private set; }

    [Tooltip("보스를 잡고 나서 포탈이 나타나기까지의 시간(초). 처치 연출과 겹치지 않게 둔다")]
    [SerializeField] private float portalDelay = 1.5f;

    [Tooltip("포탈 프리팹의 Resources 경로")]
    [SerializeField] private string portalResourcePath = "Dungeon/FloorPortal";

    [SerializeField] private string portalMessage = "다음 층으로 가는 길이 열렸다";

    private bool advancing;
    private DungeonGenerator generator;

    // 씬이 열릴 때마다 챙긴다. 재시작해도 다시 생긴다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        RuntimeSingletons.EnsureEachScene(Spawn);
    }

    private static void Spawn()
    {
        if (Instance != null) return;
        // 던전이 없는 씬(타이틀·마을)에는 넘어갈 층도 없다
        if (FindObjectOfType<DungeonGenerator>() == null) return;

        new GameObject("FloorFlow").AddComponent<FloorFlow>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        Room.AnyCleared += OnRoomCleared;
    }

    private void OnDestroy()
    {
        Room.AnyCleared -= OnRoomCleared;
        if (Instance == this) Instance = null;
    }

    private DungeonGenerator Generator
    {
        get
        {
            if (generator == null) generator = FindObjectOfType<DungeonGenerator>();
            return generator;
        }
    }

    // ─────────────────────────────────────────────
    // 포탈
    // ─────────────────────────────────────────────

    private void OnRoomCleared(Room room)
    {
        if (room == null || room.type != RoomType.Boss) return;
        StartCoroutine(SpawnPortal(room));
    }

    private IEnumerator SpawnPortal(Room room)
    {
        if (portalDelay > 0f) yield return new WaitForSeconds(portalDelay);
        if (room == null) yield break;

        GameObject prefab = Resources.Load<GameObject>(portalResourcePath);
        if (prefab == null)
        {
            Debug.LogError("[FloorFlow] Resources/" + portalResourcePath + " 를 찾지 못했다. 다음 층으로 갈 방법이 없다");
            yield break;
        }

        // 방의 자식으로 둔다. 다른 방으로 나가면 방과 함께 꺼지고,
        // 층을 새로 지을 때 방과 함께 사라진다 — 따로 치울 필요가 없다.
        GameObject portal = Instantiate(prefab, room.transform);
        portal.transform.position = room.transform.position;

        ToastManager.Show(portalMessage);
    }

    // ─────────────────────────────────────────────
    // 층 이동
    // ─────────────────────────────────────────────

    public void Advance()
    {
        if (advancing) return;

        DungeonGenerator gen = Generator;
        if (gen == null)
        {
            Debug.LogWarning("[FloorFlow] 씬에 DungeonGenerator가 없다");
            return;
        }

        // 마지막 층을 깼으면 한 판이 끝난 것이다. 결과 화면을 승리로 띄운다.
        if (gen.IsLastFloor)
        {
            GameOverScreen.Show(true);
            return;
        }

        advancing = true;
        int next = gen.CurrentFloorIndex + 1;

        DungeonIntro intro = FindObjectOfType<DungeonIntro>();
        if (intro == null)
        {
            // 연출이 없으면 그냥 갈아엎는다. 층 이동이 연출 유무에 묶이면 안 된다.
            gen.GenerateFloor(next);
            advancing = false;
            return;
        }

        // 층 이름은 GenerateFloor가 연출에 직접 넣어 준다. 그래서 여기선 null을 넘긴다 —
        // 여기서 또 넘기면 생성기가 넣은 값을 덮어써 두 곳이 어긋난다.
        StartCoroutine(RunTransition(intro, gen, next));
    }

    private IEnumerator RunTransition(DungeonIntro intro, DungeonGenerator gen, int next)
    {
        Coroutine c = intro.PlayFloorTransition(null, null, () => gen.GenerateFloor(next));

        // 연출 오브젝트가 꺼져 있으면 코루틴이 시작되지 않는다.
        // 그대로 두면 층이 안 넘어간 채 advancing만 남아 영영 못 올라간다.
        if (c == null) gen.GenerateFloor(next);
        else yield return c;

        advancing = false;
    }
}
