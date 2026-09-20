using UnityEngine;

// 8방향 조준.
//
// 공격 방향은 마지막으로 누른 이동 방향(8방향으로 반올림)이다. 공격하는 동안에는 고정된다 —
// 휘두르는 도중에 방향키를 바꿔도 이미 나간 베기가 따라 돌면 어색하다.
//
// 발밑에 반투명 원을 깔고, 원 테두리 위에 공격 방향을 가리키는 뾰족한 표시를 둔다.
// 그림은 전부 런타임에 픽셀 단위로 직접 그린다 (8방향마다 따로 래스터라이즈해서
// 대각선도 계단이 깨지지 않는다).
public class PlayerAim : MonoBehaviour
{
    public static readonly Vector2[] Directions8 =
    {
        new Vector2(1f, 0f), new Vector2(1f, 1f).normalized, new Vector2(0f, 1f), new Vector2(-1f, 1f).normalized,
        new Vector2(-1f, 0f), new Vector2(-1f, -1f).normalized, new Vector2(0f, -1f), new Vector2(1f, -1f).normalized,
    };

    [Header("발밑 원")]
    [SerializeField] private bool showIndicator = true;
    [Tooltip("원 지름 (월드 단위)")]
    [SerializeField] private float ringDiameter = 0.8f;
    [Tooltip("원을 위아래로 눌러 바닥에 누운 것처럼 보이게 한다. 이동의 세로 보정(0.7)과 맞췄다")]
    [SerializeField] private float ringYScale = 0.7f;
    [SerializeField] private Color ringColor = new Color(0.85f, 0.9f, 1f, 0.35f);
    [Tooltip("원 중심의 발 기준 위치 (오른쪽을 볼 때 기준 — 왼쪽을 보면 x가 자동으로 뒤집힌다).\n" +
             "Little Reaper는 낫이 한쪽에 달려 있어 몸통 중심이 스프라이트 피벗보다 4.5px(≈0.14) 옆에 있다")]
    [SerializeField] private Vector2 ringOffset = new Vector2(0.14f, 0f);
    [Tooltip("방향 표시 화살촉 크기(픽셀). 원이 작아지면 같이 줄인다")]
    [SerializeField] private int pointerPixels = 11;

    [Header("방향 표시")]
    [SerializeField] private Color pointerColor = new Color(1f, 0.93f, 0.7f, 0.95f);
    [SerializeField] private Color pointerOutline = new Color(0.12f, 0.1f, 0.14f, 0.9f);
    [Tooltip("표시가 방향을 바꿀 때 돌아가는 시간(초)")]
    [SerializeField] private float turnTime = 0.06f;

    [Header("그리기")]
    [Tooltip("그림 1픽셀이 월드에서 차지하는 크기 = 1/이 값. 프로젝트 기준 유효 PPU 32")]
    [SerializeField] private float pixelsPerUnit = 32f;
    [SerializeField] private int sortingOrderOffset = -5;

    public Vector2 Direction => Directions8[index];
    public int DirectionIndex => index;
    public float Angle => index * 45f;

    private int index;           // 0=→, 1=↗, 2=↑ … 반시계
    private PlayerCombat combat;
    private PlayerMove move;
    private SpriteRenderer body;

    private Transform root;
    private SpriteRenderer ringRenderer;
    private SpriteRenderer pointerRenderer;
    private Sprite[] pointerSprites;
    private float shownAngle;
    private float turnVelocity;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        move = GetComponent<PlayerMove>();
        body = GetComponentInChildren<SpriteRenderer>();

        index = body != null && body.flipX ? 4 : 0;
        shownAngle = Angle;

