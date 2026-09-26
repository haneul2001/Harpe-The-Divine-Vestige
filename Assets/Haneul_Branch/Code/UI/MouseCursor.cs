using UnityEngine;

// 마우스 커서를 픽셀 십자선으로 바꾼다.
//
// 공격 방향이 포인터를 따라가게 된 뒤로 커서는 곧 조준선이다. 윈도우 기본 화살표는
// 끝이 어디를 가리키는지 애매하고, 픽셀 화면 위에서 혼자 매끈해서 뜬다.
//
// 가운데를 비운 십자라 조준점 아래 몬스터를 가리지 않는다. 그 아래 무엇이 있느냐에 따라 색이 바뀐다:
//   · 평소        회보라
//   · 적 위       핏빛, 선이 한 칸 벌어진다
//   · 처형 가능   소울 보라 + 네 귀퉁이 꺾쇠
//
// 그림은 전부 런타임에 픽셀 단위로 그린다. 에셋으로 두면 세 장을 따로 관리해야 하고,
// 색만 바꾸는 그림이라 파일로 나눠 둘 이유가 없다.
public class MouseCursor : MonoBehaviour
{
    public static MouseCursor Instance { get; private set; }

    [Header("크기")]
    [Tooltip("십자선 한 변(그림 픽셀). 홀수여야 가운데 칸이 생겨 좌우가 대칭이 된다")]
    [SerializeField] private int pixels = 15;

    [Tooltip("화면에 몇 배로 키울지. 하드웨어 커서는 32x32까지라 pixels x scale 이 그 안에 들어와야 한다")]
    [Range(1, 2)] [SerializeField] private int scale = 2;

    [Header("색")]
    [SerializeField] private Color idleColor = new Color32(0xA7, 0xA0, 0xBC, 0xFF);
    [SerializeField] private Color enemyColor = new Color32(0x98, 0x28, 0x2F, 0xFF);
    [SerializeField] private Color harvestColor = new Color32(0x8A, 0x74, 0xD8, 0xFF);
    [SerializeField] private Color outlineColor = new Color32(0x14, 0x12, 0x20, 0xF0);

    [Header("감지")]
    [Tooltip("포인터 주변 이 반경 안에 몬스터가 있으면 반응한다(월드 단위). 몸통보다 조금 넉넉하게")]
    [Min(0.05f)] [SerializeField] private float pickRadius = 0.45f;

    private enum State { Idle, Enemy, Harvest, System }

    private State shown = State.System;
    private Texture2D idleTex, enemyTex, harvestTex;
    private Vector2 hotspot;
    private Camera cam;
    private readonly Collider2D[] hits = new Collider2D[8];

