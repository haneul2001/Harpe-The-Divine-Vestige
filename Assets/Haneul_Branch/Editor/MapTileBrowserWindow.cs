using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// 맵 타일 브라우저 — 메뉴 [Harpe > 맵 타일 브라우저]
//
// 프로젝트의 타일을 격자로 펼쳐 놓고, 타일을 고른 뒤 역할 버튼을 누르면
// "이 타일은 울타리 왼쪽", "이 타일은 오브젝트" 같은 지정이 팔레트 에셋에 저장된다.
// 지정은 테마(타일셋 팩 = 층)별로 따로 쌓인다.
//
// 프로젝트에 타일이 8만 장 넘게 있어서(에셋 팩) 전부 열면 에디터가 멈춘다.
// 훑을 때는 경로만 모으고, 그림은 화면에 보이는 칸만 그때 읽는다.
//
// 한 타일이 여러 역할을 맡을 수 있다 (같은 울타리 조각이 ←와 → 둘 다인 경우). 역할 버튼은 켜고 끄는 토글이다.
// 칸 클릭: 선택 / 역할 버튼(단축키 1~9,0,-): 토글 / 우클릭: 메뉴 / Delete: 이 타일 지정 전부 해제
public class MapTileBrowserWindow : EditorWindow
{
    private const string PalettePath = "Assets/Haneul_Branch/Resources/Map/MapTilePalette.asset";

    // 타일셋 폴더 이름 → 테마 이름 (RafaelMatos 팩 기준). 목록에 없으면 폴더 이름을 그대로 쓴다
    private static readonly Dictionary<string, string> ThemeNameOfFolder = new Dictionary<string, string>
    {
        { "ERW - Old Prison",    "낡은 감옥" },
        { "ERW-Crypt",           "지하 묘실" },
        { "ERW - Sewers",        "지하 하수도" },
        { "ERW - Cemetery",      "묘지" },
        { "ERW-Ancient Ruins",   "고대 유적" },
        { "ERW - The Depths",    "심연" },
        { "ERW - Volcano",       "화산" },
        { "ERW-Grassland 2.0",   "초원" },
        { "ERW-Grass Land",      "초원(구)" },
        { "ERW - Highlands",     "고원" },
        { "ERW - The Village",   "마을" },
        { "ERW-Sea Adventures-GL2 Expansion", "바다" },
    };

    private class Entry
    {
        public string path;
        public string group;      // 타일셋 폴더 (테마 후보)
        public string name;
        public bool loaded;
        public TileBase tile;
        public Sprite sprite;
    }

    private readonly List<Entry> all = new List<Entry>();
    private readonly List<Entry> shown = new List<Entry>();
    private readonly HashSet<string> usedInRooms = new HashSet<string>();     // 방 프리팹이 실제로 쓰는 타일
    private readonly HashSet<string> assignedPaths = new HashSet<string>();   // 현재 테마에 지정된 타일

    private string[] groups = { "전체" };
    private int groupIndex;
    private string search = "";
    private float cellSize = 72f;
    private bool onlyUsed;
    private bool onlyAssigned;
    private bool onlyUnassigned;
    private Vector2 scroll;
    private Vector2 panelScroll;
    private int loadedThisFrame;

    private Entry selected;
    private MapTilePalette palette;
    private int themeIndex;
    private string renameBuffer = "";

    [MenuItem("Harpe/맵 타일 브라우저")]
    private static void Open()
    {
        var w = GetWindow<MapTileBrowserWindow>("맵 타일");
        w.minSize = new Vector2(760f, 440f);
        w.Rescan();
    }

    private void OnEnable()
    {
        EnsurePalette();
        if (all.Count == 0) Rescan();
    }

