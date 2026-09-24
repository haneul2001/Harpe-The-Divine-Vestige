using UnityEngine;

// 몬스터가 떨어뜨리는 골드 동전.
//
//  · 떨어진 자리에서 살짝 튀어 흩어진다
//  · 1초 동안은 못 줍는다 (죽자마자 빨려 들어가면 떨어진 게 안 보인다)
//  · 그 뒤로는 가까이 가면 줍고, 방을 다 치우면 남은 동전이 알아서 날아온다
//
// 그림은 RafaelMatos 팩의 동전을 금색으로 다시 칠한 것이고, 회전은 이 스크립트가 직접 돌린다
// (프레임 12장짜리 단순 반복이라 Animator를 따로 두지 않는다).
public class GoldCoin : MonoBehaviour
{
    [Tooltip("이 동전의 값어치")]
    public int value = 1;

    [Tooltip("줍기까지의 시간(초) — 떨어지는 게 보이도록 잠깐 막아 둔다")]
    public float pickupDelay = 1f;

    [Tooltip("걸어가서 주울 수 있는 거리")]
    public float pickupRadius = 0.6f;

    [Tooltip("빨려 들어갈 때의 속도(초당 월드 단위). 가까워질수록 빨라진다")]
    public float magnetSpeed = 9f;

    [Tooltip("흩어지는 거리")]
    public float scatter = 0.55f;

    [Header("회전 애니메이션")]
    public Sprite[] frames = new Sprite[0];
    [Min(1f)] public float fps = 12f;

    private Transform player;
    private SpriteRenderer renderer2D;
    private float bornAt;
    private bool magnet;          // 방을 다 치워서 날아오는 중
    private bool collected;

    private Vector3 from, to;     // 튀어 흩어지는 구간
    private float hopTime = 0.35f;

    public static GoldCoin Spawn(GameObject prefab, Vector3 position, int value)
    {
        if (prefab == null) return null;

        GameObject go = Instantiate(prefab, position, Quaternion.identity);
        GoldCoin coin = go.GetComponent<GoldCoin>();
        if (coin == null) coin = go.AddComponent<GoldCoin>();
        coin.value = Mathf.Max(1, value);
        return coin;
    }

    private void OnEnable()
    {
        Room.AnyCleared += OnRoomCleared;
    }

    private void OnDisable()
    {
        Room.AnyCleared -= OnRoomCleared;
    }

    private void Start()
    {
        bornAt = Time.time;
        renderer2D = GetComponentInChildren<SpriteRenderer>();

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        // 같은 자리에 여러 개가 겹치지 않게 사방으로 흩는다
        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir == Vector2.zero) dir = Vector2.up;

        from = transform.position;
        to = from + (Vector3)(dir * Random.Range(scatter * 0.4f, scatter));

        // 방이 이미 조용한 곳(상자방 등)에 떨어졌다면 굳이 기다릴 필요가 없다
        Room room = GetRoomAt(from);
        if (room != null && room.IsCleared) magnet = true;
    }

    private void Update()
    {
        float age = Time.time - bornAt;

        // 계속 도는 동전
        if (frames.Length > 0 && renderer2D != null)
        {
            int f = Mathf.FloorToInt(age * fps) % frames.Length;
            renderer2D.sprite = frames[f];
        }

        // ① 튀어 나가는 연출 — 짧은 포물선
        if (age < hopTime)
        {
            float k = age / hopTime;
            Vector3 flat = Vector3.Lerp(from, to, k);
            transform.position = flat + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.25f;
            return;
        }

        if (collected || player == null) return;
        if (age < pickupDelay) return;   // 아직 줍는 게 막혀 있다

        // ② 방을 다 치웠으면 알아서 날아간다
        if (magnet)
        {
            float step = magnetSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, player.position, step);
            if (Vector3.Distance(transform.position, player.position) <= 0.25f) Collect();
            return;
        }

        // ③ 직접 걸어와서 줍기
        if (Vector3.Distance(transform.position, player.position) <= pickupRadius) Collect();
    }

    private void OnRoomCleared(Room room)
    {
        // 다른 방을 치운 것까지 따라 날아오면 벽을 뚫고 온다
        if (room == null || room != GetRoomAt(transform.position)) return;
        magnet = true;
    }

    // 좌표가 어느 방 안인지. 방은 한 씬에 격자로 놓여 있다.
    private static Room GetRoomAt(Vector3 world)
    {
        Room best = null;
        float bestDist = float.MaxValue;

        foreach (Room r in FindObjectsOfType<Room>())
        {
            float d = Vector2.Distance(r.transform.position, world);
            if (d < bestDist) { bestDist = d; best = r; }
        }
        return best;
    }

    private void Collect()
    {
        if (collected) return;
        collected = true;

        PlayerStatus status = player != null ? player.GetComponent<PlayerStatus>() : null;
        if (status == null && player != null) status = player.GetComponentInParent<PlayerStatus>();
        if (status != null) status.AddGold(value);

        Destroy(gameObject);
    }
}
