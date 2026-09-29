using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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

    [Tooltip("격자 여러 칸을 차지하는 큰 방에서, 이 문이 몇 번째 칸에 붙어 있는지.\n"
           + "위·아래 문은 왼쪽 칸부터 0, 1, …  왼·오른쪽 문은 아래 칸부터 0, 1, …  한 칸 방은 늘 0")]
    [Min(0)] public int subCell = 0;

    [Header("참조")]
    [Tooltip("이 문을 통해 들어왔을 때 플레이어가 놓일 위치. 문 트리거와 겹치지 않게 방 안쪽으로 충분히 띄울 것")]
    [SerializeField] private Transform entryPoint;
    [Tooltip("열렸을 때 켜질 오브젝트 (선택)")]
    [SerializeField] private GameObject openVisual;
    [Tooltip("전투 중 잠겼을 때 켜질 오브젝트 (선택)")]
    [SerializeField] private GameObject closedVisual;
    [Tooltip("이웃 방이 없어 아예 벽으로 막힐 때 켜질 오브젝트 (선택). 문이 아니라 벽처럼 보여야 함")]
    [SerializeField] private GameObject wallVisual;
    // 이웃이 없어 벽이 될 때 바꿔 끼울 타일 (임포터가 채운다). 있으면 벽 그림(wallVisual) 대신 이것을 쓴다 —
    // 주변 벽과 같은 타일이라 그림을 덮을 때처럼 이음새·밝은 띠가 생기지 않는다
    [System.Serializable]
    public class TilePatch
    {
        public Tilemap map;
        public Vector3Int pos;
        public TileBase tile;            // null이면 지운다
        public Matrix4x4 matrix = Matrix4x4.identity;
    }
    [SerializeField, HideInInspector] private List<TilePatch> wallPatch = new List<TilePatch>();

    public void EditorSetWallPatch(List<TilePatch> patch) { wallPatch = patch ?? new List<TilePatch>(); }

    [Tooltip("잠겼을 때 통행을 막는 콜라이더. IsTrigger 꺼진 것 (선택)")]
    [SerializeField] private Collider2D blocker;
    [Tooltip("위쪽 문: 위쪽 벽의 보이는 밑선(바닥이 시작하는 선)의 문 기준 y. 임포터가 테마별로 채운다")]
    public float wallBottom = -2.5f;
    [Tooltip("게이트 뒤에 벽 그림(wallVisual)을 항상 깔아 둔다. 보스 입구처럼 4칸 구멍 위에 3칸짜리 문을 얹을 때")]
    [SerializeField] private bool wallBehindGate = false;

    // 생성기가 채워준다.
    public Room OwnerRoom { get; private set; }
    public Door Linked { get; private set; }

    public bool IsConnected => Linked != null;
    public bool IsLocked { get; private set; }
    // 이웃 방이 없어 영구히 벽이 된 문. 전투 클리어로 열리면 안 된다.
    public bool IsWalledOff { get; private set; }

    public Transform EntryPoint => entryPoint != null ? entryPoint : transform;

    private Collider2D trigger;
    private DoorGateVisual gate;
    // 방이 꺼진 채 Link/Open 되는 경우가 있어(Awake 전) 필요할 때 찾는다
    private DoorGateVisual Gate
    {
        get
        {
            if (gate == null && closedVisual != null) gate = closedVisual.GetComponent<DoorGateVisual>();
            return gate;
        }
    }

    private void Awake()
    {
        trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;

        OwnerRoom = GetComponentInParent<Room>();
        if (OwnerRoom == null)
            Debug.LogError($"[Door] {name}: 부모에 Room 컴포넌트가 없음", this);

        SortGateWithCharacters();
    }

    // 위·아래 문의 철창은 캐릭터와 같은 층에서 밑단(피벗) 기준으로 앞뒤를 가린다.
    // 벽 층에 두면 캐릭터가 늘 위에 그려져, 아래 문 철창 뒤(위쪽)에 선 플레이어가 철창을 뚫고 보였다.
    // 옆문 철창은 피벗이 그림 가운데라 이 방식이 맞지 않아 그대로 둔다. 보스 입구 문도 제 설정을 쓴다.
    private void SortGateWithCharacters()
    {
        if (wallBehindGate || (dir != Dir.Up && dir != Dir.Down)) return;
        DoorGateVisual g = Gate;
        if (g == null) return;
        var sr = g.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        sr.sortingLayerName = CharacterSorting.Layer;
        sr.sortingOrder = CharacterSorting.Order;
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
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
        if (Gate != null) Gate.Hide();
        else if (closedVisual != null) closedVisual.SetActive(false);

        if (wallPatch != null && wallPatch.Count > 0)
        {
            foreach (TilePatch p in wallPatch)
            {
                if (p == null || p.map == null) continue;
                p.map.SetTile(p.pos, p.tile);
                if (p.tile != null && p.matrix != Matrix4x4.identity) p.map.SetTransformMatrix(p.pos, p.matrix);
            }
            if (wallVisual != null) wallVisual.SetActive(false);
        }
        else if (wallVisual != null) wallVisual.SetActive(true);
        if (blocker != null) blocker.enabled = true;
        if (trigger != null) trigger.enabled = false;
    }

    public void SetLocked(bool locked)
    {
        if (IsWalledOff) return;   // 벽으로 막힌 문은 전투 상태와 무관

        bool wasLocked = IsLocked;
        IsLocked = locked;

        if (openVisual != null) openVisual.SetActive(!locked);
        if (wallVisual != null) wallVisual.SetActive(wallBehindGate);

        DoorGateVisual g = Gate;
        if (g == null)
        {
            if (closedVisual != null) closedVisual.SetActive(locked);
            if (blocker != null) blocker.enabled = locked;
            return;
        }

        StopAllCoroutines();
        if (locked)
        {
            g.Lock();
            if (blocker != null) blocker.enabled = true;
        }
        else
        {
            // 처음 잇는 순간(잠긴 적 없음)이나 방이 꺼져 있을 땐 연출 없이 열린 상태로
            float t = g.Unlock(wasLocked && isActiveAndEnabled);
            if (blocker == null) return;
            if (t > 0f && isActiveAndEnabled) StartCoroutine(UnblockAfter(t));
            else blocker.enabled = false;
        }
    }

    // 게이트가 다 내려간 뒤에 통행을 튼다
    private System.Collections.IEnumerator UnblockAfter(float t)
    {
        yield return new WaitForSeconds(t);
        if (!IsLocked && blocker != null) blocker.enabled = false;
    }

    // 생성기가 특별한 문(보스 입구 감옥 문)을 끼워 넣을 때. 프리팹의 자체 위치를 그대로 쓴다
    public void SetGate(GameObject gatePrefab, bool wallBehind)
    {
        if (gatePrefab == null) return;
        if (closedVisual != null) Destroy(closedVisual);
        GameObject go = Instantiate(gatePrefab, transform);
        go.name = "ClosedVisual";
        // 끼울 문 프리팹은 벽 밑선 -2.5 기준으로 만들어 두었다 — 이 방의 실제 밑선만큼 내린다
        if (dir == Dir.Up)
        {
            go.transform.localPosition += new Vector3(0f, wallBottom + 2.5f, 0f);
            // 양옆 막이: 벽 윗변(+0.5)부터 밑선까지
            foreach (BoxCollider2D bc in go.GetComponentsInChildren<BoxCollider2D>(true))
            {
                float h = 0.5f - wallBottom;
                bc.size = new Vector2(bc.size.x, h);
                Vector3 lp = bc.transform.localPosition;
                bc.transform.localPosition = new Vector3(lp.x, (0.5f + wallBottom) * 0.5f - go.transform.localPosition.y, lp.z);
                bc.offset = Vector2.zero;
            }
        }
        // 정렬은 벽 그림(plug)과 같은 레이어, 그보다 앞
        SpriteRenderer wallSr = wallVisual != null ? wallVisual.GetComponent<SpriteRenderer>() : null;
        if (wallSr != null)
            foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingLayerID = wallSr.sortingLayerID;
                sr.sortingOrder = wallSr.sortingOrder + 2;
            }
        closedVisual = go;
        gate = go.GetComponent<DoorGateVisual>();
        wallBehindGate = wallBehind;
        // 문 뒤에 까는 벽 그림은 그림만 — 그 충돌은 구멍 전체를 막는다. 통로 폭은 끼운 문의 양옆 막이가 정한다
        if (wallBehind && wallVisual != null)
            foreach (Collider2D c in wallVisual.GetComponents<Collider2D>()) c.enabled = false;

        // 벽을 깔고 문을 얹은 입구는 벽 그림이 구멍을 덮고 있어, 입장 판정이 벽 윗변에 있으면
        // 벽 속으로 끝까지 파고들어야 들어가진다. 판정을 문 밑단(바닥이 시작하는 선)으로 내린다.
        if (wallBehind && dir == Dir.Up)
        {
            var box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.size = new Vector2(box.size.x, 0.8f);
                box.offset = new Vector2(box.offset.x, wallBottom + 0.3f);
            }
        }
    }

    public void Open() => SetLocked(false);

    // 처음부터 잠긴 채로 둔다 — 닫히는 연출 없이. 방을 클리어하면 Room이 SetLocked(false)로 연다 (여는 연출은 튼다)
    public void LockSilently()
    {
        if (IsWalledOff) return;
        IsLocked = true;
        StopAllCoroutines();
        if (openVisual != null) openVisual.SetActive(false);
        if (wallVisual != null) wallVisual.SetActive(wallBehindGate);
        if (Gate != null) Gate.ShowClosed();
        else if (closedVisual != null) closedVisual.SetActive(true);
        if (blocker != null) blocker.enabled = true;
    }
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
