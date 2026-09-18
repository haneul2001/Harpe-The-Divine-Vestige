using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 프로젝트 전체 스프라이트 애니메이션 브라우저 — 메뉴 [Harpe > 프로젝트 애니메이션 브라우저]
//
// 아직 안 쓰는 에셋 팩의 몬스터까지 전부 깔아 놓고 움직이는 걸 보면서 고르려고 만들었다.
// (게임에 들어간 몬스터만 보려면 [Harpe > 몬스터 애니메이션 브라우저] 쪽이 가볍다)
//
// 프로젝트에는 클립 수천 개, 텍스처 수만 장이 있어서 전부 읽으면 에디터가 멈춘다. 그래서
//  · 훑을 때는 "경로 문자열"만 모은다 — 에셋을 하나도 안 연다
//  · 그림은 화면에 보이는 칸만 그때 읽어서 캐시에 담는다
//
// 애니메이션 한 칸이 되는 것 세 가지:
//  1. 애니메이션 클립 (팩이 같이 준 것) — 스프라이트 키프레임을 그대로 쓴다
//  2. 잘라 놓은 스프라이트 시트 한 장 — 안에 든 스프라이트 전부를 순서대로
//  3. 낱장 그림 묶음 (idle_0.png, idle_1.png …) — 폴더 + 이름에서 숫자를 뗀 것이 같으면 한 묶음
//
// 칸 클릭: 선택(프로젝트 창에 핑) / 우클릭: 경로 복사 / 정지 후 아래 막대로 한 프레임씩
public class ProjectSpriteAnimBrowserWindow : EditorWindow
{
    private enum Kind { Clip, Sheet, Sequence }

    private class Entry
    {
        public Kind kind;
        public string label;        // 애니메이션 이름
        public string group;        // 묶음 (팩 폴더)
        public string folder;       // 에셋 폴더 경로
        public string mainPath;     // 클립·시트 경로 (낱장 묶음이면 첫 장)
        public List<string> paths;  // 낱장 묶음일 때 그림 경로들
        public int guessedFrames;   // 읽기 전에 아는 장 수 (낱장 묶음만 정확)

        // 아래는 실제로 읽은 뒤에 채워진다
        public bool loaded;
        public List<Sprite> frames;
        public Rect pivotBounds;
        public float fps = 12f;
        public string info;
    }

    private readonly List<Entry> all = new List<Entry>();
    private readonly List<Entry> shown = new List<Entry>();
    private string[] groups = { "전체" };
    private int groupIndex;
    private string search = "";
    private bool includeClips = true;
    private bool includeTextures = true;
    private bool playing = true;
    private float speed = 1f;
    private float cellSize = 150f;
    private float sheetFps = 12f;
    private bool darkBackground = true;
    private Vector2 scroll;
    private bool scanned;

    private Entry selected;
    private int scrubFrame;
    private double clock;
    private double lastClockTime;
    private double lastRepaint;
    private int loadedThisFrame;

    [MenuItem("Harpe/프로젝트 애니메이션 브라우저")]
    private static void Open()
    {
        var w = GetWindow<ProjectSpriteAnimBrowserWindow>("프로젝트 애니메이션");
        w.minSize = new Vector2(560f, 400f);
    }

    private void OnEnable()
    {
        lastClockTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
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

        if (playing && now - lastRepaint > 1.0 / 30.0)
        {
            lastRepaint = now;
            Repaint();
        }
    }

    // ────────────────────────────── 훑기 (경로만) ──────────────────────────────

