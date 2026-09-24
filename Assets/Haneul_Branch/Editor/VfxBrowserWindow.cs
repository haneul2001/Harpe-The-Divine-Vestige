using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// VFX 브라우저 — 메뉴 [Harpe > VFX 브라우저]
//
// 이펙트 팩의 프리팹을 격자로 깔고 전부 동시에 움직이는 썸네일로 보여 준다.
// 프리팹을 하나씩 씬에 끌어다 놓고 재생해 보는 수고를 없애려고 만들었다.
//
//  · 파티클 프리팹(Vefects 등)   → ParticleSystem.Simulate로 시간을 직접 돌린다
//  · 애니메이터 프리팹(100PixelVFX) → 컨트롤러 안의 상태(클립)마다 칸을 따로 만든다
//
// 칸 클릭: 프로젝트 창에서 선택 / 더블클릭: 프리팹 열기 / 드래그: 씬·인스펙터로 끌어 놓기
// 우클릭: 경로·상태 이름 복사
public class VfxBrowserWindow : EditorWindow
{
    private static readonly string[] DefaultRoots =
    {
        "Assets/ThirdParty/Vefects/Pixel Craft VFX URP/VFX",
        "Assets/ThirdParty/100PixelVFX/Prefabs",
    };

    private class Entry
    {
        public string path;         // 프리팹 경로
        public string state;        // 애니메이터 상태 이름 (파티클이면 null)
        public AnimationClip clip;  // 애니메이터 칸일 때 샘플링할 클립
        public string category;
        public string label;
        public bool isLoopVariant;
        public bool is2D;

        public string Key => state == null ? path : path + "#" + state;
    }

    private class Live
    {
        public GameObject go;
        public ParticleSystem[] systems;
        public float duration;
        public Bounds bounds;
        public bool framed;
    }

    private List<Entry> all = new List<Entry>();
    private string[] categories = { "전체" };
    private int categoryIndex;
    private string search = "";
    private bool only2D = true;
    private bool hideLoopCopies = true;
    private bool playing = true;
    private float cellSize = 150f;
    private float speed = 1f;
    private Vector2 scroll;

    private PreviewRenderUtility preview;
    private readonly Dictionary<string, Live> live = new Dictionary<string, Live>();
    private double startTime;
    private double lastRepaint;
    private float pausedAt;

    private const int MaxLiveInstances = 60;   // 화면에 보이는 칸만 인스턴스를 들고 있는다

    // 칸마다 3D 미리보기를 매 프레임 그리면(30fps × 수십 칸) URP가 프레임마다 잡는 메모리가
    // 쌓여 에디터가 몇십 GB까지 부풀다 죽는다. 그래서 움직이는 건 마우스를 올린 칸 하나뿐이고
    // 나머지는 한 번 찍어 둔 그림을 다시 그린다.
    private readonly Dictionary<string, Texture2D> stills = new Dictionary<string, Texture2D>();
    private float stillsCellSize = -1f;
    private string hoverKey = "";

    [MenuItem("Harpe/VFX 브라우저")]
    public static void Open()
    {
        var w = GetWindow<VfxBrowserWindow>("VFX 브라우저");
        w.minSize = new Vector2(480f, 360f);
        w.Show();
    }

