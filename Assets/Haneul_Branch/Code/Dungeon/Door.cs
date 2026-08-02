using UnityEngine;

// 방 프리팹 안의 문 하나. 방향별로 최대 4개.
//
// 계층 구조 예시:
//   Room_Normal_01
//   └ Doors
//     └ Door_Right          ← 이 스크립트 + IsTrigger 콜라이더
//       ├ EntryPoint        ← 이 문으로 "들어왔을 때" 플레이어가 놓이는 위치 (방 안쪽)
//       ├ OpenVisual        ← 열린 문 그래픽
//       ├ ClosedVisual      ← 잠긴 문 그래픽
//       └ Blocker           ← 잠겼을 때 막는 콜라이더 (IsTrigger 끄고 Wall 레이어)
[RequireComponent(typeof(Collider2D))]
public class Door : MonoBehaviour
{
    [Header("방향 (방 기준)")]
    public Dir dir = Dir.Right;

    [Header("참조")]
    [Tooltip("이 문을 통해 들어왔을 때 플레이어가 놓일 위치. 문 트리거와 겹치지 않게 방 안쪽으로 충분히 띄울 것")]
    [SerializeField] private Transform entryPoint;
    [Tooltip("열렸을 때 켜질 오브젝트 (선택)")]
    [SerializeField] private GameObject openVisual;
    [Tooltip("전투 중 잠겼을 때 켜질 오브젝트 (선택)")]
    [SerializeField] private GameObject closedVisual;
    [Tooltip("이웃 방이 없어 아예 벽으로 막힐 때 켜질 오브젝트 (선택). 문이 아니라 벽처럼 보여야 함")]
    [SerializeField] private GameObject wallVisual;
    [Tooltip("잠겼을 때 통행을 막는 콜라이더. IsTrigger 꺼진 것 (선택)")]
    [SerializeField] private Collider2D blocker;

    // 생성기가 채워준다.
    public Room OwnerRoom { get; private set; }
    public Door Linked { get; private set; }

    public bool IsConnected => Linked != null;
    public bool IsLocked { get; private set; }
    // 이웃 방이 없어 영구히 벽이 된 문. 전투 클리어로 열리면 안 된다.
    public bool IsWalledOff { get; private set; }

    public Transform EntryPoint => entryPoint != null ? entryPoint : transform;

    private Collider2D trigger;

    private void Awake()
    {
        trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;

        OwnerRoom = GetComponentInParent<Room>();
        if (OwnerRoom == null)
            Debug.LogError($"[Door] {name}: 부모에 Room 컴포넌트가 없음", this);
    }

    // 생성기가 이웃 방의 문과 짝지어 준다.
    public void Link(Door other)
    {
        Linked = other;
    }

    // 이웃 방이 없는 방향 → 문 자체를 끄고 벽으로 남긴다.
    // 전투 중 "잠긴 문"과는 다르게 보여야 하므로 wallVisual을 따로 쓴다.
    public void DisableAsWall()
    {
        Linked = null;
        IsLocked = true;
        IsWalledOff = true;

        if (openVisual != null) openVisual.SetActive(false);
        if (closedVisual != null) closedVisual.SetActive(false);
        if (wallVisual != null) wallVisual.SetActive(true);
        if (blocker != null) blocker.enabled = true;
        if (trigger != null) trigger.enabled = false;
    }

    public void SetLocked(bool locked)
    {
        if (IsWalledOff) return;   // 벽으로 막힌 문은 전투 상태와 무관

        IsLocked = locked;

        if (openVisual != null) openVisual.SetActive(!locked);
        if (closedVisual != null) closedVisual.SetActive(locked);
        if (wallVisual != null) wallVisual.SetActive(false);
        if (blocker != null) blocker.enabled = locked;
    }

    public void Open() => SetLocked(false);
    public void Close() => SetLocked(true);

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsLocked || !IsConnected) return;
        if (!other.CompareTag("Player")) return;
        if (RoomManager.Instance == null) return;

        RoomManager.Instance.TravelThrough(this);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = IsConnected ? Color.green : Color.gray;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.6f);

        if (entryPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(entryPoint.position, 0.4f);
            Gizmos.DrawLine(transform.position, entryPoint.position);
        }
    }
}