    private void EnsurePalette()
    {
        if (palette != null) return;

        palette = AssetDatabase.LoadAssetAtPath<MapTilePalette>(PalettePath);
        if (palette == null)
        {
            string dir = System.IO.Path.GetDirectoryName(PalettePath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(dir).Replace('\\', '/'), System.IO.Path.GetFileName(dir));

            palette = ScriptableObject.CreateInstance<MapTilePalette>();
            AssetDatabase.CreateAsset(palette, PalettePath);
            AssetDatabase.SaveAssets();
        }

        themeIndex = Mathf.Clamp(themeIndex, 0, Mathf.Max(0, palette.Themes.Count - 1));
    }

    private MapTilePalette.Theme CurrentTheme
    {
        get
        {
            if (palette == null || palette.Themes.Count == 0) return null;
            return palette.Themes[Mathf.Clamp(themeIndex, 0, palette.Themes.Count - 1)];
        }
    }

    // ────────────────────────────── 수집 ──────────────────────────────

    private void Rescan()
    {
        all.Clear();
        selected = null;

        foreach (string guid in AssetDatabase.FindAssets("t:TileBase"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;

            all.Add(new Entry
            {
                path = path,
                group = FolderOf(path),
                name = System.IO.Path.GetFileNameWithoutExtension(path),
            });
        }

        all.Sort(delegate(Entry a, Entry b)
        {
            int g = string.Compare(a.group, b.group, System.StringComparison.Ordinal);
            return g != 0 ? g : string.Compare(a.path, b.path, System.StringComparison.Ordinal);
        });

        var names = new List<string> { "전체" };
        names.AddRange(all.Select(x => x.group).Distinct().OrderBy(x => x));
        groups = names.ToArray();
        groupIndex = Mathf.Clamp(groupIndex, 0, groups.Length - 1);
        Refilter();
    }

    // 타일셋 팩 폴더 하나 = 테마 후보 (Assets/ThirdParty/RafaelMatos/<팩>/…)
    private static string FolderOf(string path)
    {
        string[] p = path.Split('/');
        if (p.Length > 4 && p[1] == "ThirdParty" && p[2] == "RafaelMatos") return p[3];
        if (p.Length > 3 && p[1] == "ThirdParty") return p[2];
        return p.Length > 2 ? p[1] + "/" + p[2] : path;
    }

    private static string ThemeNameFor(string folder)
    {
        string name;
        return ThemeNameOfFolder.TryGetValue(folder, out name) ? name : folder;
    }

    private void Refilter()
    {
        RefreshAssignedPaths();
        shown.Clear();
        string q = search != null ? search.Trim() : "";

        foreach (var e in all)
        {
            if (groupIndex > 0 && e.group != groups[groupIndex]) continue;
            if (q.Length > 0 && e.name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0
                && e.path.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (onlyUsed && !usedInRooms.Contains(e.path)) continue;

            // 여기서 타일을 열면 8만 장을 전부 읽는다 — 경로만 비교한다
            bool assigned = assignedPaths.Contains(e.path);
            if (onlyAssigned && !assigned) continue;
            if (onlyUnassigned && assigned) continue;

            shown.Add(e);
        }
    }

    private void RefreshAssignedPaths()
    {
        assignedPaths.Clear();
        var theme = CurrentTheme;
        if (theme == null) return;

        foreach (var e in theme.entries)
            if (e != null && e.tile != null) assignedPaths.Add(AssetDatabase.GetAssetPath(e.tile));
    }

    // 방 프리팹이 실제로 쓰는 타일 모으기 — "쓰는 것만" 필터와 ● 표시에 쓴다
    private void ScanRooms()
    {
        usedInRooms.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Haneul_Branch/Dungeon", "Assets/Haneul_Branch/Prefab" });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (EditorUtility.DisplayCancelableProgressBar("방에서 쓰는 타일 찾는 중", path, i / (float)guids.Length)) break;

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            foreach (var map in go.GetComponentsInChildren<Tilemap>(true))
                foreach (var pos in map.cellBounds.allPositionsWithin)
                {
                    TileBase t = map.GetTile(pos);
                    if (t != null) usedInRooms.Add(AssetDatabase.GetAssetPath(t));
                }
        }
        EditorUtility.ClearProgressBar();
    }

    private void Load(Entry e)
    {
        e.loaded = true;
        e.tile = AssetDatabase.LoadAssetAtPath<TileBase>(e.path);
        e.sprite = e.tile is Tile ? ((Tile)e.tile).sprite : null;
    }

    // ────────────────────────────── 그리기 ──────────────────────────────

    private void OnGUI()
    {
        EnsurePalette();
        loadedThisFrame = 0;

        DrawToolbar();
        DrawThemeBar();
        DrawRoleBar();

        EditorGUILayout.BeginHorizontal();
        DrawGrid();
        DrawPalettePanel();
        EditorGUILayout.EndHorizontal();

        HandleShortcuts();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("다시 훑기", EditorStyles.toolbarButton, GUILayout.Width(66f))) Rescan();

        int g = EditorGUILayout.Popup(groupIndex, groups, EditorStyles.toolbarPopup, GUILayout.Width(190f));
        if (g != groupIndex) { groupIndex = g; scroll = Vector2.zero; Refilter(); }

        string q = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(140f));
        if (q != search) { search = q; scroll = Vector2.zero; Refilter(); }

