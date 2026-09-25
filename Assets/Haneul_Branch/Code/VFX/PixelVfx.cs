using UnityEngine;

// 이펙트 하나를 재생하는 오브젝트. 프레임을 직접 넘겨 그리므로 애니메이터가 필요 없다.
//
//   PixelVfx.Play("EnemyHit", pos);                 // 한 번 재생하고 스스로 사라진다
//   var v = PixelVfx.Play("BatShot", pos, ang, tr); // 루프 이펙트 — 들고 있다가 직접 없앤다
//
// 정의는 Resources/VFX/VfxLibrary 에 모여 있다. 새 이펙트를 추가할 때 이 파일은 안 건드린다.
[RequireComponent(typeof(SpriteRenderer))]
public class PixelVfx : MonoBehaviour
{
    private static VfxLibrary library;
    private static bool libraryMissingLogged;

    private SpriteRenderer sr;
    private VfxClip clip;
    private float t;
    private bool playing;

    public bool IsPlaying { get { return playing; } }

    // 이펙트를 하나 띄운다. 정의를 못 찾으면 아무것도 안 하고 null을 돌려준다.
    public static PixelVfx Play(string id, Vector3 position, float rotationZ = 0f, Transform parent = null)
    {
        return Spawn(id, position, rotationZ, parent, Vector2.zero);
    }

    // 정해진 넓이를 꽉 채우도록 늘려 띄운다. 예고한 위험지역만큼 공격 그림을 덮을 때 쓴다.
    // 라이브러리의 기본 배율은 무시한다 — 여기서는 "얼마나 크게"가 아니라 "어디까지"가 기준이다.
    public static PixelVfx PlayStretched(string id, Vector3 center, float rotationZ, Vector2 size)
    {
        return Spawn(id, center, rotationZ, null, size);
    }

    private static PixelVfx Spawn(string id, Vector3 position, float rotationZ, Transform parent, Vector2 stretch)
    {
        bool stretched = stretch.x > 0.01f && stretch.y > 0.01f;
        VfxClip c = Lookup(id);
        if (c == null) return null;
        if (c.prefab == null && (c.frames == null || c.frames.Length == 0)) return null;

        var go = new GameObject("VFX_" + id);
        go.transform.SetParent(parent, false);
        // 부모에 붙여도 월드 좌표는 지정한 자리에 오게 한다
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);

        // 부모가 커져 있으면(적은 3배로 스폰된다) 이펙트까지 같이 커진다.
        // 화면에 보이는 크기가 정의값 그대로가 되도록 부모 배율을 나눠 준다.
        float s = Mathf.Max(0.01f, c.scale);
        Vector3 pl = parent != null ? parent.lossyScale : Vector3.one;
        go.transform.localScale = stretched ? Vector3.one : new Vector3(
            s / Mathf.Max(0.0001f, Mathf.Abs(pl.x)),
            s / Mathf.Max(0.0001f, Mathf.Abs(pl.y)),
            1f);

        var vfx = go.AddComponent<PixelVfx>();
        vfx.Begin(c, rotationZ, stretched);