    private void Rescan()
    {
        all.Clear();
        selected = null;

        try
        {
            if (includeClips) ScanClips();
            if (includeTextures) ScanTextures();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        all.Sort(delegate(Entry a, Entry b)
        {
            int byGroup = string.Compare(a.group, b.group, System.StringComparison.Ordinal);
            if (byGroup != 0) return byGroup;
            int byFolder = string.Compare(a.folder, b.folder, System.StringComparison.Ordinal);
            return byFolder != 0 ? byFolder : string.Compare(a.label, b.label, System.StringComparison.Ordinal);
        });

        var names = new List<string> { "전체" };
        names.AddRange(all.Select(x => x.group).Distinct().OrderBy(x => x));
        groups = names.ToArray();
        groupIndex = 0;
        scanned = true;
        Refilter();
    }

    private void ScanClips()
    {
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
        for (int i = 0; i < guids.Length; i++)
        {
            if ((i & 63) == 0 && EditorUtility.DisplayCancelableProgressBar("애니메이션 훑는 중", "클립 " + i + " / " + guids.Length, i / (float)guids.Length))
                break;

            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!Wanted(path)) continue;   // 엔진 패키지 안의 리소스는 볼 일이 없다

            all.Add(new Entry
            {
                kind = Kind.Clip,
                label = System.IO.Path.GetFileNameWithoutExtension(path),
                group = GroupOf(path),
                folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'),
                mainPath = path,
            });
        }
    }

    // 텍스처는 열지 않는다. 경로 이름만 보고 "숫자만 다른 낱장"을 한 묶음으로 만든다.
    // 시트 한 장짜리(잘라 놓은 스프라이트)는 묶음이 1장으로 잡히고, 그릴 때 안에서 여러 장이 나온다.
    private void ScanTextures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D");
        var buckets = new Dictionary<string, List<string>>();

        for (int i = 0; i < guids.Length; i++)
        {
            if ((i & 255) == 0 && EditorUtility.DisplayCancelableProgressBar("애니메이션 훑는 중", "그림 " + i + " / " + guids.Length, i / (float)guids.Length))
                break;

            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!Wanted(path)) continue;
            if (!path.EndsWith(".png") && !path.EndsWith(".tga") && !path.EndsWith(".jpg")) continue;

            string dir = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            string key = dir + "|" + StripTrailingNumber(name);