    private void OnEnable()
    {
        startTime = EditorApplication.timeSinceStartup;
        Collect();
        EditorApplication.update += Tick;
    }

    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        ClearStills();
        ClearLive();
        if (preview != null) { preview.Cleanup(); preview = null; }
    }

    private void Tick()
    {
        if (!playing) return;
        // 30fps 정도로만 다시 그린다 — 칸이 많아서 매 에디터 틱마다 그리면 무겁다
        if (EditorApplication.timeSinceStartup - lastRepaint < 1.0 / 30.0) return;
        lastRepaint = EditorApplication.timeSinceStartup;
        Repaint();
    }

    private float Now()
    {
        if (!playing) return pausedAt;
        return (float)(EditorApplication.timeSinceStartup - startTime) * speed;
    }

    // ─────────────────────────────────────────────
    // 목록 모으기
    // ─────────────────────────────────────────────

    private void Collect()
    {
        all.Clear();
        ClearLive();

        var roots = DefaultRoots.Where(AssetDatabase.IsValidFolder).ToArray();
        if (roots.Length == 0) return;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", roots))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            string category = CategoryOf(path);
            string file = Path.GetFileNameWithoutExtension(path);
            bool loopVariant = path.Replace('\\', '/').Contains("/Loop/");
            bool is2D = file.Contains("_2D_") || path.Contains("100PixelVFX");

            var animator = prefab.GetComponentInChildren<Animator>(true);
            var controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;

            if (controller != null && prefab.GetComponentInChildren<ParticleSystem>(true) == null)
            {
                // 상태 하나 = 칸 하나 (100PixelVFX는 프리팹 하나에 연기 20종이 들어 있다)
                foreach (var layer in controller.layers)
                foreach (var st in layer.stateMachine.states)
                {
                    var clip = st.state.motion as AnimationClip;
                    if (clip == null) continue;
                    if (clip.length <= 0.34f && clip.name.EndsWith("Blank")) continue;   // 빈 상태

                    all.Add(new Entry
                    {
                        path = path, state = st.state.name, clip = clip,
                        category = category, label = st.state.name.Replace("VFX_", ""),
                        isLoopVariant = false, is2D = true,
                    });
                }
                continue;
            }

            if (prefab.GetComponentInChildren<ParticleSystem>(true) == null) continue;

            all.Add(new Entry
            {
                path = path, category = category,
                label = file.Replace("VFX_2D_", "").Replace("VFX_", ""),
                isLoopVariant = loopVariant, is2D = is2D,
            });
        }

        all = all.OrderBy(e => e.category).ThenBy(e => e.label).ToList();
        categories = new[] { "전체" }.Concat(all.Select(e => e.category).Distinct()).ToArray();
        categoryIndex = Mathf.Clamp(categoryIndex, 0, categories.Length - 1);
    }

    // "…/VFX/Fireball/Particles/x.prefab" → "Fireball", "100PixelVFX/Prefabs/VFX_smoke" → "100Pixel · smoke"
    private static string CategoryOf(string path)
    {
        string p = path.Replace('\\', '/');
        int vfx = p.IndexOf("/VFX/");
        if (vfx >= 0)
        {
            string rest = p.Substring(vfx + 5);
            int slash = rest.IndexOf('/');
            return slash > 0 ? rest.Substring(0, slash) : rest;
        }
        if (p.Contains("100PixelVFX"))
            return "100Pixel · " + Path.GetFileNameWithoutExtension(p).Replace("VFX_", "");
        return Path.GetFileName(Path.GetDirectoryName(p));
    }

    private List<Entry> Filtered()
    {
        string cat = categoryIndex > 0 ? categories[categoryIndex] : null;
        string s = search.Trim().ToLowerInvariant();
        return all.Where(e =>
                (cat == null || e.category == cat) &&
                (!only2D || e.is2D) &&
                (!hideLoopCopies || !e.isLoopVariant) &&
                (s.Length == 0 || e.label.ToLowerInvariant().Contains(s) || e.category.ToLowerInvariant().Contains(s)))
            .ToList();
    }

    // ─────────────────────────────────────────────
    // 화면
    // ─────────────────────────────────────────────

    private void OnGUI()
    {
        DrawToolbar();

        var list = Filtered();
        float width = position.width - 16f;
        int columns = Mathf.Max(1, Mathf.FloorToInt(width / (cellSize + 6f)));
        int rows = Mathf.CeilToInt(list.Count / (float)columns);
        float cellH = cellSize + 20f;

        Rect view = new Rect(0, 0, width, rows * (cellH + 6f));
        Rect area = new Rect(0, 44f, position.width, position.height - 44f);
        scroll = GUI.BeginScrollView(area, scroll, view);

        var visibleKeys = new HashSet<string>();
        float t = Now();
        capturedThisFrame = 0;

        for (int i = 0; i < list.Count; i++)
        {
            int r = i / columns, c = i % columns;
            Rect cell = new Rect(c * (cellSize + 6f), r * (cellH + 6f), cellSize, cellH);

            // 스크롤 밖 칸은 그리지도, 인스턴스를 만들지도 않는다
            if (cell.yMax < scroll.y || cell.y > scroll.y + area.height) continue;

            Entry e = list[i];
            visibleKeys.Add(e.Key);
            DrawCell(cell, e, t, i * 0.37f);
        }

        GUI.EndScrollView();
        TrimLive(visibleKeys);
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        categoryIndex = EditorGUILayout.Popup(categoryIndex, categories, EditorStyles.toolbarPopup, GUILayout.Width(170f));
        search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120f));
        only2D = GUILayout.Toggle(only2D, "2D 버전만", EditorStyles.toolbarButton, GUILayout.Width(70f));
        hideLoopCopies = GUILayout.Toggle(hideLoopCopies, "Loop 복제본 숨김", EditorStyles.toolbarButton, GUILayout.Width(110f));
        GUILayout.FlexibleSpace();

        bool newPlaying = GUILayout.Toggle(playing, playing ? "❚❚ 일시정지" : "▶ 재생", EditorStyles.toolbarButton, GUILayout.Width(80f));
        if (newPlaying != playing)
        {
            if (!newPlaying) pausedAt = Now();
            else startTime = EditorApplication.timeSinceStartup - pausedAt / Mathf.Max(0.01f, speed);
            playing = newPlaying;
        }
        if (GUILayout.Button("↻ 새로고침", EditorStyles.toolbarButton, GUILayout.Width(80f))) Collect();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("칸 크기", GUILayout.Width(44f));
        cellSize = GUILayout.HorizontalSlider(cellSize, 90f, 320f, GUILayout.Width(140f));
        GUILayout.Space(12f);
        GUILayout.Label("속도", GUILayout.Width(30f));
        speed = GUILayout.HorizontalSlider(speed, 0.1f, 2f, GUILayout.Width(100f));
        GUILayout.Label(speed.ToString("0.0") + "x", GUILayout.Width(34f));
        GUILayout.FlexibleSpace();
        GUILayout.Label(Filtered().Count + " / " + all.Count + "개", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCell(Rect cell, Entry e, float time, float phase)
    {
        Rect img = new Rect(cell.x, cell.y, cell.width, cell.width);
        Rect label = new Rect(cell.x, img.yMax + 2f, cell.width, 18f);

        EditorGUI.DrawRect(img, new Color(0.13f, 0.13f, 0.15f));

        if (img.Contains(Event.current.mousePosition)) hoverKey = e.Key;

        if (Event.current.type == EventType.Repaint)
        {
            if (e.Key == hoverKey) RenderPreview(img, e, time + phase);    // 올려 둔 칸만 움직인다
            else DrawStill(img, e);
        }

        bool selected = Selection.activeObject != null && AssetDatabase.GetAssetPath(Selection.activeObject) == e.path;
        if (selected) DrawOutline(img, new Color(0.95f, 0.75f, 0.3f));

        var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
        GUI.Label(label, new GUIContent(e.label, e.category + "\n" + e.path + (e.state != null ? "\n상태: " + e.state : "")), style);

        HandleInput(cell, e);
    }

    private static void DrawOutline(Rect r, Color c)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2f), c);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2f, r.width, 2f), c);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 2f, r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax - 2f, r.y, 2f, r.height), c);
    }

    private void HandleInput(Rect cell, Entry e)
    {
        Event ev = Event.current;
        if (!cell.Contains(ev.mousePosition)) return;

        if (ev.type == EventType.MouseDown && ev.button == 0)
        {
            var obj = AssetDatabase.LoadAssetAtPath<GameObject>(e.path);
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
            if (ev.clickCount == 2) AssetDatabase.OpenAsset(obj);
            ev.Use();
        }
        else if (ev.type == EventType.MouseDrag && ev.button == 0)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new Object[] { AssetDatabase.LoadAssetAtPath<GameObject>(e.path) };
            DragAndDrop.paths = new[] { e.path };
            DragAndDrop.StartDrag(e.label);
            ev.Use();
        }
        else if (ev.type == EventType.ContextClick || (ev.type == EventType.MouseDown && ev.button == 1))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("경로 복사"), false, () => EditorGUIUtility.systemCopyBuffer = e.path);
            if (e.state != null)
                menu.AddItem(new GUIContent("상태 이름 복사 (" + e.state + ")"), false, () => EditorGUIUtility.systemCopyBuffer = e.state);
            menu.ShowAsContext();
            ev.Use();
        }
    }

    // ─────────────────────────────────────────────
    // 미리보기 렌더
    // ─────────────────────────────────────────────

    private void EnsurePreview()
    {
        if (preview != null) return;
        preview = new PreviewRenderUtility();
        var cam = preview.camera;
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.13f, 0.13f, 0.15f, 1f);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 200f;
        cam.transform.rotation = Quaternion.identity;
    }

    // 한 번 찍어 둔 그림을 그린다. 없으면 이번 프레임에 한 칸만 찍는다 —
    // 한꺼번에 다 찍으면 창을 열자마자 예전과 같은 양을 렌더링하게 된다.
    private void DrawStill(Rect rect, Entry e)
    {
        if (stillsCellSize != cellSize) { ClearStills(); stillsCellSize = cellSize; }

        Texture2D tex;
        if (stills.TryGetValue(e.Key, out tex) && tex != null)
        {
            GUI.DrawTexture(rect, tex);
            return;
        }

        if (capturedThisFrame >= 1) return;   // 프레임당 한 칸
        capturedThisFrame++;

        tex = CapturePreview(rect, e);
        if (tex != null)
        {
            stills[e.Key] = tex;
            GUI.DrawTexture(rect, tex);
        }
    }

    private int capturedThisFrame;

    private Texture2D CapturePreview(Rect rect, Entry e)
    {
        EnsurePreview();
        Live l = GetLive(e);
        if (l == null || l.go == null) return null;

        SetupScene(l, e, StillTime(e, l));

        preview.BeginPreview(rect, GUIStyle.none);
        preview.Render(true);
        Texture rt = preview.EndPreview();
        if (rt == null) return null;

        int w = Mathf.Max(8, Mathf.RoundToInt(rect.width));
        int h = Mathf.Max(8, Mathf.RoundToInt(rect.height));

        var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
        copy.hideFlags = HideFlags.HideAndDontSave;

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt as RenderTexture;
        copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        return copy;
    }

    // 정지 그림으로 쓸 시점 — 이펙트가 가장 잘 보이는 중간쯤
    private static float StillTime(Entry e, Live l)
    {
        float len = e.clip != null ? e.clip.length : l.duration;
        return len * 0.45f;
    }

    private void ClearStills()
    {
        foreach (var t in stills.Values) if (t != null) DestroyImmediate(t);
        stills.Clear();
    }

    private void RenderPreview(Rect rect, Entry e, float time)
    {
        EnsurePreview();
        Live l = GetLive(e);
        if (l == null || l.go == null) return;

        SetupScene(l, e, time);

        preview.BeginPreview(rect, GUIStyle.none);
        preview.Render(true);
        preview.EndAndDrawPreview(rect);
    }

    // 프리뷰 씬을 이 항목 상태로 맞춘다 (실시간·정지 캡처가 같은 경로를 쓴다)
    private void SetupScene(Live l, Entry e, float time)
    {
        // 같은 프리뷰 씬을 칸마다 돌려 쓰므로 지금 그릴 것만 켠다
        foreach (var other in live.Values)
            if (other.go != null && other != l && other.go.activeSelf) other.go.SetActive(false);
        l.go.SetActive(true);

        Sample(e, l, time);
        if (!l.framed) Frame(e, l);

        var cam = preview.camera;
        Vector3 c = l.bounds.center;
        cam.transform.position = new Vector3(c.x, c.y, c.z - 50f);
        cam.orthographicSize = Mathf.Max(0.3f, Mathf.Max(l.bounds.extents.x, l.bounds.extents.y) * 1.15f);
    }

    private void Sample(Entry e, Live l, float time)
    {
        if (e.clip != null)
        {
            // 한 바퀴 돌고 0.25초 마지막 프레임에 멈췄다가 다시 — 끝나는 모양도 보이게
            float len = Mathf.Max(0.05f, e.clip.length);
            float ct = Mathf.Min(Mathf.Repeat(time, len + 0.25f), len - 0.001f);
            e.clip.SampleAnimation(l.go, ct);
            return;
        }

        if (l.systems.Length == 0) return;
        // 한 바퀴 + 잠깐 쉬고 다시 — 원샷 이펙트도 계속 보이게 반복한다
        float cycle = l.duration + 0.4f;
        float t = Mathf.Repeat(time, cycle);
        l.systems[0].Simulate(t, true, true, true);
    }

    // 여러 시점을 돌려 보며 이펙트가 차지하는 가장 큰 범위를 잡는다 (한 번만).
    // 파티클은 렌더러 bounds가 "최대 크기 가정"이라 실제보다 몇 배 크게 나온다 —
    // 그러면 번개·폭발이 칸 안에서 점처럼 작아지므로 살아 있는 파티클 위치로 직접 잰다.
    private void Frame(Entry e, Live l)
    {
        Bounds b = new Bounds(l.go.transform.position, Vector3.one * 0.5f);
        bool any = false;
        float len = e.clip != null ? e.clip.length : l.duration;

        for (int i = 1; i <= 8; i++)
        {
            Sample(e, l, len * i / 9f);

            if (l.systems.Length > 0)
            {
                foreach (var ps in l.systems)
                {
                    var main = ps.main;
                    if (buffer == null || buffer.Length < main.maxParticles)
                        buffer = new ParticleSystem.Particle[Mathf.Min(main.maxParticles, 4096)];
                    int n = ps.GetParticles(buffer);
                    bool local = main.simulationSpace == ParticleSystemSimulationSpace.Local;
                    float scale = local ? Mathf.Abs(ps.transform.lossyScale.x) : 1f;
                    // 번개처럼 피벗을 끝으로 옮긴 파티클은 그림이 위치에서 한쪽으로 뻗는다 — 양쪽 다 넣는다
                    var psr = ps.GetComponent<ParticleSystemRenderer>();
                    Vector3 pivot = psr != null ? psr.pivot : Vector3.zero;

                    for (int k = 0; k < n; k++)
                    {
                        Vector3 pos = local ? ps.transform.TransformPoint(buffer[k].position) : buffer[k].position;
                        Vector3 size = buffer[k].GetCurrentSize3D(ps) * scale;
                        Vector3 off = new Vector3(pivot.x * size.x, pivot.y * size.y, 0f);
                        var pb = new Bounds(pos + off, new Vector3(size.x, size.y, 0.01f));
                        pb.Encapsulate(new Bounds(pos - off, new Vector3(size.x, size.y, 0.01f)));
                        if (!any) { b = pb; any = true; }
                        else b.Encapsulate(pb);
                    }
                }
                continue;
            }

            foreach (var r in l.go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                Bounds rb = r.bounds;
                if (rb.size.sqrMagnitude < 1e-6f || float.IsNaN(rb.size.x)) continue;
                if (!any) { b = rb; any = true; }
                else b.Encapsulate(rb);
            }
        }

        // 폭주하는 파티클 하나 때문에 화면이 점이 되지 않게 상한을 둔다
        Vector3 ext = b.extents;
        float m = Mathf.Min(Mathf.Max(ext.x, ext.y), 12f);
        b.extents = new Vector3(Mathf.Min(ext.x, m), Mathf.Min(ext.y, m), ext.z);
        l.bounds = b;
        l.framed = true;
    }

    private Live GetLive(Entry e)
    {
        Live l;
        if (live.TryGetValue(e.Key, out l) && l.go != null) return l;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(e.path);
        if (prefab == null) return null;

        var go = Instantiate(prefab);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.position = Vector3.zero;
        preview.AddSingleGO(go);

        var systems = go.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            var main = ps.main;
            main.playOnAwake = false;
        }
        float duration = MeasureDuration(systems);

        // 에디터 프리뷰에서 애니메이터가 스스로 돌지 않게 끈다 (클립을 직접 샘플링한다)
        foreach (var a in go.GetComponentsInChildren<Animator>(true)) a.enabled = false;

        l = new Live { go = go, systems = systems, duration = duration };
        live[e.Key] = l;
        return l;
    }

    private ParticleSystem.Particle[] buffer;

    // 설정값(duration + lifetime)은 파티클을 안 뿜는 껍데기 시스템까지 합쳐 부풀려진다.
    // 실제로 0.1초씩 돌려 보며 마지막으로 파티클이 남아 있던 시점을 길이로 쓴다 (최대 6초).
    private static float MeasureDuration(ParticleSystem[] systems)
    {
        if (systems.Length == 0) return 1f;

        const float step = 0.1f;
        const float max = 6f;
        float last = 0.3f;
        bool loops = false;

        foreach (var ps in systems) if (ps.main.loop) loops = true;
        if (loops) return 2f;   // 루프 이펙트는 2초 구간만 반복해 보여 준다

        systems[0].Simulate(0f, true, true, true);
        for (float t = step; t <= max; t += step)
        {
            systems[0].Simulate(step, true, false, true);
            int count = 0;
            foreach (var ps in systems) count += ps.particleCount;
            if (count > 0) last = t;
            else if (t > last + 1f) break;   // 1초 넘게 비어 있으면 끝난 것으로 본다
        }
        return last + step;
    }

    private void TrimLive(HashSet<string> keep)
    {
        if (live.Count <= MaxLiveInstances) return;
        foreach (var key in live.Keys.Where(k => !keep.Contains(k)).ToList())
        {
            if (live[key].go != null) DestroyImmediate(live[key].go);
            live.Remove(key);
        }
    }

    private void ClearLive()
    {
        foreach (var l in live.Values)
            if (l.go != null) DestroyImmediate(l.go);
        live.Clear();
    }
}
