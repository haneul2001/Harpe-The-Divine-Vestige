using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 몬스터 애니메이션 브라우저 — 메뉴 [Harpe > 몬스터 애니메이션 브라우저]
//
// 몬스터 프리팹의 애니메이터에 물린 클립을 전부 격자로 깔고 동시에 재생한다.
// 클립을 하나씩 열어 타임라인을 긁어 보는 수고를 없애려고 만들었다. (VFX 브라우저와 같은 결)
//
// 클립은 전부 SpriteRenderer.m_Sprite 키프레임이라, 애니메이터를 돌리지 않고
// 키프레임에서 스프라이트를 직접 뽑아 GUI로 그린다 — 씬도 프리뷰 씬도 필요 없다.
//
// 칸 클릭: 클립 선택 / 더블클릭: 프리팹 선택 / 우클릭: 경로 복사
// 정지 상태에서는 아래 막대로 프레임을 한 장씩 넘겨 볼 수 있다.
public class MonsterAnimBrowserWindow : EditorWindow
{
    private static readonly string[] SearchRoots = { "Assets/Haneul_Branch" };

    private class Frame
    {
        public float time;
        public Sprite sprite;
    }

    private class Entry
    {
        public AnimationClip clip;
        public string clipPath;
        public string owner;        // 몬스터 이름 (프리팹). 프리팹이 없으면 컨트롤러 이름
        public string ownerPath;
        public bool isEnemy;        // 몬스터 프리팹에 물린 클립인가
        public List<Frame> frames = new List<Frame>();
        public float length;
        public float fps;
        public bool looping;

        // 모든 프레임을 피벗 기준으로 합친 크기(픽셀). 프레임마다 그림 크기가 달라도 안 흔들리게 한다
        public Rect pivotBounds;
    }

    private readonly List<Entry> all = new List<Entry>();
    private string[] owners = { "전체" };
    private int ownerIndex;
    private string search = "";
    private bool playing = true;
    private float speed = 1f;
    private float cellSize = 170f;
    private bool darkBackground = true;
    private bool enemiesOnly = true;
    private Vector2 scroll;

    private Entry selected;
    private int scrubFrame;
    private double lastRepaint;
    private double clock;
    private double lastClockTime;

    [MenuItem("Harpe/몬스터 애니메이션 브라우저")]
    private static void Open()
    {
        var w = GetWindow<MonsterAnimBrowserWindow>("몬스터 애니메이션");
        w.minSize = new Vector2(520f, 360f);
        w.Rescan();
    }

    private void OnEnable()
    {
        lastClockTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        if (all.Count == 0) Rescan();
    }

    private void OnDisable()
    {
        EditorApplication.update -= Tick;
    }

    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        double dt = now - lastClockTime;
        lastClockTime = now;

        if (playing) clock += dt * speed;