        // 늘리는 건 그림이 준비된 뒤다 — 기준 크기를 실제 그려질 물건에서 재기 때문이다
        if (stretched)
        {
            float span = vfx.DrawnSpan(c);
            go.transform.localScale = new Vector3(stretch.x / span, stretch.y / span, 1f);
        }
        return vfx;
    }

    // 배율 1일 때 이 이펙트가 그려지는 크기(월드 단위).
    // 늘려 그리려면 기준을 알아야 한다 — 모르고 곱하면 패턴마다 길이가 제각각이 된다.
    private float DrawnSpan(VfxClip c)
    {
        if (c.prefab == null)
            return c.frames != null && c.frames.Length > 0
                ? Mathf.Max(0.01f, c.frames[0].bounds.size.x) : 1f;

        // 쿼드로 바꿔 둔 파티클은 시작 크기가 곧 변의 길이다
        float span = 0f;
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r == null || r.renderMode == ParticleSystemRenderMode.None) continue;
            span = Mathf.Max(span, ps.main.startSize.constantMax);
        }
        return span > 0.01f ? span : 1f;
    }

    private static VfxClip Lookup(string id)
    {
        if (library == null) library = Resources.Load<VfxLibrary>("VFX/VfxLibrary");
        if (library == null)
        {
            // 한 번만 알린다. 매 타격마다 콘솔이 도배되면 진짜 오류가 묻힌다.
            if (!libraryMissingLogged)
            {
                libraryMissingLogged = true;
                Debug.LogWarning("[PixelVfx] Resources/VFX/VfxLibrary 를 찾지 못했다. 이펙트가 나오지 않는다");
            }
            return null;
        }

        VfxClip c = library.Find(id);
        if (c == null) Debug.LogWarning($"[PixelVfx] '{id}' 정의가 없다. VfxLibrary의 id를 확인할 것");
        return c;
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Begin(VfxClip c, float rotationZ, bool stretched)
    {
        clip = c;
        t = 0f;
        playing = true;

        if (sr == null) sr = GetComponent<SpriteRenderer>();

        if (c.prefab != null) { BeginPrefab(c, rotationZ, stretched); return; }

        sr.sprite = c.frames[0];
        sr.color = c.tint;
        sr.sortingLayerName = string.IsNullOrEmpty(c.sortingLayer) ? "Default" : c.sortingLayer;
        sr.sortingOrder = c.sortingOrder;
    }

    // 파티클 프리팹을 자식으로 붙인다.
    // 스프라이트 프레임 방식과 달리 크기·색·정렬이 전부 프리팹 안에 들어 있어
    // 인스턴스마다 여기서 덮어써 준다.
    private void BeginPrefab(VfxClip c, float rotationZ, bool stretched)
    {
        sr.enabled = false;   // 프리팹 모드에선 이 오브젝트가 껍데기 역할만 한다

        var inst = Instantiate(c.prefab, transform);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true))
        {
            // 기본값(Local)은 부모 배율을 무시한다. 껍데기에 준 크기가 먹으려면 Hierarchy여야 한다.
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            if (c.tint != Color.white) main.startColor = c.tint;

            // 파티클은 트랜스폼 회전을 따라오지 않는다 (배율로 뒤집으면 아예 안 보인다).
            // 방향은 시작 각도로만 돌릴 수 있다 — 파티클 각도는 시계방향이 +라 부호를 뒤집는다.
            // 늘려 그릴 때는 쿼드로 바꾸므로 트랜스폼 회전이 그대로 먹는다. 여기서 또 돌리면 두 번 돈다.
            if (!stretched && Mathf.Abs(rotationZ) > 0.01f)
                main.startRotation = new ParticleSystem.MinMaxCurve(
                    main.startRotation.constant - rotationZ * Mathf.Deg2Rad);
        }

        foreach (var r in inst.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            r.sortingLayerName = string.IsNullOrEmpty(c.sortingLayer) ? "Default" : c.sortingLayer;
            r.sortingOrder = c.sortingOrder;

            // 빌보드는 축별 배율을 무시하고 가장 큰 축으로 균등하게 커진다 —
            // 가로로만 늘리려 해도 세로까지 같이 늘어난다(재 봄: 배율 4,1,1 → 사방 4배).
            // 쿼드 메시로 바꾸면 그제서야 축별로 늘어난다.
            if (stretched && r.renderMode == ParticleSystemRenderMode.Billboard)
            {
                r.renderMode = ParticleSystemRenderMode.Mesh;
                r.mesh = QuadMesh();
                r.alignment = ParticleSystemRenderSpace.Local;
            }
        }

        // 파티클이 다 꺼져도 오브젝트는 남으므로 수명을 직접 건다.
        if (!c.loop) Destroy(gameObject, Mathf.Max(0.05f, c.lifeTime));
    }

    // 이펙트는 연출이라 슬로우(보스 처치)에 같이 늘어지는 편이 자연스럽다.
    // 그래서 여기만은 스케일된 시간을 쓴다 — UI 막대와 반대다.
    private void Update()
    {
        if (!playing || clip == null || clip.prefab != null) return;

        t += Time.deltaTime * clip.fps;
        int i = Mathf.FloorToInt(t);

        if (i >= clip.frames.Length)
        {
            if (!clip.loop) { playing = false; Destroy(gameObject); return; }
            i %= clip.frames.Length;
            t -= clip.frames.Length;
        }

        sr.sprite = clip.frames[i];
    }

    // 1x1 사각형. 빌보드를 대신할 최소한의 메시라 코드로 만들고 하나를 돌려 쓴다.
    private static Mesh quadMesh;

    private static Mesh QuadMesh()
    {
        if (quadMesh != null) return quadMesh;

        quadMesh = new Mesh();
        quadMesh.name = "PixelVfxQuad";
        quadMesh.hideFlags = HideFlags.HideAndDontSave;
        quadMesh.vertices = new Vector3[] {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f,  0.5f, 0f), new Vector3(-0.5f,  0.5f, 0f) };
        quadMesh.uv = new Vector2[] {
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        quadMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        quadMesh.RecalculateNormals();
        quadMesh.RecalculateBounds();
        return quadMesh;
    }

    // 움직이는 주인을 따라다니게 한다.
    //
    // 자식으로 붙이지 않는 이유: 적은 왼쪽을 볼 때 Y축으로 180도 돌아가는데,
    // 자식이 되면 그림까지 같이 뒤집혀 늘려 둔 방향이 반대가 된다.
    // 자리만 따라가고 회전·배율은 건드리지 않는다.
    public PixelVfx Follow(Transform target)
    {
        followTarget = target;
        if (target != null) followOffset = transform.position - target.position;
        return this;
    }

    private Transform followTarget;
    private Vector3 followOffset;

    private void LateUpdate()
    {
        if (followTarget == null) return;
        transform.position = followTarget.position + followOffset;
    }

    // 루프 이펙트를 끝낼 때. 즉시 없앤다.
    public void Stop()
    {
        playing = false;
        Destroy(gameObject);
    }
}