            List<string> list;
            if (!buckets.TryGetValue(key, out list)) { list = new List<string>(); buckets[key] = list; }
            list.Add(path);
        }

        foreach (var kv in buckets)
        {
            var paths = kv.Value;
            paths.Sort(CompareNatural);

            string dir = kv.Key.Substring(0, kv.Key.IndexOf('|'));
            string stem = kv.Key.Substring(kv.Key.IndexOf('|') + 1);

            all.Add(new Entry
            {
                kind = paths.Count > 1 ? Kind.Sequence : Kind.Sheet,
                label = string.IsNullOrEmpty(stem) ? System.IO.Path.GetFileNameWithoutExtension(paths[0]) : stem,
                group = GroupOf(paths[0]),
                folder = dir,
                mainPath = paths[0],
                paths = paths,
                guessedFrames = paths.Count,
            });
        }
    }

    // 프로젝트에 직접 넣은 에셋만 본다 — 엔진 패키지·에디터용 그림·폰트는 뺀다
    private static readonly string[] Skip = { "/Editor/", "TextMesh Pro", "/Gizmos/", "Pixelate", "SpriteOutline", "vFavorites" };

    private static bool Wanted(string path)
    {
        if (!path.StartsWith("Assets/")) return false;
        for (int i = 0; i < Skip.Length; i++) if (path.Contains(Skip[i])) return false;
        return true;
    }

    // 팩 단위로 묶는다 (Assets/ThirdParty/<팩>, 그 외에는 Assets/<폴더>)
    private static string GroupOf(string path)
    {
        string[] p = path.Split('/');
        if (p.Length > 3 && p[1] == "ThirdParty") return p[2];
        return p.Length > 1 ? p[1] : path;
    }

    private static string StripTrailingNumber(string name)
    {
        int end = name.Length;
        while (end > 0 && char.IsDigit(name[end - 1])) end--;
        while (end > 0 && (name[end - 1] == '_' || name[end - 1] == '-' || name[end - 1] == ' ')) end--;
        return end <= 0 ? name : name.Substring(0, end);
    }

    private static int CompareNatural(string a, string b)
    {
        if (a.Length == b.Length) return string.Compare(a, b, System.StringComparison.Ordinal);
        return a.Length.CompareTo(b.Length) != 0 && SameStem(a, b) ? a.Length.CompareTo(b.Length)
            : string.Compare(a, b, System.StringComparison.Ordinal);
    }

    private static bool SameStem(string a, string b)
    {
        return StripTrailingNumber(a) == StripTrailingNumber(b);
    }

    // ────────────────────────────── 읽기 (보이는 칸만) ──────────────────────────────

    private void Load(Entry e)
    {
        e.loaded = true;
        e.frames = new List<Sprite>();

        if (e.kind == Kind.Clip)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(e.mainPath);
            if (clip == null) { e.info = "클립 없음"; return; }

            ObjectReferenceKeyframe[] best = null;
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.propertyName != "m_Sprite") continue;
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keys != null && (best == null || keys.Length > best.Length)) best = keys;
            }
            if (best != null)
                foreach (var k in best) { var s = k.value as Sprite; if (s != null) e.frames.Add(s); }

            e.fps = clip.frameRate > 0f ? clip.frameRate : 12f;
            e.info = "클립 · " + e.frames.Count + "장 · " + clip.frameRate.ToString("0") + "fps";
        }
        else
        {
            foreach (string path in e.paths)
            {
                var objects = AssetDatabase.LoadAllAssetsAtPath(path);
                var sprites = objects.OfType<Sprite>().ToList();
                sprites.Sort(delegate(Sprite x, Sprite y) { return CompareNatural(x.name, y.name); });
                e.frames.AddRange(sprites);
            }
            e.fps = sheetFps;
            e.info = (e.paths.Count > 1 ? "낱장 " + e.paths.Count + "장" : "시트 1장") + " · " + e.frames.Count + "칸";
        }

        float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;
        foreach (var s in e.frames)
        {
            Vector2 pivot = s.pivot;
            Rect r = s.rect;
            minX = Mathf.Min(minX, -pivot.x); maxX = Mathf.Max(maxX, r.width - pivot.x);
            minY = Mathf.Min(minY, -pivot.y); maxY = Mathf.Max(maxY, r.height - pivot.y);
        }
        e.pivotBounds = new Rect(minX, minY, Mathf.Max(1f, maxX - minX), Mathf.Max(1f, maxY - minY));
    }

    private void ForgetAll()
    {
        foreach (var e in all) { e.loaded = false; e.frames = null; }
        EditorUtility.UnloadUnusedAssetsImmediate();
    }

    // ────────────────────────────── 그리기 ──────────────────────────────

    private void OnGUI()
    {
        DrawToolbar();

        if (!scanned)
        {
            EditorGUILayout.HelpBox("[훑기]를 누르면 프로젝트 전체의 스프라이트 애니메이션을 모은다.\n"
                + "경로만 모으므로 금방 끝나고, 그림은 화면에 보이는 칸만 읽는다.", MessageType.Info);
            return;
        }

        if (shown.Count == 0)
        {
            EditorGUILayout.HelpBox("조건에 맞는 애니메이션이 없다.", MessageType.Info);
            return;
        }

        loadedThisFrame = 0;
        DrawGrid();
        DrawFooter();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("훑기", EditorStyles.toolbarButton, GUILayout.Width(50f))) Rescan();

        int g = EditorGUILayout.Popup(groupIndex, groups, EditorStyles.toolbarPopup, GUILayout.Width(150f));
        if (g != groupIndex) { groupIndex = g; scroll = Vector2.zero; Refilter(); }

        string q = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(150f));
        if (q != search) { search = q; scroll = Vector2.zero; Refilter(); }

        if (GUILayout.Button(playing ? "❚❚ 정지" : "▶ 재생", EditorStyles.toolbarButton, GUILayout.Width(58f)))
            playing = !playing;

        GUILayout.Label("속도", EditorStyles.miniLabel, GUILayout.Width(26f));
        speed = GUILayout.HorizontalSlider(speed, 0.1f, 3f, GUILayout.Width(60f));

        GUILayout.Label("크기", EditorStyles.miniLabel, GUILayout.Width(26f));
        cellSize = GUILayout.HorizontalSlider(cellSize, 90f, 320f, GUILayout.Width(70f));

        GUILayout.Label("시트 fps", EditorStyles.miniLabel, GUILayout.Width(46f));
        float newFps = GUILayout.HorizontalSlider(sheetFps, 2f, 24f, GUILayout.Width(60f));
        if (!Mathf.Approximately(newFps, sheetFps))
        {
            sheetFps = newFps;
            foreach (var e in all) if (e.loaded && e.kind != Kind.Clip) e.fps = sheetFps;
        }

        darkBackground = GUILayout.Toggle(darkBackground, "어두운 배경", EditorStyles.toolbarButton, GUILayout.Width(74f));

        bool c = GUILayout.Toggle(includeClips, "클립", EditorStyles.toolbarButton, GUILayout.Width(40f));
        bool t = GUILayout.Toggle(includeTextures, "그림", EditorStyles.toolbarButton, GUILayout.Width(40f));
        if (c != includeClips || t != includeTextures) { includeClips = c; includeTextures = t; if (scanned) Rescan(); }

        GUILayout.FlexibleSpace();
        GUILayout.Label(shown.Count + " / " + all.Count, EditorStyles.miniLabel);
        if (GUILayout.Button("메모리 비우기", EditorStyles.toolbarButton, GUILayout.Width(84f))) ForgetAll();

        EditorGUILayout.EndHorizontal();
    }

    private void Refilter()
    {
        shown.Clear();
        string q = search != null ? search.Trim() : "";

        foreach (var e in all)
        {
            if (groupIndex > 0 && e.group != groups[groupIndex]) continue;
            if (q.Length > 0
                && e.label.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0
                && e.folder.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            shown.Add(e);
        }
    }

    private const float LabelHeight = 30f;

    private void DrawGrid()
    {
        float viewWidth = position.width - 18f;
        float cellH = cellSize + LabelHeight + 8f;
        int columns = Mathf.Max(1, Mathf.FloorToInt(viewWidth / (cellSize + 6f)));
        int rows = Mathf.CeilToInt(shown.Count / (float)columns);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Space(rows * cellH);
        Rect area = GUILayoutUtility.GetLastRect();

        // 보이는 줄만 그린다 — 수천 칸이어도 에디터가 안 밀린다
        float viewHeight = position.height;
        int firstRow = Mathf.Max(0, Mathf.FloorToInt((scroll.y - area.y) / cellH) - 1);
        int lastRow = Mathf.Min(rows - 1, firstRow + Mathf.CeilToInt(viewHeight / cellH) + 1);

        for (int row = firstRow; row <= lastRow; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                int i = row * columns + col;
                if (i >= shown.Count) break;

                var cell = new Rect(area.x + col * (cellSize + 6f), area.y + row * cellH, cellSize, cellSize + LabelHeight);
                DrawCell(shown[i], cell);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawCell(Entry e, Rect cell)
    {
        var art = new Rect(cell.x, cell.y, cell.width, cell.height - LabelHeight);
        EditorGUI.DrawRect(art, darkBackground ? new Color(0.13f, 0.12f, 0.15f) : new Color(0.62f, 0.62f, 0.66f));
        if (selected == e)
            EditorGUI.DrawRect(new Rect(art.x, art.yMax - 2f, art.width, 2f), new Color(1f, 0.75f, 0.3f));

        // 한 번에 너무 많이 읽으면 스크롤이 튄다. 한 프레임에 몇 개씩만 읽고 나머지는 다음 프레임에
        if (!e.loaded)
        {
            if (loadedThisFrame >= 12) { GUI.Label(art, "읽는 중…", EditorStyles.centeredGreyMiniLabel); Repaint(); }
            else { Load(e); loadedThisFrame++; }
        }

        if (e.loaded && e.frames.Count > 0) DrawSprite(e, FrameIndex(e), art);
        else if (e.loaded) GUI.Label(art, "스프라이트 없음", EditorStyles.centeredGreyMiniLabel);

        var label = new Rect(cell.x, art.yMax + 2f, cell.width, LabelHeight);
        GUI.Label(label, e.label + "\n" + e.group + (e.loaded ? " · " + e.info : ""), EditorStyles.miniLabel);

        HandleInput(e, cell);
    }

    private int FrameIndex(Entry e)
    {
        if (!playing && selected == e) return Mathf.Clamp(scrubFrame, 0, e.frames.Count - 1);
        float fps = Mathf.Max(1f, e.fps);
        return (int)(((long)(clock * fps)) % e.frames.Count);
    }

    private void DrawSprite(Entry e, int index, Rect art)
    {
        Sprite sprite = e.frames[Mathf.Clamp(index, 0, e.frames.Count - 1)];
        if (sprite == null || sprite.texture == null) return;

        float pad = 8f;
        float fit = Mathf.Min((art.width - pad * 2f) / e.pivotBounds.width, (art.height - pad * 2f) / e.pivotBounds.height);
        float scale = fit >= 1f ? Mathf.Floor(fit) : fit;   // 픽셀아트는 정수 배로만 키운다

        float boxW = e.pivotBounds.width * scale, boxH = e.pivotBounds.height * scale;
        float boxX = art.x + (art.width - boxW) * 0.5f;
        float boxY = art.y + (art.height - boxH) * 0.5f;

        Vector2 pivot = sprite.pivot;
        Rect r = sprite.rect;
        float x = boxX + (-pivot.x - e.pivotBounds.xMin) * scale;
        float y = boxY + (e.pivotBounds.yMax - (r.height - pivot.y)) * scale;

        var tex = sprite.texture;
        var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        GUI.DrawTextureWithTexCoords(new Rect(x, y, r.width * scale, r.height * scale), tex, uv, true);
    }

    private void HandleInput(Entry e, Rect cell)
    {
        Event ev = Event.current;
        if (!cell.Contains(ev.mousePosition)) return;

        if (ev.type == EventType.MouseDown && ev.button == 0)
        {
            selected = e;
            scrubFrame = e.loaded && e.frames.Count > 0 ? FrameIndex(e) : 0;
            var obj = AssetDatabase.LoadAssetAtPath<Object>(e.mainPath);
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
            ev.Use();
            Repaint();
        }
        else if (ev.type == EventType.ContextClick)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("경로 복사"), false, delegate { EditorGUIUtility.systemCopyBuffer = e.mainPath; });
            menu.AddItem(new GUIContent("폴더 열기"), false, delegate
            {
                var folder = AssetDatabase.LoadAssetAtPath<Object>(e.folder);
                if (folder != null) EditorGUIUtility.PingObject(folder);
            });
            menu.ShowAsContext();
            ev.Use();
        }
    }

    private void DrawFooter()
    {
        if (selected == null) return;

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label(selected.label + "  (" + selected.group + ")", EditorStyles.boldLabel, GUILayout.Width(240f));

        if (!selected.loaded || selected.frames.Count == 0)
        {
            GUILayout.Label(selected.mainPath, EditorStyles.miniLabel);
        }
        else if (playing)
        {
            GUILayout.Label(selected.mainPath, EditorStyles.miniLabel);
        }
        else
        {
            int last = selected.frames.Count - 1;
            if (GUILayout.Button("◀", GUILayout.Width(26f))) scrubFrame = Mathf.Max(0, scrubFrame - 1);
            if (GUILayout.Button("▶", GUILayout.Width(26f))) scrubFrame = Mathf.Min(last, scrubFrame + 1);
            scrubFrame = Mathf.RoundToInt(GUILayout.HorizontalSlider(scrubFrame, 0f, last));
            GUILayout.Label((scrubFrame + 1) + " / " + (last + 1), EditorStyles.miniLabel, GUILayout.Width(70f));
        }

        EditorGUILayout.EndHorizontal();
    }
}