        bool used = GUILayout.Toggle(onlyUsed, "방에서 쓰는 것만", EditorStyles.toolbarButton, GUILayout.Width(100f));
        if (used != onlyUsed) { onlyUsed = used; if (used && usedInRooms.Count == 0) ScanRooms(); scroll = Vector2.zero; Refilter(); }

        bool a = GUILayout.Toggle(onlyAssigned, "지정된 것만", EditorStyles.toolbarButton, GUILayout.Width(78f));
        if (a != onlyAssigned) { onlyAssigned = a; if (a) onlyUnassigned = false; scroll = Vector2.zero; Refilter(); }

        bool u = GUILayout.Toggle(onlyUnassigned, "안 된 것만", EditorStyles.toolbarButton, GUILayout.Width(72f));
        if (u != onlyUnassigned) { onlyUnassigned = u; if (u) onlyAssigned = false; scroll = Vector2.zero; Refilter(); }

        GUILayout.Label("크기", EditorStyles.miniLabel, GUILayout.Width(26f));
        cellSize = GUILayout.HorizontalSlider(cellSize, 40f, 160f, GUILayout.Width(70f));

        GUILayout.FlexibleSpace();
        GUILayout.Label(shown.Count + " / " + all.Count, EditorStyles.miniLabel);
        if (GUILayout.Button("메모리 비우기", EditorStyles.toolbarButton, GUILayout.Width(84f)))
        {
            foreach (var e in all) { e.loaded = false; e.tile = null; e.sprite = null; }
            EditorUtility.UnloadUnusedAssetsImmediate();
        }

