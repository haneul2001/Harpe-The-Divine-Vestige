using UnityEngine;

// 보스가 지금 무슨 패턴을 쓰는지 머리 위에 글자로 띄운다.
// DebugBoxManager가 꺼져 있으면(F2) 보이지 않는다 — 다른 디버그 표시와 같은 규칙이다.
//
// 이름이 뜨는 시점은 "예고가 시작될 때"다. 빨간 표시와 글자가 같이 떠야
// "저 표시가 이 패턴의 것"이라고 읽을 수 있다.
//
// 글자는 TextMesh로 그린다. 캔버스를 쓰면 보스를 따라다니게 하려고
// 좌표 변환을 매 프레임 돌려야 하는데, 디버그 한 줄에 치를 값은 아니다.
[RequireComponent(typeof(BossEnemy))]
public class BossPatternDebugLabel : DebugVisual
{
    [Tooltip("보스 발밑에서 이만큼 위에 띄운다 (월드 단위)")]
    [SerializeField] private float height = 3.6f;

    [Tooltip("한 번 뜬 이름이 남아 있는 시간(초). 다음 공격이 오면 바로 바뀐다")]
    [Min(0.5f)] [SerializeField] private float holdSeconds = 2.5f;

    [Tooltip("글자 크기. 픽셀 폰트라 너무 키우면 뭉개진다")]
    [Min(0.02f)] [SerializeField] private float characterSize = 0.1f;

    [SerializeField] private Color color = new Color(1f, 0.85f, 0.35f);

    [Tooltip("페이즈와 공격 번호도 같이 보여 준다")]
    [SerializeField] private bool showDetail = true;

    private BossEnemy boss;
    private TextMesh text;
    private MeshRenderer textRenderer;
    private Transform textTransform;
    private float shownAt = -999f;

    private void Awake()
    {
        boss = GetComponent<BossEnemy>();
        Build();
    }

    protected override void OnEnable()
    {
        base.OnEnable();                    // DebugBoxManager 구독 + 현재 상태 반영
        if (boss != null) boss.PatternChosen += OnPatternChosen;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (boss != null) boss.PatternChosen -= OnPatternChosen;
    }

    private void Build()
    {
        var go = new GameObject("DebugPatternLabel");
        go.transform.SetParent(transform, false);
        go.AddComponent<DebugVisualPart>();   // 피격 깜빡임 같은 연출이 건드리지 않게

        textTransform = go.transform;

        text = go.AddComponent<TextMesh>();
        // 위에서 아래로 자란다 — 화면 위쪽에 붙어도 첫 줄이 먼저 잘리지 않는다
        text.anchor = TextAnchor.UpperCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 48;
        text.characterSize = characterSize;
        text.color = color;
        text.text = "";

        // 한글이 나오는 폰트라야 한다. 기본 폰트는 한글 글리프가 없어 네모로 나온다
        Font font = Resources.Load<Font>("Galmuri11");
        if (font != null)
        {
            text.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        textRenderer = go.GetComponent<MeshRenderer>();
        textRenderer.sortingLayerName = "Skill";
        textRenderer.sortingOrder = 210;      // 디버그 박스(200)보다 위
        textRenderer.enabled = false;
    }

    protected override void Apply(bool visible)
    {
        // 매니저가 꺼지면 즉시 숨긴다. 켜질 때는 다음 공격부터 다시 뜬다
        if (textRenderer != null) textRenderer.enabled = visible && Fresh;
    }

    private bool Fresh { get { return Time.time - shownAt <= holdSeconds; } }

    // 보스는 덩치가 커서 머리 위가 곧잘 화면 밖이다. 읽으라고 띄운 글자가
    // 화면 밖에 있으면 없는 것과 같으므로, 가장자리에 붙여서라도 보이게 한다.
    private Vector3 ClampToView(Vector3 world)
    {
        Camera cam = Camera.main;
        if (cam == null) return world;

        Vector3 vp = cam.WorldToViewportPoint(world);
        if (vp.z <= 0f) return world;

        vp.x = Mathf.Clamp(vp.x, 0.10f, 0.90f);
        vp.y = Mathf.Clamp(vp.y, 0.12f, 0.94f);

        Vector3 back = cam.ViewportToWorldPoint(vp);
        back.z = world.z;
        return back;
    }

    private void OnPatternChosen(BossPattern pattern)
    {
        if (text == null) return;

        // 아무것도 안 고른 공격은 이름표를 지운다 — 직전 이름이 남으면 엉뚱한 패턴을 의심하게 된다
        if (pattern == null)
        {
            shownAt = -999f;
            if (textRenderer != null) textRenderer.enabled = false;
            return;
        }

        string line = pattern.Name;
        if (showDetail)
            line += "\n<" + (boss.PhaseIndex + 1) + "페이즈 · 공격" + pattern.AnimationIndex + ">";

        text.text = line;
        shownAt = Time.time;

        if (textRenderer != null) textRenderer.enabled = DebugBoxManager.Visible;
    }

    private void LateUpdate()
    {
        if (textRenderer == null) return;

        if (textRenderer.enabled && !Fresh) { textRenderer.enabled = false; return; }
        if (!textRenderer.enabled) return;

        // 보스는 4배로 스폰되고 좌우로 뒤집히기도 한다. 글자는 그걸 따라가면 안 된다
        textTransform.position = ClampToView(transform.position + Vector3.up * height);
        textTransform.rotation = Quaternion.identity;

        Vector3 parent = transform.lossyScale;
        textTransform.localScale = new Vector3(
            1f / Mathf.Max(0.0001f, Mathf.Abs(parent.x)),
            1f / Mathf.Max(0.0001f, Mathf.Abs(parent.y)),
            1f);

        // 끝날 때쯤 옅어지게 — 갑자기 사라지면 마지막 패턴이 뭐였는지 놓친다
        float left = holdSeconds - (Time.time - shownAt);
        float alpha = Mathf.Clamp01(left / 0.6f);
        text.color = new Color(color.r, color.g, color.b, color.a * alpha);
    }
}