        // 60fps로 다시 그리면 에디터가 무거워진다. 30fps면 픽셀 애니메이션엔 충분하다
        if (playing && now - lastRepaint > 1.0 / 30.0)
        {
            lastRepaint = now;
            Repaint();
        }
    }

    // ────────────────────────────── 수집 ──────────────────────────────

    private void Rescan()
    {
        all.Clear();

        // 클립 → 그 클립을 쓰는 컨트롤러 (프리팹을 못 찾았을 때 쓸 이름)
        var controllerOf = new Dictionary<AnimationClip, string>();
        foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", SearchRoots))
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
            if (ctrl == null) continue;
            foreach (var clip in ctrl.animationClips)
                if (clip != null && !controllerOf.ContainsKey(clip)) controllerOf[clip] = ctrl.name;
        }

        // 클립 → 그 클립을 쓰는 몬스터 프리팹
        var ownerOf = new Dictionary<AnimationClip, GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", SearchRoots))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null || go.GetComponentInChildren<Enemy>(true) == null) continue;

            foreach (var anim in go.GetComponentsInChildren<Animator>(true))
            {
                if (anim.runtimeAnimatorController == null) continue;
                foreach (var clip in anim.runtimeAnimatorController.animationClips)
                {
                    if (clip == null) continue;
                    if (!ownerOf.ContainsKey(clip)) ownerOf[clip] = go;
                }
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", SearchRoots))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;

            Entry e = Build(clip, path, ownerOf, controllerOf);
            if (e != null) all.Add(e);
        }

        // 주인 있는 몬스터가 먼저, 같은 몬스터 안에서는 이름 순
        all.Sort(delegate(Entry a, Entry b)
        {
            int byOwner = string.Compare(a.owner, b.owner, System.StringComparison.Ordinal);
            return byOwner != 0 ? byOwner : string.Compare(a.clip.name, b.clip.name, System.StringComparison.Ordinal);
        });

        var names = new List<string> { "전체" };
        names.AddRange(all.Where(x => !enemiesOnly || x.isEnemy).Select(x => x.owner).Distinct());
        owners = names.ToArray();
        ownerIndex = Mathf.Clamp(ownerIndex, 0, owners.Length - 1);
    }

    private Entry Build(AnimationClip clip, string path, Dictionary<AnimationClip, GameObject> ownerOf,
        Dictionary<AnimationClip, string> controllerOf)
    {
        // 스프라이트 키가 여러 줄이면 프레임이 가장 많은 줄을 쓴다 (본체 SpriteRenderer)
        ObjectReferenceKeyframe[] best = null;
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.propertyName != "m_Sprite") continue;
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys != null && (best == null || keys.Length > best.Length)) best = keys;
        }
        if (best == null || best.Length == 0) return null;   // 스프라이트 애니메이션이 아니면 보여 줄 게 없다

        GameObject owner;
        ownerOf.TryGetValue(clip, out owner);

        // 몬스터 프리팹이 먼저, 없으면 컨트롤러 이름(플레이어·소품 등), 그것도 없으면 폴더 이름으로 묶는다
        string ownerName;
        if (owner != null) ownerName = owner.name;
        else if (!controllerOf.TryGetValue(clip, out ownerName) || string.IsNullOrEmpty(ownerName))
            ownerName = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path));

        var e = new Entry
        {
            clip = clip,
            clipPath = path,
            owner = ownerName,
            isEnemy = owner != null,
            ownerPath = owner != null ? AssetDatabase.GetAssetPath(owner) : null,
            length = clip.length,
            fps = clip.frameRate,
            looping = clip.isLooping,
        };

        float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;
        foreach (var k in best)
        {
            var sprite = k.value as Sprite;
            if (sprite == null) continue;
            e.frames.Add(new Frame { time = k.time, sprite = sprite });

            Vector2 pivot = sprite.pivot;                 // 그림 왼쪽 아래 기준 피벗(픽셀)
            Rect r = sprite.rect;
            minX = Mathf.Min(minX, -pivot.x);
            maxX = Mathf.Max(maxX, r.width - pivot.x);
            minY = Mathf.Min(minY, -pivot.y);
            maxY = Mathf.Max(maxY, r.height - pivot.y);
        }
        if (e.frames.Count == 0) return null;

        e.pivotBounds = new Rect(minX, minY, Mathf.Max(1f, maxX - minX), Mathf.Max(1f, maxY - minY));
        return e;
    }

    // ────────────────────────────── 그리기 ──────────────────────────────

    private void OnGUI()
    {
        DrawToolbar();

        List<Entry> shown = Filter();
        if (shown.Count == 0)
        {
            EditorGUILayout.HelpBox("보여 줄 스프라이트 애니메이션이 없다. [다시 훑기]를 눌러 봐.", MessageType.Info);
            return;
        }

        DrawGrid(shown);
        DrawFooter();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        int newOwner = EditorGUILayout.Popup(ownerIndex, owners, EditorStyles.toolbarPopup, GUILayout.Width(160f));
        if (newOwner != ownerIndex) { ownerIndex = newOwner; scroll = Vector2.zero; }

        search = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(160f));

        if (GUILayout.Button(playing ? "❚❚ 정지" : "▶ 재생", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            playing = !playing;

        GUILayout.Label("속도", EditorStyles.miniLabel, GUILayout.Width(28f));
        speed = GUILayout.HorizontalSlider(speed, 0.1f, 3f, GUILayout.Width(70f));
        GUILayout.Label(speed.ToString("0.0") + "x", EditorStyles.miniLabel, GUILayout.Width(32f));

        GUILayout.Label("크기", EditorStyles.miniLabel, GUILayout.Width(28f));
        cellSize = GUILayout.HorizontalSlider(cellSize, 90f, 320f, GUILayout.Width(80f));

        darkBackground = GUILayout.Toggle(darkBackground, "어두운 배경", EditorStyles.toolbarButton, GUILayout.Width(80f));

        bool onlyEnemy = GUILayout.Toggle(enemiesOnly, "몬스터만", EditorStyles.toolbarButton, GUILayout.Width(62f));
        if (onlyEnemy != enemiesOnly) { enemiesOnly = onlyEnemy; ownerIndex = 0; scroll = Vector2.zero; Rescan(); }

        GUILayout.FlexibleSpace();
        GUILayout.Label(Filter().Count + " / " + all.Count + " 클립", EditorStyles.miniLabel);
        if (GUILayout.Button("다시 훑기", EditorStyles.toolbarButton, GUILayout.Width(70f))) Rescan();

        EditorGUILayout.EndHorizontal();
    }

    private List<Entry> Filter()
    {
        string q = search != null ? search.Trim() : "";
        return all.Where(delegate(Entry e)
        {
            if (enemiesOnly && !e.isEnemy) return false;
            if (ownerIndex > 0 && e.owner != owners[ownerIndex]) return false;
            if (q.Length == 0) return true;
            return e.clip.name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0
                || e.owner.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }).ToList();
    }

    private const float LabelHeight = 32f;

    private void DrawGrid(List<Entry> shown)
    {
        float viewWidth = position.width - 18f;
        int columns = Mathf.Max(1, Mathf.FloorToInt(viewWidth / (cellSize + 6f)));
        int rows = Mathf.CeilToInt(shown.Count / (float)columns);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Space(rows * (cellSize + LabelHeight + 8f));
        Rect area = GUILayoutUtility.GetLastRect();

        for (int i = 0; i < shown.Count; i++)
        {
            int cx = i % columns, cy = i / columns;
            var cell = new Rect(
                area.x + cx * (cellSize + 6f),
                area.y + cy * (cellSize + LabelHeight + 8f),
                cellSize, cellSize + LabelHeight);

            DrawCell(shown[i], cell);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawCell(Entry e, Rect cell)
    {
        var art = new Rect(cell.x, cell.y, cell.width, cell.height - LabelHeight);

        bool isSelected = selected == e;
        EditorGUI.DrawRect(art, darkBackground ? new Color(0.13f, 0.12f, 0.15f) : new Color(0.62f, 0.62f, 0.66f));
        if (isSelected)
        {
            var outline = new Rect(art.x - 1f, art.y - 1f, art.width + 2f, art.height + 2f);
            EditorGUI.DrawRect(outline, new Color(1f, 0.75f, 0.3f, 0.55f));
            EditorGUI.DrawRect(art, darkBackground ? new Color(0.13f, 0.12f, 0.15f) : new Color(0.62f, 0.62f, 0.66f));
        }

        int index = FrameIndex(e);
        DrawSprite(e, index, art);

        var label = new Rect(cell.x, art.yMax + 2f, cell.width, LabelHeight);
        GUI.Label(label, e.clip.name + "\n" + e.owner + " · " + e.frames.Count + "프레임 · "
            + e.fps.ToString("0") + "fps · " + e.length.ToString("0.00") + "초" + (e.looping ? " · 반복" : ""),
            EditorStyles.miniLabel);

        HandleCellInput(e, cell);
    }

    // 현재 보여 줄 프레임. 재생 중이면 시계에서, 정지 중이면 아래 막대에서 정한다
    private int FrameIndex(Entry e)
    {
        if (!playing && selected == e) return Mathf.Clamp(scrubFrame, 0, e.frames.Count - 1);

        float len = Mathf.Max(0.0001f, e.length);
        float t = (float)(clock % len);

        int index = 0;
        for (int i = 0; i < e.frames.Count; i++)
            if (e.frames[i].time <= t + 0.0001f) index = i;
        return index;
    }

    private void DrawSprite(Entry e, int index, Rect art)
    {
        Sprite sprite = e.frames[Mathf.Clamp(index, 0, e.frames.Count - 1)].sprite;
        if (sprite == null || sprite.texture == null) return;

        // 모든 프레임을 합친 크기에 맞춰 배율을 정한다 — 프레임마다 그림 크기가 달라도 안 흔들린다.
        // 픽셀아트라 정수 배로만 키운다 (1배보다 작아야 할 때만 소수 허용)
        float pad = 8f;
        float fit = Mathf.Min((art.width - pad * 2f) / e.pivotBounds.width, (art.height - pad * 2f) / e.pivotBounds.height);
        float scale = fit >= 1f ? Mathf.Floor(fit) : fit;

        // 합친 상자를 칸 한가운데에 놓고, 그 안에서 이번 프레임의 자리를 피벗으로 잡는다
        float boxW = e.pivotBounds.width * scale, boxH = e.pivotBounds.height * scale;
        float boxX = art.x + (art.width - boxW) * 0.5f;
        float boxY = art.y + (art.height - boxH) * 0.5f;

        Vector2 pivot = sprite.pivot;
        Rect r = sprite.rect;
        float left = -pivot.x, top = r.height - pivot.y;
        float x = boxX + (left - e.pivotBounds.xMin) * scale;
        float y = boxY + (e.pivotBounds.yMax - top) * scale;   // GUI는 y가 아래로 커진다

        var dst = new Rect(x, y, r.width * scale, r.height * scale);
        var tex = sprite.texture;
        var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        GUI.DrawTextureWithTexCoords(dst, tex, uv, true);
    }

    private void HandleCellInput(Entry e, Rect cell)
    {
        Event ev = Event.current;
        if (!cell.Contains(ev.mousePosition)) return;

        if (ev.type == EventType.MouseDown && ev.button == 0)
        {
            selected = e;
            scrubFrame = FrameIndex(e);
            if (ev.clickCount >= 2 && e.ownerPath != null)
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(e.ownerPath);
            else
                Selection.activeObject = e.clip;
            EditorGUIUtility.PingObject(Selection.activeObject);
            ev.Use();
            Repaint();
        }
        else if (ev.type == EventType.ContextClick)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("클립 경로 복사"), false, delegate { EditorGUIUtility.systemCopyBuffer = e.clipPath; });
            menu.AddItem(new GUIContent("클립 선택"), false, delegate { Selection.activeObject = e.clip; });
            if (e.ownerPath != null)
                menu.AddItem(new GUIContent("몬스터 프리팹 선택"), false, delegate { Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(e.ownerPath); });
            menu.ShowAsContext();
            ev.Use();
        }
    }

    // 정지 상태에서 선택한 클립의 프레임을 한 장씩 넘겨 본다
    private void DrawFooter()
    {
        if (selected == null) return;

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label(selected.owner + " · " + selected.clip.name, EditorStyles.boldLabel, GUILayout.Width(240f));

        if (playing)
        {
            GUILayout.Label("정지하면 프레임을 한 장씩 볼 수 있다", EditorStyles.miniLabel);
        }
        else
        {
            int last = selected.frames.Count - 1;
            if (GUILayout.Button("◀", GUILayout.Width(26f))) scrubFrame = Mathf.Max(0, scrubFrame - 1);
            if (GUILayout.Button("▶", GUILayout.Width(26f))) scrubFrame = Mathf.Min(last, scrubFrame + 1);
            scrubFrame = Mathf.RoundToInt(GUILayout.HorizontalSlider(scrubFrame, 0f, last));
            GUILayout.Label((scrubFrame + 1) + " / " + (last + 1) + "  ("
                + selected.frames[Mathf.Clamp(scrubFrame, 0, last)].time.ToString("0.00") + "초)",
                EditorStyles.miniLabel, GUILayout.Width(130f));
        }

        EditorGUILayout.EndHorizontal();
    }
}