        EditorGUILayout.EndHorizontal();
    }

    // 테마 고르기·만들기·이름 바꾸기
    private void DrawThemeBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("테마", EditorStyles.miniBoldLabel, GUILayout.Width(30f));

        string[] names = palette.ThemeNames();
        if (names.Length == 0)
        {
            GUILayout.Label("아직 없음 — 폴더를 고르고 오른쪽 [이 폴더로 테마 만들기]", EditorStyles.miniLabel);
        }
        else
        {
            int t = EditorGUILayout.Popup(themeIndex, names, EditorStyles.toolbarPopup, GUILayout.Width(150f));
            if (t != themeIndex) { themeIndex = t; renameBuffer = ""; Refilter(); }

            var theme = CurrentTheme;
            GUILayout.Label(theme.entries.Count + "개 지정", EditorStyles.miniLabel, GUILayout.Width(56f));

            // 이름 바꾸기 — 입력칸에 쓰고 Enter 또는 [이름 적용]
            if (string.IsNullOrEmpty(renameBuffer)) renameBuffer = theme.name;
            renameBuffer = GUILayout.TextField(renameBuffer, EditorStyles.toolbarTextField, GUILayout.Width(110f));
            if (GUILayout.Button("이름 적용", EditorStyles.toolbarButton, GUILayout.Width(62f)) && renameBuffer.Trim().Length > 0)
            {
                Undo.RecordObject(palette, "테마 이름 변경");
                theme.name = renameBuffer.Trim();
                Save();
            }

            if (GUILayout.Button("테마 삭제", EditorStyles.toolbarButton, GUILayout.Width(62f))
                && EditorUtility.DisplayDialog("테마 삭제", "'" + theme.name + "' 테마의 지정을 모두 지울까?", "삭제", "취소"))
            {
                Undo.RecordObject(palette, "테마 삭제");
                palette.Themes.Remove(theme);
                themeIndex = 0;
                renameBuffer = "";
                Save();
                Refilter();
            }
        }

        GUILayout.FlexibleSpace();

        GUI.enabled = groupIndex > 0;
        string folder = groupIndex > 0 ? groups[groupIndex] : "";
        if (GUILayout.Button(groupIndex > 0 ? "이 폴더로 테마 만들기 — " + ThemeNameFor(folder) : "이 폴더로 테마 만들기",
            EditorStyles.toolbarButton, GUILayout.Width(210f)))
        {
            Undo.RecordObject(palette, "테마 추가");
            var theme = palette.GetOrCreate(ThemeNameFor(folder), folder);
            themeIndex = palette.Themes.IndexOf(theme);
            renameBuffer = "";
            Save();
            Refilter();
        }
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    // 선택한 타일에 역할을 주는 버튼 줄
    private void DrawRoleBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        if (CurrentTheme == null)
        {
            GUILayout.Label("테마를 먼저 만들어야 지정할 수 있다.", EditorStyles.miniLabel);
        }
        else if (selected == null)
        {
            GUILayout.Label("타일을 고른 뒤 역할을 누르면 '" + CurrentTheme.name + "' 테마에 지정된다. (단축키 1~9, 0, -)", EditorStyles.miniLabel);
        }
        else
        {
            if (!selected.loaded) Load(selected);
            var cur = palette.RolesOf(CurrentTheme, selected.tile);
            GUILayout.Label(selected.name + (cur.Count > 0 ? "  —  " + string.Join(", ", cur.Select(r => r.Label()).ToArray()) : ""),
                EditorStyles.boldLabel, GUILayout.Width(220f));

            var roles = MapTileRoleUtil.All;
            for (int i = 0; i < roles.Length; i++)
            {
                bool on = cur.Contains(roles[i]);
                if (GUILayout.Toggle(on, ShortcutOf(i) + " " + roles[i].Label(), EditorStyles.miniButton, GUILayout.Width(82f)) != on)
                    Toggle(roles[i]);
            }

            if (GUILayout.Button("모두 해제", EditorStyles.miniButton, GUILayout.Width(62f))) ClearSelected();
        }

        EditorGUILayout.EndHorizontal();
    }

    private static string ShortcutOf(int i)
    {
        if (i < 9) return (i + 1).ToString();
        return i == 9 ? "0" : "-";
    }

    private void HandleShortcuts()
    {
        Event ev = Event.current;
        if (selected == null || CurrentTheme == null || ev.type != EventType.KeyDown) return;
        if (EditorGUIUtility.editingTextField) return;   // 이름 입력 중에는 단축키를 먹지 않는다

        if (ev.keyCode == KeyCode.Delete || ev.keyCode == KeyCode.Backspace) { ClearSelected(); ev.Use(); return; }

        int index = -1;
        if (ev.keyCode >= KeyCode.Alpha1 && ev.keyCode <= KeyCode.Alpha9) index = ev.keyCode - KeyCode.Alpha1;
        else if (ev.keyCode == KeyCode.Alpha0) index = 9;
        else if (ev.keyCode == KeyCode.Minus) index = 10;

        var roles = MapTileRoleUtil.All;
        if (index < 0 || index >= roles.Length) return;

        Toggle(roles[index]);
        ev.Use();
    }

    // 역할 켜고 끄기 — 한 타일에 여러 역할을 줄 수 있다
    private void Toggle(MapTileRole role)
    {
        if (selected == null || CurrentTheme == null) return;
        if (!selected.loaded) Load(selected);
        if (selected.tile == null) return;

        Undo.RecordObject(palette, "타일 역할 지정");
        palette.ToggleRole(CurrentTheme, selected.tile, role);
        Save();
        Refilter();
        Repaint();
    }

    private void ClearSelected()
    {
        if (selected == null || CurrentTheme == null || selected.tile == null) return;

        Undo.RecordObject(palette, "타일 지정 해제");
        palette.ClearRoles(CurrentTheme, selected.tile);
        Save();
        Refilter();
        Repaint();
    }

    private void Save()
    {
        EditorUtility.SetDirty(palette);
        AssetDatabase.SaveAssets();
    }

    private const float LabelHeight = 28f;
    private const float PanelWidth = 250f;

    private void DrawGrid()
    {
        EditorGUILayout.BeginVertical();

        float viewWidth = position.width - PanelWidth - 26f;
        float cellH = cellSize + LabelHeight + 6f;
        int columns = Mathf.Max(1, Mathf.FloorToInt(viewWidth / (cellSize + 6f)));
        int rows = Mathf.CeilToInt(shown.Count / (float)columns);

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Width(viewWidth));
        GUILayout.Space(rows * cellH);
        Rect area = GUILayoutUtility.GetLastRect();

        int firstRow = Mathf.Max(0, Mathf.FloorToInt((scroll.y - area.y) / cellH) - 1);
        int lastRow = Mathf.Min(rows - 1, firstRow + Mathf.CeilToInt(position.height / cellH) + 1);

        for (int row = firstRow; row <= lastRow; row++)
            for (int col = 0; col < columns; col++)
            {
                int i = row * columns + col;
                if (i >= shown.Count) break;
                DrawCell(shown[i], new Rect(area.x + col * (cellSize + 6f), area.y + row * cellH, cellSize, cellSize + LabelHeight));
            }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawCell(Entry e, Rect cell)
    {
        var art = new Rect(cell.x, cell.y, cell.width, cell.width);

        // 스크롤이 튀지 않게 한 프레임에 몇 개씩만 읽는다
        if (!e.loaded)
        {
            if (loadedThisFrame >= 24)
            {
                EditorGUI.DrawRect(art, new Color(0.13f, 0.12f, 0.15f));
                GUI.Label(art, "읽는 중…", EditorStyles.centeredGreyMiniLabel);
                Repaint();
                return;
            }
            Load(e);
            loadedThisFrame++;
        }
        if (e.tile == null) return;

        var roles = CurrentTheme != null ? palette.RolesOf(CurrentTheme, e.tile) : new List<MapTileRole>();

        if (selected == e) EditorGUI.DrawRect(new Rect(art.x - 2f, art.y - 2f, art.width + 4f, art.height + 4f), new Color(1f, 0.78f, 0.3f, 0.6f));
        EditorGUI.DrawRect(art, new Color(0.13f, 0.12f, 0.15f));
        DrawTile(e, art);

        if (roles.Count > 0)
        {
            // 역할이 여럿이면 기호를 모아서 한 줄로 (예: "← → ↑")
            var tag = new Rect(art.x, art.yMax - 14f, art.width, 14f);
            EditorGUI.DrawRect(tag, new Color(0.2f, 0.45f, 0.25f, 0.85f));
            GUI.Label(tag, " " + string.Join(" ", roles.Select(r => r.Short()).ToArray()), EditorStyles.miniLabel);
        }

        string label = e.name + (usedInRooms.Contains(e.path) ? "  ●" : "");
        GUI.Label(new Rect(cell.x, art.yMax + 2f, cell.width, LabelHeight), label, EditorStyles.miniLabel);

        Event ev = Event.current;
        if (!cell.Contains(ev.mousePosition)) return;

        if (ev.type == EventType.MouseDown && ev.button == 0)
        {
            selected = e;
            Selection.activeObject = e.tile;
            EditorGUIUtility.PingObject(e.tile);
            ev.Use();
            Repaint();
        }
        else if (ev.type == EventType.ContextClick)
        {
            var menu = new GenericMenu();
            foreach (var r in MapTileRoleUtil.All)
            {
                MapTileRole captured = r;
                menu.AddItem(new GUIContent(r.Label()), roles.Contains(r), delegate { selected = e; Toggle(captured); });
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("이 타일 지정 모두 해제"), false, delegate { selected = e; ClearSelected(); });
            menu.AddItem(new GUIContent("경로 복사"), false, delegate { EditorGUIUtility.systemCopyBuffer = e.path; });
            menu.ShowAsContext();
            ev.Use();
        }
    }

    private void DrawTile(Entry e, Rect art)
    {
        // Tile이면 스프라이트를 직접 그린다 (픽셀 그대로). RuleTile 등은 에디터 미리보기로 대체
        if (e.sprite != null && e.sprite.texture != null)
        {
            Sprite s = e.sprite;
            Rect r = s.rect;
            float pad = 6f;
            float k = Mathf.Min((art.width - pad * 2f) / r.width, (art.height - pad * 2f) / r.height);
            k = k >= 1f ? Mathf.Floor(k) : k;

            var dst = new Rect(art.center.x - r.width * k * 0.5f, art.center.y - r.height * k * 0.5f, r.width * k, r.height * k);
            var uv = new Rect(r.x / s.texture.width, r.y / s.texture.height, r.width / s.texture.width, r.height / s.texture.height);
            GUI.DrawTextureWithTexCoords(dst, s.texture, uv, true);
            return;
        }

        Texture2D preview = AssetPreview.GetAssetPreview(e.tile);
        if (preview != null) GUI.DrawTexture(new Rect(art.x + 6f, art.y + 6f, art.width - 12f, art.height - 12f), preview, ScaleMode.ScaleToFit);
        else GUI.Label(art, "미리보기\n없음", EditorStyles.centeredGreyMiniLabel);
    }

    // ────────────────────────────── 오른쪽 패널 ──────────────────────────────

    private void DrawPalettePanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(PanelWidth));

        var theme = CurrentTheme;
        GUILayout.Label(theme != null ? theme.name + " — 지정된 타일" : "테마 없음", EditorStyles.boldLabel);

        panelScroll = EditorGUILayout.BeginScrollView(panelScroll);
        if (theme != null)
        {
            foreach (var role in MapTileRoleUtil.All)
            {
                var tiles = palette.Of(theme, role);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(role.Label(), EditorStyles.miniBoldLabel, GUILayout.Width(80f));
                GUILayout.Label(tiles.Count == 0 ? "—" : tiles.Count + "개", EditorStyles.miniLabel, GUILayout.Width(30f));
                EditorGUILayout.EndHorizontal();

                foreach (var t in tiles)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(12f);
                    if (GUILayout.Button(t.name, EditorStyles.miniLabel)) { Selection.activeObject = t; EditorGUIUtility.PingObject(t); }
                    if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(20f)))
                    {
                        // 이 역할에서만 뺀다 (다른 역할 지정은 남는다)
                        Undo.RecordObject(palette, "타일 지정 해제");
                        palette.ToggleRole(theme, t, role);
                        Save();
                        Refilter();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (theme != null)
            EditorGUILayout.HelpBox(palette.HasFullFence(theme)
                ? "울타리 8종이 모두 지정됐다. 이 테마로 사각형 울타리를 그릴 수 있다."
                : "울타리 8종(네 변 + 네 모서리)을 모두 지정하면 사각형 울타리를 그릴 수 있다.",
                palette.HasFullFence(theme) ? MessageType.Info : MessageType.Warning);

        if (GUILayout.Button("팔레트 에셋 선택")) Selection.activeObject = palette;

        EditorGUILayout.EndVertical();
    }
}
