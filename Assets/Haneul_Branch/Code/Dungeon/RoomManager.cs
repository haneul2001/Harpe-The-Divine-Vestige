using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 현재 어느 방에 있는지 추적하고, 방 이동 시 카메라 경계 교체 + 방 활성/비활성을 처리한다.
// 씬에 빈 오브젝트 하나 만들어 붙이면 됨 (DungeonGenerator와 같은 오브젝트여도 무방).
public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [Header("참조 (비우면 자동 탐색)")]
    [SerializeField] private Transform player;
    [SerializeField] private CameraFollow cameraFollow;

    [Header("이동 설정")]
    [Tooltip("방 이동 직후 이 시간 동안 다른 문이 반응하지 않음 (문끼리 튕기는 것 방지)")]
    [SerializeField] private float travelCooldown = 0.25f;
    [Tooltip("문 통과 시 플레이어 속도를 0으로 만들지 여부")]
    [SerializeField] private bool zeroVelocityOnTravel = false;

    [Header("방 전환 연출 (아이작 방식)")]
    [Tooltip("카메라가 옆 방으로 미끄러지는 시간(초). 0이면 예전처럼 즉시 전환.\n" +
             "0.3~0.45가 무난하다. 길수록 묵직하지만 답답해진다.")]
    [SerializeField] private float slideDuration = 0.38f;

    [Tooltip("전환 직후 플레이어를 방 안쪽으로 밀어 넣는 시간(초). 0이면 그 자리에 툭 놓인다.\n" +
             "'순간이동'이 아니라 '걸어 들어온' 느낌을 만드는 부분.")]
    [SerializeField] private float pushInDuration = 0.14f;

    [Tooltip("밀어 넣는 속도")]
    [SerializeField] private float pushInSpeed = 6f;

    [Tooltip("도착하는 순간 카메라를 툭 치는 세기. 0이면 없음")]
    [SerializeField] private float arriveShake = 0.12f;

    public Room Current { get; private set; }
    // 전환 중에는 문 트리거·입력을 모두 막는다
    public bool IsTransitioning { get; private set; }

    // 던전에 배치된 모든 방 (미니맵 등에서 사용)
    public IReadOnlyList<Room> AllRooms => allRooms;

    // 던전 생성 직후 / 방이 바뀔 때마다 발생
    public event System.Action OnDungeonReady;
    public event System.Action<Room> OnRoomChanged;

    private readonly List<Room> allRooms = new List<Room>();
    private float travelUnlockTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void ResolveRefs()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else Debug.LogError("[RoomManager] 'Player' 태그 오브젝트를 찾을 수 없음");
        }

        if (cameraFollow == null)
        {
            cameraFollow = FindObjectOfType<CameraFollow>();
            if (cameraFollow == null)
                Debug.LogWarning("[RoomManager] 씬에 CameraFollow가 없음 — 카메라 경계 교체 생략");
        }
    }

    // 생성기가 방을 다 만든 뒤 호출한다.
    public void Initialize(List<Room> rooms, Room startRoom)
    {
        ResolveRefs();

        allRooms.Clear();
        allRooms.AddRange(rooms);

        // 시작 방만 켜두고 전부 끈다.
        // 끄지 않으면 모든 방의 적 AI가 동시에 돌면서 플레이어에게 몰려온다.
        for (int i = 0; i < allRooms.Count; i++)
        {
            if (allRooms[i] == null) continue;
            allRooms[i].gameObject.SetActive(false);
        }

        Current = null;

        if (startRoom == null)
        {
            Debug.LogError("[RoomManager] 시작 방이 null");
            return;
        }

        // 시작 방 중앙에 플레이어 배치
        if (player != null)
            SetPlayerPosition(startRoom.transform.position);

        if (OnDungeonReady != null) OnDungeonReady();

        EnterRoom(startRoom);
    }

    // 문을 통과했을 때 Door가 호출.
    public void TravelThrough(Door door)
    {
        if (IsTransitioning) return;
        if (Time.time < travelUnlockTime) return;
        if (door == null || door.Linked == null) return;

        Door target = door.Linked;
        Room targetRoom = target.OwnerRoom;
        if (targetRoom == null) return;

        travelUnlockTime = Time.time + travelCooldown;

        StartCoroutine(TravelRoutine(target, targetRoom));
    }

    // 아이작식 전환: 두 방을 동시에 켜 둔 채 카메라만 옆 방으로 미끄러진다.
    // 카메라 뷰(20 x 11.25)가 방(18 x 10)보다 커서 방 하나가 화면에 딱 들어오기 때문에
    // 중심 → 중심으로 미는 것만으로 방이 통째로 넘어가는 그림이 나온다.
    private IEnumerator TravelRoutine(Door target, Room targetRoom)
    {
        IsTransitioning = true;
        SetPlayerInputLocked(true);
        StopPlayer();

        Room previous = Current;

        // 목적지 방을 먼저 켜야 EntryPoint의 월드 좌표가 유효하다.
        // 이전 방은 슬라이드가 끝날 때까지 켜 둔다 — 둘 다 보여야 "넘어가는" 그림이 된다.
        targetRoom.gameObject.SetActive(true);

        SetPlayerPosition(target.EntryPoint.position);

        if (cameraFollow != null && slideDuration > 0f)
        {
            Vector3 camFrom = cameraFollow.transform.position;

            // 경계만 갈아끼우고 스냅은 하지 않는다 (스냅하면 한 프레임 만에 도착해 버린다)
            ApplyCameraBounds(targetRoom, false);

            Vector3 camTo;
            if (!cameraFollow.TryGetTargetPosition(out camTo)) camTo = camFrom;

            cameraFollow.Suspended = true;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / slideDuration;
                // 부드럽게 출발해 부드럽게 멈추는 곡선
                cameraFollow.OverridePosition =
                    Vector3.Lerp(camFrom, camTo, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }

            cameraFollow.OverridePosition = camTo;
            cameraFollow.Suspended = false;
        }
        else
        {
            ApplyCameraBounds(targetRoom, true);
        }

        // 슬라이드가 끝난 뒤에 이전 방을 끈다
        if (previous != null && previous != targetRoom)
            previous.gameObject.SetActive(false);

        Current = targetRoom;

        if (arriveShake > 0f) CameraShake.Shake(arriveShake);

        if (OnRoomChanged != null) OnRoomChanged(targetRoom);

        // 적 스폰·문 잠금은 카메라가 도착한 뒤에 시작해야 등장 연출이 화면에 보인다
        targetRoom.OnPlayerEnter();

        yield return PushPlayerIn(target, targetRoom);

        SetPlayerInputLocked(false);
        IsTransitioning = false;
    }

    // 문에서 방 안쪽으로 잠깐 밀어 넣는다.
    // 순간이동처럼 툭 놓이지 않고 걸어 들어온 것처럼 보이며,
    // 방금 지나온 문 트리거를 곧바로 다시 밟는 사고도 줄여 준다.
    private IEnumerator PushPlayerIn(Door entryDoor, Room room)
    {
        if (player == null || pushInDuration <= 0f) yield break;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb == null) yield break;

        Vector2 dir = (Vector2)(room.transform.position - entryDoor.transform.position);
        if (dir.sqrMagnitude < 0.0001f) yield break;
        dir.Normalize();

        float t = 0f;
        while (t < pushInDuration)
        {
            t += Time.deltaTime;
            rb.velocity = dir * pushInSpeed;
            yield return null;
        }

        rb.velocity = Vector2.zero;
    }

    // 던전 입장 연출 등 외부 연출도 조작을 막아야 해서 공개해 둔다.
    public void SetPlayerInputLocked(bool locked)
    {
        if (player == null) return;

        PlayerMove move = player.GetComponent<PlayerMove>();
        if (move != null) move.inputLocked = locked;
    }

    private void StopPlayer()
    {
        if (player == null) return;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) rb.velocity = Vector2.zero;
    }

    public void EnterRoom(Room room)
    {
        if (room == null || room == Current) return;

        if (Current != null && Current != room)
            Current.gameObject.SetActive(false);

        Current = room;
        room.gameObject.SetActive(true);

        ApplyCameraBounds(room, true);
        room.OnPlayerEnter();

        if (OnRoomChanged != null) OnRoomChanged(room);
    }

    // snap=false면 경계만 바꾸고 위치는 그대로 둔다 (슬라이드 시작점을 유지하기 위해).
    private void ApplyCameraBounds(Room room, bool snap)
    {
        if (cameraFollow == null) return;

        if (room.camXMin == null || room.camXMax == null ||
            room.camYMin == null || room.camYMax == null)
        {
            Debug.LogWarning($"[RoomManager] {room.name}: 카메라 경계 Transform이 비어 있음", room);
            return;
        }

        cameraFollow.SetBounds(room.camXMin, room.camXMax, room.camYMin, room.camYMax);
        if (snap) cameraFollow.SnapToTarget();
    }

    private void SetPlayerPosition(Vector3 pos)
    {
        if (player == null) return;

        pos.z = player.position.z;
        player.position = pos;

        if (zeroVelocityOnTravel)
        {
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = Vector2.zero;
        }
    }
}
