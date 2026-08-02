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

    public Room Current { get; private set; }

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
        if (Time.time < travelUnlockTime) return;
        if (door == null || door.Linked == null) return;

        Door target = door.Linked;
        Room targetRoom = target.OwnerRoom;
        if (targetRoom == null) return;

        travelUnlockTime = Time.time + travelCooldown;

        // 목적지 방을 먼저 켜야 EntryPoint의 월드 좌표가 유효하다
        targetRoom.gameObject.SetActive(true);

        SetPlayerPosition(target.EntryPoint.position);
        EnterRoom(targetRoom);
    }

    public void EnterRoom(Room room)
    {
        if (room == null || room == Current) return;

        if (Current != null && Current != room)
            Current.gameObject.SetActive(false);

        Current = room;
        room.gameObject.SetActive(true);

        ApplyCameraBounds(room);
        room.OnPlayerEnter();

        if (OnRoomChanged != null) OnRoomChanged(room);
    }

    private void ApplyCameraBounds(Room room)
    {
        if (cameraFollow == null) return;

        if (room.camXMin == null || room.camXMax == null ||
            room.camYMin == null || room.camYMax == null)
        {
            Debug.LogWarning($"[RoomManager] {room.name}: 카메라 경계 Transform이 비어 있음", room);
            return;
        }

        cameraFollow.SetBounds(room.camXMin, room.camXMax, room.camYMin, room.camYMax);
        cameraFollow.SnapToTarget();
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