        if (showIndicator) BuildIndicator();
    }

    private void Update()
    {
        bool locked = (combat != null && (combat.isAttacking || combat.isCharging))
                      || (move != null && (move.isExecuting || move.inputLocked));

        if (!locked) RefreshFromInput();

        UpdateIndicator();
    }

    // 지금 누르고 있는 방향키로 조준을 다시 잡는다. 안 누르고 있으면 방향을 유지한다.
    //
    // 콤보 중에는 한 타가 끝나는 순간 다음 타가 바로 시작돼 isAttacking이 한 프레임도 안 풀린다.
    // 그래서 Update의 잠금 해제만으로는 콤보 내내 첫 타 방향에 묶인다 — 새 타가 나갈 때 여기서 다시 잡는다.
    public void RefreshFromInput()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude <= 0.01f) return;

        float a = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
        index = ((Mathf.RoundToInt(a / 45f) % 8) + 8) % 8;
    }

    // 공격이 시작될 때 몸도 그 방향으로 돌린다 (위·아래는 좌우를 그대로 둔다)
    public void FaceBody()
    {
        if (body == null) return;
        Vector2 d = Direction;
        if (Mathf.Abs(d.x) > 0.01f) body.flipX = d.x < 0f;
    }

    // ─────────────────────────────────────────────
    // 표시
    // ─────────────────────────────────────────────

    private void BuildIndicator()
    {
        var go = new GameObject("AimIndicator");
        root = go.transform;
        root.SetParent(transform, false);

        int order = (body != null ? body.sortingOrder : 0) + sortingOrderOffset;
        string layer = body != null ? body.sortingLayerName : "Default";

        int ringPx = Mathf.Max(8, Mathf.RoundToInt(ringDiameter * pixelsPerUnit));
        ringRenderer = MakeChild("Ring", RingSprite(ringPx, Mathf.Max(4, Mathf.RoundToInt(ringPx * ringYScale))), layer, order);
        ringRenderer.color = ringColor;

        pointerSprites = new Sprite[8];
        for (int i = 0; i < 8; i++) pointerSprites[i] = PointerSprite(i * 45f);
        pointerRenderer = MakeChild("Pointer", pointerSprites[0], layer, order + 1);
        pointerRenderer.color = Color.white;
    }

    private SpriteRenderer MakeChild(string name, Sprite sprite, string layer, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        return sr;
    }

    private void UpdateIndicator()
    {
        if (root == null) return;

        // 플레이어가 축소돼 있어도(0.64배) 표시는 월드 기준 크기로
        Vector3 s = transform.lossyScale;
        root.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(s.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(s.y)), 1f);
        // 몸통이 피벗에서 비켜 있으므로 좌우 반전에 맞춰 오프셋도 뒤집는다
        float side = body != null && body.flipX ? -1f : 1f;
        root.position = transform.position + new Vector3(ringOffset.x * side, ringOffset.y, 0f);

        bool visible = move == null || !move.isExecuting;
        ringRenderer.enabled = visible;
        pointerRenderer.enabled = visible;

        // 표시 위치는 부드럽게 돌리고, 그림은 가장 가까운 8방향 것을 쓴다
        shownAngle = Mathf.SmoothDampAngle(shownAngle, Angle, ref turnVelocity, turnTime, Mathf.Infinity, Time.unscaledDeltaTime);
        float rad = shownAngle * Mathf.Deg2Rad;
        float r = ringDiameter * 0.5f + 2f / pixelsPerUnit;
        pointerRenderer.transform.localPosition = new Vector3(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r * ringYScale, 0f);

        int nearest = ((Mathf.RoundToInt(shownAngle / 45f) % 8) + 8) % 8;
        pointerRenderer.sprite = pointerSprites[nearest];
    }

    // 2px 두께 타원 테두리 + 아주 옅은 안쪽 — 테두리만 있으면 바닥 무늬에 묻힌다
    private Sprite RingSprite(int w, int h)
    {
        var tex = NewTexture(w, h);
        float rx = w * 0.5f, ry = h * 0.5f;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = (x + 0.5f - rx) / rx, dy = (y + 0.5f - ry) / ry;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float edge = 2f / Mathf.Min(rx, ry);   // 테두리 두께 2px
            if (d <= 1f && d >= 1f - edge) px[y * w + x] = Color.white;
            else if (d < 1f - edge) px[y * w + x] = new Color(1f, 1f, 1f, 0.18f);
            else px[y * w + x] = Color.clear;
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }

    // 뾰족한 화살촉(가운데가 파인 쐐기)을 해당 각도로 돌려 픽셀로 채운다. 한 칸 테두리는 어두운 색.
    private Sprite PointerSprite(float angleDeg)
    {
        int size = Mathf.Max(7, pointerPixels) | 1;   // 홀수 — 가운데 픽셀이 있어야 좌우 대칭이 맞는다
        var tex = NewTexture(size, size);
        float c = size * 0.5f;
        float rad = angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);

        // +X를 향한 화살촉 (중심 기준, 17px 기준으로 잡은 모양을 크기에 맞춰 줄인다)
        float k = size / 17f;
        Vector2[] poly = { new Vector2(7f, 0f) * k, new Vector2(-5f, 5.5f) * k, new Vector2(-2f, 0f) * k, new Vector2(-5f, -5.5f) * k };

        bool[] fill = new bool[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // 픽셀 중심을 반대로 돌려 원래 모양 좌표에서 안에 드는지 본다
            float px = x + 0.5f - c, py = y + 0.5f - c;
            Vector2 p = new Vector2(px * cos + py * sin, -px * sin + py * cos);
            fill[y * size + x] = Inside(poly, p);
        }

        var colors = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int i = y * size + x;
            if (fill[i]) { colors[i] = pointerColor; continue; }
            bool nearFill = false;
            for (int oy = -1; oy <= 1 && !nearFill; oy++)
            for (int ox = -1; ox <= 1 && !nearFill; ox++)
            {
                if (Mathf.Abs(ox) + Mathf.Abs(oy) != 1) continue;
                int nx = x + ox, ny = y + oy;
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                nearFill = fill[ny * size + nx];
            }
            colors[i] = nearFill ? pointerOutline : Color.clear;
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }

    private static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    private static Texture2D NewTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }
}