    // 씬이 열릴 때마다 챙긴다. 체력바와 같은 방식 — 씬에 미리 둘 것이 없다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        RuntimeSingletons.EnsureEachScene(Spawn);
    }

    private static void Spawn()
    {
        if (Instance != null) return;
        if (FindObjectOfType<MouseCursor>() != null) return;

        // 플레이어가 없는 화면(타이틀·컷신)에서는 겨눌 것이 없으므로 기본 커서를 그대로 둔다
        if (FindObjectOfType<PlayerStatus>() == null) return;

        new GameObject("MouseCursor").AddComponent<MouseCursor>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        int size = Mathf.Max(7, pixels) | 1;
        int s = Mathf.Max(1, scale);

        idleTex = Build(size, s, idleColor, 4, false);
        enemyTex = Build(size, s, enemyColor, 5, false);     // 선이 한 칸 더 벌어진다
        harvestTex = Build(size, s, harvestColor, 5, true);  // 귀퉁이 꺾쇠까지

        hotspot = new Vector2(size * s * 0.5f, size * s * 0.5f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }

    private void Update()
    {
        Apply(Resolve());
    }

    private State Resolve()
    {
        // 창이 멈춰 있으면(특성 카드·상점 등) 시스템 커서로 돌려준다 —
        // 그때는 조준이 아니라 버튼을 누르는 중이라 화살표가 맞다.
        if (Time.timeScale <= 0f) return State.System;

        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        if (cam == null) return State.Idle;

        Vector2 world = cam.ScreenToWorldPoint(Input.mousePosition);

        // 레이어로 좁히지 않는다 — 몸통·발밑이 서로 다른 레이어에 있어서,
        // 하나만 보면 커서가 몸 위에 있는데도 반응하지 않는 자리가 생긴다.
        int n = Physics2D.OverlapCircleNonAlloc(world, pickRadius, hits);
        State best = State.Idle;

        for (int i = 0; i < n; i++)
        {
            if (hits[i] == null) continue;

            Enemy e = hits[i].GetComponentInParent<Enemy>();
            if (e == null || e.isDead) continue;

            if (e.CanHarvest) return State.Harvest;   // 제일 센 표시라 더 볼 것 없다
            best = State.Enemy;
        }
        return best;
    }

    private void Apply(State state)
    {
        if (state == shown) return;
        shown = state;

        switch (state)
        {
            case State.Enemy: Cursor.SetCursor(enemyTex, hotspot, CursorMode.Auto); break;
            case State.Harvest: Cursor.SetCursor(harvestTex, hotspot, CursorMode.Auto); break;
            case State.System: Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); break;
            default: Cursor.SetCursor(idleTex, hotspot, CursorMode.Auto); break;
        }
    }

    // ─────────────────────────────────────────────
    // 그리기
    // ─────────────────────────────────────────────

    // 가운데가 빈 십자 + 중심 점. armGap이 클수록 선이 중심에서 멀어진다.
    private Texture2D Build(int size, int scale, Color color, int armGap, bool brackets)
    {
        bool[] lit = new bool[size * size];
        int c = size / 2;

        // 팔 네 개 — 바깥 끝에서 시작해 중심 앞 armGap 칸에서 끊는다
        for (int d = armGap; d <= c; d++)
        {
            lit[c * size + (c - d)] = true;
            lit[c * size + (c + d)] = true;
            lit[(c - d) * size + c] = true;
            lit[(c + d) * size + c] = true;
        }

        lit[c * size + c] = true;   // 중심 점 — 정확히 어디를 가리키는지 보여 준다

        if (brackets)
        {
            // 네 귀퉁이 꺾쇠. 처형 가능한 상대를 조준했다는 표시다
            int[] xs = { 0, size - 1 };
            int[] ys = { 0, size - 1 };
            for (int xi = 0; xi < 2; xi++)
            for (int yi = 0; yi < 2; yi++)
            {
                int x = xs[xi], y = ys[yi];
                int dx = xi == 0 ? 1 : -1;
                int dy = yi == 0 ? 1 : -1;
                lit[y * size + x] = true;
                lit[y * size + (x + dx)] = true;
                lit[(y + dy) * size + x] = true;
            }
        }

        var colors = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int i = y * size + x;
            if (lit[i]) { colors[i] = color; continue; }

            // 어두운 바닥에서도 밝은 바닥에서도 보이게 한 겹 두른다
            bool near = false;
            for (int oy = -1; oy <= 1 && !near; oy++)
            for (int ox = -1; ox <= 1 && !near; ox++)
            {
                int nx = x + ox, ny = y + oy;
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                near = lit[ny * size + nx];
            }
            colors[i] = near ? outlineColor : Color.clear;
        }

        return Upscale(colors, size, Mathf.Max(1, scale));
    }

    // 정수 배로만 키운다 — 픽셀 그림이라 보간이 끼면 가장자리가 뭉갠다
    private static Texture2D Upscale(Color[] src, int size, int scale)
    {
        int w = size * scale;
        var tex = new Texture2D(w, w, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        var big = new Color[w * w];
        for (int y = 0; y < w; y++)
        for (int x = 0; x < w; x++)
            big[y * w + x] = src[(y / scale) * size + (x / scale)];

        tex.SetPixels(big);
        tex.Apply();
        return tex;
    }
}
