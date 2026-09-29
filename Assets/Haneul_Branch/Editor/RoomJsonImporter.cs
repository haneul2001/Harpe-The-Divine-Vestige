using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Tools/RoomGen(파이썬)이 뽑은 방 JSON을 방 프리팹으로 만든다.
//
// 타일·소품 배치는 파이썬이 다 정하고(팩의 오토타일 규칙을 거기서 돌린다),
// 여기서는 기존 방 프리팹을 본으로 떠서 크기·문·카메라 경계·스폰만 맞추고 타일을 채운다.
// 문 그래픽·콜라이더·Room 설정은 본 프리팹 것을 그대로 물려받는다 — 규격이 한곳에만 있어야 한다.
public static class RoomJsonImporter
{
    [System.Serializable] public class Cell { public int x, y; public string a; public int f; }
    [System.Serializable] public class Layer { public string name; public List<Cell> cells; }
    [System.Serializable] public class Prop { public string sprite; public float x, y; public bool passable; public string broken; public int goldMin, goldMax; public float fps; }
    [System.Serializable] public class Spawn { public float x, y; }
    [System.Serializable] public class Upright { public List<Cell> cells; public bool full; }
    [System.Serializable] public class PatchCell { public string m; public int x, y; public string a; public int f; }
    [System.Serializable] public class DoorSpec { public string dir; public int sub; public string plug; public float plugX, plugY; public float colX, colY, colW, colH; public List<PatchCell> patch; }
    [System.Serializable]
    public class RoomJson
    {
        public string name, kind;
        public int width, height, originX, originY;
        public int spanX = 1, spanY = 1;
        public float wallBottom;
        public List<DoorSpec> doors;
        public List<Spawn> spawns;
        public List<Layer> tilemaps;
        public List<Prop> props;
        public List<Upright> uprights;
    }

    // Tiled 뒤집기 플래그 (gid 상위 비트)
    const uint FlipH = 0x80000000, FlipV = 0x40000000, FlipD = 0x20000000;

    public static string Import(string jsonPath, string templatePrefabPath, string outPrefabPath)
    {
        var data = JsonUtility.FromJson<RoomJson>(File.ReadAllText(jsonPath));
        if (data == null) return "JSON을 못 읽었다: " + jsonPath;

        GameObject root = PrefabUtility.LoadPrefabContents(templatePrefabPath);
        try
        {
            root.name = data.name;
            var room = root.GetComponent<Room>();
            var so = new SerializedObject(room);
            so.FindProperty("gizmoRoomSize").vector2Value = new Vector2(data.width, data.height);
            so.ApplyModifiedPropertiesWithoutUndo();

            // ① 타일
            var maps = new Dictionary<string, Tilemap>();
            foreach (var tm in root.GetComponentsInChildren<Tilemap>(true))
            {
                foreach (string key in new[] { "Ground", "Wall", "Object", "Props" })
                    if (tm.name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0) maps[key] = tm;
            }
            // 막는 소품은 벽 타일맵에 같이 두면 그 자리 벽 타일을 지워 버린다 — 별도 타일맵(콜라이더 있음)에 둔다
            if (!maps.ContainsKey("Props") && maps.ContainsKey("Wall"))
            {
                Tilemap wall = maps["Wall"];
                var go = new GameObject("Props TileMap", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
                go.transform.SetParent(wall.transform.parent, false);
                go.layer = wall.gameObject.layer;
                var wr = wall.GetComponent<TilemapRenderer>();
                var pr = go.GetComponent<TilemapRenderer>();
                pr.sortingLayerName = wr.sortingLayerName;
                pr.sortingOrder = wr.sortingOrder + 1;
                pr.mode = wr.mode;
                maps["Props"] = go.GetComponent<Tilemap>();
            }
            int placed = 0, missing = 0;
            foreach (var layer in data.tilemaps)
            {
                if (!maps.TryGetValue(layer.name, out Tilemap tm)) continue;
                tm.ClearAllTiles();
                foreach (var c in layer.cells)
                {
                    var tile = ResolveTile(c.a);
                    if (tile == null) { missing++; continue; }
                    var pos = new Vector3Int(c.x, c.y, 0);
                    tm.SetTile(pos, tile);
                    Matrix4x4 m = c.f != 0 ? FlipMatrix((uint)c.f) : Matrix4x4.identity;
                    // 소품 아틀라스 스프라이트는 피벗이 왼쪽 아래라 그대로 두면 반 칸 밀려 그려진다 — 가운데로 당긴다
                    Matrix4x4 pv = PivotFix(tile);
                    if (pv != Matrix4x4.identity || c.f != 0) tm.SetTransformMatrix(pos, pv * m);
                    placed++;
                }
                tm.CompressBounds();
            }

            // 타일이 실제로 깔린 자리의 가운데. 높이가 짝수 칸(세로 2칸 방 등)이면 Grid가 0.5 올라가 있는 탓에
            // 타일 전체가 방 중심보다 반 칸 아래에 놓인다 — 카메라 범위는 방 중심이 아니라 여기에 맞춘다.
            // 벽 타일맵이 방 테두리 전체를 두르므로 그걸로 잰다 (바닥은 위쪽 벽 줄 아래에 없어서 치우친다)
            Vector2 content = Vector2.zero;
            if (maps.TryGetValue("Wall", out Tilemap frameMap) && frameMap.cellBounds.size.x > 0)
            {
                var gb = frameMap.cellBounds;
                Vector3 grid = root.transform.InverseTransformPoint(frameMap.layoutGrid.transform.position);
                content = new Vector2((gb.xMin + gb.xMax) * 0.5f + grid.x, (gb.yMin + gb.yMax) * 0.5f + grid.y);
            }

            // 방 밖이 검게 보이지 않게 가장자리 타일을 바깥으로 두 줄 이어 깐다.
            // 화면(11.25칸)이 1칸 방(11칸)보다 높아 위아래 끝이 늘 조금 비친다. 옆방은 꺼져 있어 겹칠 일이 없다.
            foreach (var key in new[] { "Ground", "Wall" })
                if (maps.TryGetValue(key, out Tilemap padMap)) PadOutside(padMap, 2);

            // ② 문 — JSON이 요구하는 (방향, 칸) 마다 하나씩. 본 프리팹의 같은 방향 문을 복제해서 쓴다.
            //    막힌 문은 본 프리팹처럼 벽 그림(plug)을 덮는다 — 파이썬이 그 자리 벽을 그려 둔 스프라이트다.
            var rso0 = new SerializedObject(room);
            rso0.FindProperty("cellSpan").vector2IntValue = new Vector2Int(Mathf.Max(1, data.spanX), Mathf.Max(1, data.spanY));
            rso0.FindProperty("doors").arraySize = 0;   // 자식에서 다시 모으게 비운다
            rso0.ApplyModifiedPropertiesWithoutUndo();

            float halfW = data.width * 0.5f, halfH = data.height * 0.5f;
            var templates = new Dictionary<Dir, Door>();
            foreach (var d in root.GetComponentsInChildren<Door>(true)) if (!templates.ContainsKey(d.dir)) templates[d.dir] = d;
            Transform doorsRoot = templates.Count > 0 ? templates[new List<Dir>(templates.Keys)[0]].transform.parent : root.transform;

            var made = new List<GameObject>();
            if (data.doors != null)
                foreach (var spec in data.doors)
                {
                    Dir ddir = ParseDir(spec.dir);
                    if (!templates.TryGetValue(ddir, out Door tpl)) { missing++; continue; }
                    var go = Object.Instantiate(tpl.gameObject, doorsRoot);
                    go.name = "Door_" + ddir + (spec.sub > 0 ? "_" + spec.sub : "");
                    var door = go.GetComponent<Door>();
                    // sub -1 = 변 한가운데 문 (여러 칸짜리 특수 방의 단일 입구). 생성기는 입구 방향 문이 하나면 입구 칸에 맞춰 잇는다
                    bool centered = spec.sub < 0;
                    door.subCell = Mathf.Max(0, spec.sub);
                    if (centered) go.name = "Door_" + ddir;
                    Vector3 p;
                    if (centered)
                        p = ddir == Dir.Up ? new Vector3(0f, halfH - 0.5f, 0f)
                          : ddir == Dir.Down ? new Vector3(0f, -halfH + 0.5f, 0f)
                          : ddir == Dir.Left ? new Vector3(-halfW + 0.5f, 0f, 0f) : new Vector3(halfW - 0.5f, 0f, 0f);
                    else
                    switch (ddir)
                    {
                        case Dir.Up:    p = new Vector3(-halfW + 10f + spec.sub * 20f, halfH - 0.5f, 0f); break;
                        case Dir.Down:  p = new Vector3(-halfW + 10f + spec.sub * 20f, -halfH + 0.5f, 0f); break;
                        // 옆문 구멍(3~8행)의 가운데는 칸 중심보다 반 칸 아래 (위쪽 벽이 3줄)
                        case Dir.Left:  p = new Vector3(-halfW + 0.5f, -halfH + 5.0f + spec.sub * 11f, 0f); break;
                        default:        p = new Vector3(halfW - 0.5f, -halfH + 5.0f + spec.sub * 11f, 0f); break;
                    }
                    go.transform.localPosition = p;

                    // 구멍 6칸(보이는 트임 5칸): 트리거·막이 콜라이더를 구멍 폭에 맞춘다
                    bool horiz = ddir == Dir.Up || ddir == Dir.Down;
                    var trig = go.GetComponent<BoxCollider2D>();
                    if (trig != null) { trig.size = horiz ? new Vector2(6.4f, 0.7f) : new Vector2(0.7f, 6.4f); trig.offset = Vector2.zero; }
                    Transform blk = go.transform.Find("Blocker");
                    var blkCol = blk != null ? blk.GetComponent<BoxCollider2D>() : null;
                    if (blkCol != null) { blkCol.size = horiz ? new Vector2(6.2f, 0.6f) : new Vector2(0.6f, 6.2f); blkCol.offset = Vector2.zero; blk.localPosition = Vector3.zero; }

                    // 잠김 그림: Depths 보스 창살 (위·아래 문은 가로형, 왼·오른쪽 문은 세로형)
                    Transform oldClosed = go.transform.Find("ClosedVisual");
                    if (oldClosed != null) Object.DestroyImmediate(oldClosed.gameObject);
                    // 막힐 때 바꿔 끼울 타일
                    var wp = new List<Door.TilePatch>();
                    if (spec.patch != null)
                        foreach (var pc in spec.patch)
                        {
                            if (!maps.TryGetValue(pc.m, out Tilemap ptm)) continue;
                            TileBase pt = string.IsNullOrEmpty(pc.a) ? null : ResolveTile(pc.a);
                            if (!string.IsNullOrEmpty(pc.a) && pt == null) { missing++; continue; }
                            Matrix4x4 pm = pc.f != 0 ? FlipMatrix((uint)pc.f) : Matrix4x4.identity;
                            if (pt != null) pm = PivotFix(pt) * pm;
                            wp.Add(new Door.TilePatch { map = ptm, pos = new Vector3Int(pc.x, pc.y, 0), tile = pt, matrix = pm });
                        }
                    door.EditorSetWallPatch(wp);

                    float wallBottom = data.wallBottom < -0.1f ? data.wallBottom : -2.5f;   // 옛 JSON엔 없다
                    door.wallBottom = wallBottom;
                    MakeGate(go, door, ddir, WallLayer(root), WallOrder(root) + 3, wallBottom);

                    // 벽 그림: 이웃 방이 없어 막힐 때 Door.DisableAsWall이 켠다
                    Transform oldWall = go.transform.Find("WallVisual");
                    if (oldWall != null) Object.DestroyImmediate(oldWall.gameObject);
                    if (!string.IsNullOrEmpty(spec.plug))
                    {
                        var plugSprite = LoadPlugSprite(spec.plug);
                        if (plugSprite != null)
                        {
                            var wv = new GameObject("WallVisual");
                            wv.transform.SetParent(go.transform, false);
                            wv.transform.localPosition = new Vector3(spec.plugX, spec.plugY, 0f);
                            var sr = wv.AddComponent<SpriteRenderer>();
                            sr.sprite = plugSprite;
                            // 벽과 같은 그리기 층 — 소품 층(Prop)은 플레이어보다 위라 캐릭터가 벽 그림 밑으로 가려졌다
                            sr.sortingLayerName = WallLayer(root);
                            sr.sortingOrder = WallOrder(root) + 2;
                            // 그림만 있고 충돌이 문 한가운데 얇은 띠뿐이면 벽 그림 안으로 걸어 들어간다 — 새로 생긴 벽 칸 범위만큼 막는다
                            if (spec.colW > 0f && spec.colH > 0f)
                            {
                                wv.layer = LayerMask.NameToLayer("Wall");
                                var wcol = wv.AddComponent<BoxCollider2D>();
                                wcol.offset = new Vector2(spec.colX - spec.plugX, spec.colY - spec.plugY);
                                wcol.size = new Vector2(spec.colW, spec.colH);
                            }
                            wv.SetActive(false);
                            var dso = new SerializedObject(door);
                            dso.FindProperty("wallVisual").objectReferenceValue = wv;
                            dso.ApplyModifiedPropertiesWithoutUndo();
                        }
                        else missing++;
                    }
                    made.Add(go);
                }
            foreach (var tpl in templates.Values) Object.DestroyImmediate(tpl.gameObject);

            // ③ 카메라 경계 — 20x11 한 칸 방은 가운데 고정, 더 크면 그만큼 따라다닌다
            SetLocal(root, "CameraBounds/CamXMin", new Vector3(content.x - (halfW - 10f), 0f, 0f));
            SetLocal(root, "CameraBounds/CamXMax", new Vector3(content.x + halfW - 10f, 0f, 0f));
            SetLocal(root, "CameraBounds/CamYMin", new Vector3(0f, content.y - (halfH - 5.5f), 0f));
            SetLocal(root, "CameraBounds/CamYMax", new Vector3(0f, content.y + halfH - 5.5f, 0f));

            // ④ 스폰 — 본에 있던 자리는 버리고 JSON대로
            Transform spawnRoot = root.transform.Find("SpawnPoints");
            if (spawnRoot != null && data.spawns != null && data.spawns.Count > 0)
            {
                for (int i = spawnRoot.childCount - 1; i >= 0; i--) Object.DestroyImmediate(spawnRoot.GetChild(i).gameObject);
                for (int i = 0; i < data.spawns.Count; i++)
                {
                    var go = new GameObject("Spawn_" + i);
                    go.transform.SetParent(spawnRoot, false);
                    go.transform.localPosition = new Vector3(data.spawns[i].x, data.spawns[i].y, 0f);
                }
                var rso = new SerializedObject(room);
                var arr = rso.FindProperty("spawnPoints");
                arr.arraySize = data.spawns.Count;
                for (int i = 0; i < data.spawns.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = spawnRoot.GetChild(i);
                rso.ApplyModifiedPropertiesWithoutUndo();
            }

            // ⑤ 소품 — 본의 소품은 지우고 JSON 것으로. 발밑 기준 피벗, 아래쪽 한 칸만 막는다
            Transform props = root.transform.Find("Props");
            if (props == null) { props = new GameObject("Props").transform; props.SetParent(root.transform, false); }
            for (int i = props.childCount - 1; i >= 0; i--) Object.DestroyImmediate(props.GetChild(i).gameObject);
            // 서 있는 소품은 캐릭터와 같은 그리기 층에서 발밑(y)으로 앞뒤를 가린다 (CharacterSorting과 같은 층).
            // 충돌은 맨 아래 한 칸만 — 그 위로는 캐릭터가 소품 뒤로 지나가며 가려진다
            int wallLayer = LayerMask.NameToLayer("Wall");
            int propCount = 0;

            // ① 선 소품(아틀라스 블록: 석상·관·촛대·받침대·의자) — 타일 조각들을 한 묶음으로
            if (data.uprights != null)
                foreach (var up in data.uprights)
                {
                    if (up.cells == null || up.cells.Count == 0) continue;
                    int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue;
                    foreach (var c in up.cells) { minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); minY = Mathf.Min(minY, c.y); }
                    // 발밑 = 맨 아래 줄 칸의 아래 변. 칸 (cx, cy)는 세계 x [cx, cx+1], y [cy+0.5, cy+1.5] (Grid가 0.5 올라가 있다)
                    Vector2 foot = new Vector2((minX + maxX + 1) * 0.5f, minY + 0.5f);
                    var go = new GameObject("Upright");
                    go.transform.SetParent(props, false);
                    go.transform.localPosition = foot;
                    go.layer = wallLayer;
                    var sg = go.AddComponent<UnityEngine.Rendering.SortingGroup>();
                    sg.sortingLayerName = CharacterSorting.Layer; sg.sortingOrder = CharacterSorting.Order;
                    foreach (var c in up.cells)
                    {
                        var tb = ResolveTile(c.a) as Tile;
                        if (tb == null || tb.sprite == null) { missing++; continue; }
                        var piece = new GameObject("Tile");
                        piece.transform.SetParent(go.transform, false);
                        Vector2 pv = tb.sprite.pivot / tb.sprite.pixelsPerUnit;
                        piece.transform.localPosition = new Vector3(c.x + pv.x - foot.x, c.y + 0.5f + pv.y - foot.y, 0f);
                        var psr = piece.AddComponent<SpriteRenderer>();
                        psr.sprite = tb.sprite;
                    }
                    float w = maxX - minX + 1;
                    var col = go.AddComponent<BoxCollider2D>();
                    col.size = new Vector2(w * 0.9f, 1f);
                    col.offset = new Vector2(0f, 0.5f);
                    LiftUprightToVisible(go.transform);
                    if (up.full) BlockWholeUpright(go.transform);
                    propCount++;
                }

            // ② 그림 소품(낱개 스프라이트) — 발밑에 묶음 뿌리를 두고 그림은 그 위로
            if (data.props != null)
                foreach (var pr in data.props)
                {
                    int hash = pr.sprite.IndexOf('#');
                    if (hash < 0) EnsurePpu32(pr.sprite);
                    // 여러 장으로 잘린 시트(횃불·거품 캡슐 등)는 첫 프레임을 그림으로 쓰고, fps가 있으면 반복 재생한다
                    Sprite[] frames = null;
                    if (hash < 0 && AssetImporter.GetAtPath(pr.sprite) is TextureImporter timp && timp.spriteImportMode == SpriteImportMode.Multiple)
                        frames = SheetFrames(pr.sprite);
                    var sp = frames != null && frames.Length > 0 ? frames[0]
                           : hash < 0 ? AssetDatabase.LoadAssetAtPath<Sprite>(pr.sprite)
                                      : FindSprite(pr.sprite.Substring(0, hash), pr.sprite.Substring(hash + 1));
                    if (sp == null) { missing++; continue; }
                    string nm = Path.GetFileNameWithoutExtension(pr.sprite);
                    float hUnits = sp.rect.height / sp.pixelsPerUnit;
                    float wUnits = sp.rect.width / sp.pixelsPerUnit;

                    if (pr.passable)
                    {
                        // 밟고 지나가는 깔개류는 바닥 위, 캐릭터 아래
                        var flat = new GameObject(nm);
                        flat.transform.SetParent(props, false);
                        flat.transform.localPosition = new Vector3(pr.x, pr.y + hUnits * 0.5f, 0f);
                        var fsr = flat.AddComponent<SpriteRenderer>();
                        fsr.sprite = sp;
                        fsr.sortingLayerName = WallLayer(root);
                        fsr.sortingOrder = WallOrder(root) + 1;
                        AddLoop(flat, frames, pr.fps);
                        propCount++;
                        continue;
                    }

                    var rootGo = new GameObject(nm);
                    rootGo.transform.SetParent(props, false);
                    rootGo.transform.localPosition = new Vector3(pr.x, pr.y, 0f);   // 발밑
                    var group = rootGo.AddComponent<UnityEngine.Rendering.SortingGroup>();
                    group.sortingLayerName = CharacterSorting.Layer; group.sortingOrder = CharacterSorting.Order;

                    var go = new GameObject("Sprite");
                    go.transform.SetParent(rootGo.transform, false);
                    go.transform.localPosition = new Vector3(0f, hUnits * 0.5f, 0f);
                    go.layer = wallLayer;
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sp;
                    AddLoop(go, frames, pr.fps);

                    // 충돌: 보이는 그림의 맨 아래 한 칸(1유닛)만. 그림이 1칸보다 낮으면 그 높이만큼
                    Bounds vb = VisibleBounds(sp);
                    float colH = Mathf.Min(1f, vb.size.y);
                    var col = go.AddComponent<BoxCollider2D>();
                    col.size = new Vector2(Mathf.Max(0.4f, vb.size.x * 0.9f), colH);
                    col.offset = new Vector2(vb.center.x, vb.min.y + colH * 0.5f);

                    // 부서지는 상자: 부서진 그림이 짝으로 있으면 체력 100, 골드는 방 데이터(없으면 5~10)
                    if (!string.IsNullOrEmpty(pr.broken))
                    {
                        EnsurePpu32(pr.broken);
                        var bs = AssetDatabase.LoadAssetAtPath<Sprite>(pr.broken);
                        if (bs == null) missing++;
                        var br = go.AddComponent<Breakable>();
                        br.brokenSprite = bs;
                        br.maxHp = 100;
                        br.goldMin = pr.goldMax > 0 ? pr.goldMin : 5;
                        br.goldMax = pr.goldMax > 0 ? pr.goldMax : 10;
                    }
                    propCount++;
                }

            string dir = Path.GetDirectoryName(outPrefabPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir)) Directory.CreateDirectory(dir);
            PrefabUtility.SaveAsPrefabAsset(root, outPrefabPath);
            return data.name + " → " + outPrefabPath + " (타일 " + placed + ", 소품 " + propCount + ", 못 찾은 에셋 " + missing + ")";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 스프라이트 피벗이 가운데가 아니면 타일 칸 가운데로 오도록 옮기는 행렬
    private static Matrix4x4 PivotFix(TileBase tb)
    {
        var t = tb as Tile;
        if (t == null || t.sprite == null) return Matrix4x4.identity;
        Vector2 size = t.sprite.rect.size;
        Vector2 pivot = t.sprite.pivot;   // 픽셀
        float ppu = t.sprite.pixelsPerUnit;
        float dx = (size.x * 0.5f - pivot.x) / ppu, dy = (size.y * 0.5f - pivot.y) / ppu;
        if (Mathf.Abs(dx) < 0.001f && Mathf.Abs(dy) < 0.001f) return Matrix4x4.identity;
        return Matrix4x4.Translate(new Vector3(-dx, -dy, 0f));
    }

    // 타일 참조 두 가지:
    //   "Assets/.../x.asset"          — 팩이 이미 만들어 둔 Tile 에셋
    //   "Assets/.../atlas.png#Name"   — 아틀라스의 스프라이트 하나. 팩에 Tile 에셋이 없는 소품 아틀라스용.
    //                                   처음 쓸 때 Tile 에셋을 만들어 Generated 폴더에 둔다 (한 번만).
    private const string GeneratedDir = "Assets/Haneul_Branch/Tilesets/Generated";

    private static TileBase ResolveTile(string reference)
    {
        if (string.IsNullOrEmpty(reference)) return null;
        int hash = reference.IndexOf('#');
        if (hash < 0) return AssetDatabase.LoadAssetAtPath<TileBase>(reference);

        string texPath = reference.Substring(0, hash);
        string spriteName = reference.Substring(hash + 1);
        // 팩이 달라도 스프라이트 이름이 겹친다(Tileset-Terrain_10 등) — 그림 이름을 앞에 붙여 구분한다
        string tilePath = GeneratedDir + "/" + Path.GetFileNameWithoutExtension(texPath) + "__" + spriteName + ".asset";

        var existing = AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);
        if (existing != null) return existing;

        Sprite sprite = FindSprite(texPath, spriteName);
        if (sprite == null) return null;

        if (!AssetDatabase.IsValidFolder(GeneratedDir))
        {
            Directory.CreateDirectory(GeneratedDir);
            AssetDatabase.Refresh();
        }
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.Sprite;
        AssetDatabase.CreateAsset(tile, tilePath);
        return tile;
    }

    public static Sprite FindSprite(string texPath, string spriteName)
    {
        foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(texPath))
        {
            var s = o as Sprite;
            if (s != null && s.name == spriteName) return s;
        }
        return null;
    }

    // 파이썬이 방금 써 둔 PNG는 아직 텍스처 설정이 안 되어 있다 — 스프라이트·PPU 32·Point로 맞춘 뒤 읽는다
    private static Sprite LoadPlugSprite(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { AssetDatabase.ImportAsset(path); imp = AssetImporter.GetAtPath(path) as TextureImporter; }
        if (imp == null) return null;
        if (imp.textureType != TextureImporterType.Sprite || !Mathf.Approximately(imp.spritePixelsPerUnit, 32f) || imp.filterMode != FilterMode.Point)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 32f;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // 팩의 낱개 소품 그림은 PPU가 100이라 그대로 두면 3배 작게 나온다. 처음 쓸 때 32로 맞춘다
    private static void EnsurePpu32(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        bool ok = imp.textureType == TextureImporterType.Sprite && Mathf.Approximately(imp.spritePixelsPerUnit, 32f) && imp.filterMode == FilterMode.Point;
        if (ok) return;
        imp.textureType = TextureImporterType.Sprite;
        if (imp.spriteImportMode == SpriteImportMode.None) imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 32f;
        imp.filterMode = FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
    }

    // Depths 보스 창살 시트. 가로형은 4.9칸 폭, 세로형은 바닥 자국이 4.6칸 높이 — 4칸 구멍에 기둥이 반 칸씩 벽에 묻힌다
    private const string GateH = "Assets/ThirdParty/RafaelMatos/ERW - The Depths/Props/Animated props/boss gate-going down.png";
    private const string GateV = "Assets/ThirdParty/RafaelMatos/ERW - The Depths/Props/Animated props/boss gate-going down-vertical.png";

    // 시트 소품을 제자리에서 반복 재생한다 (Animator 없이 SpriteLoop로)
    private static void AddLoop(GameObject go, Sprite[] frames, float fps)
    {
        if (frames == null || frames.Length < 2 || fps <= 0f) return;
        var loop = go.AddComponent<SpriteLoop>();
        loop.loop = frames;
        loop.fps = fps;
    }

    // 아틀라스 블록의 맨 아래 줄이 통째로 비어 있으면(1층 큰 석상 2x3 — 맨 아래 줄은 투명) 그 줄을 발밑·충돌에서 뺀다.
    // 빼지 않으면 보이지 않는 줄이 길을 막고, 발밑이 한 칸 아래라 그 줄에 선 캐릭터를 석상이 가린다.
    // 돌려주는 값은 올린 줄 수
    public static int LiftUprightToVisible(Transform up)
    {
        var rows = new SortedDictionary<int, bool>();     // 줄 번호(0 = 맨 아래) → 보이는 그림이 있는지
        foreach (var sr in up.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null) continue;
            float pvY = sr.sprite.pivot.y / sr.sprite.pixelsPerUnit;
            int row = Mathf.RoundToInt(sr.transform.localPosition.y - pvY);
            bool vis = !IsBlank(sr.sprite);
            bool prev;
            rows[row] = (rows.TryGetValue(row, out prev) && prev) || vis;
        }
        int lift = 0;
        foreach (var kv in rows) { if (kv.Value) break; lift++; }
        if (lift == 0 || lift >= rows.Count) return 0;

        up.localPosition += new Vector3(0f, lift, 0f);
        foreach (Transform piece in up) piece.localPosition -= new Vector3(0f, lift, 0f);
        return lift;
    }

    // 누운 물건(관)은 윗부분 뒤로도 못 지나간다 — 충돌을 보이는 그림 전체 높이로 늘린다
    public static void BlockWholeUpright(Transform up)
    {
        var col = up.GetComponent<BoxCollider2D>();
        if (col == null) return;
        int top = 0;
        foreach (var sr in up.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null || IsBlank(sr.sprite)) continue;
            float pvY = sr.sprite.pivot.y / sr.sprite.pixelsPerUnit;
            int row = Mathf.RoundToInt(sr.transform.localPosition.y - pvY);
            top = Mathf.Max(top, row + 1);
        }
        if (top <= 0) return;
        col.size = new Vector2(col.size.x, top);
        col.offset = new Vector2(col.offset.x, top * 0.5f);
    }

    private static readonly Dictionary<string, Texture2D> readableCopies = new Dictionary<string, Texture2D>();

    // 스프라이트 칸이 완전히 투명한지. 팩 텍스처는 읽기 설정이 꺼져 있으므로 파일을 직접 읽은 사본으로 본다
    private static bool IsBlank(Sprite s)
    {
        string path = AssetDatabase.GetAssetPath(s.texture);
        Texture2D tex;
        if (!readableCopies.TryGetValue(path, out tex))
        {
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path))) tex = null;
            readableCopies[path] = tex;
        }
        if (tex == null) return false;
        Rect r = s.rect;
        Color32[] px = tex.GetPixels32();
        for (int y = (int)r.y; y < (int)(r.y + r.height); y++)
            for (int x = (int)r.x; x < (int)(r.x + r.width); x++)
                if (px[y * tex.width + x].a > 8) return false;
        return true;
    }

    public static Sprite[] SheetFrames(string texPath)
    {
        EnsurePpu32(texPath);
        var list = new List<Sprite>();
        foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(texPath))
            if (o is Sprite s) list.Add(s);
        list.Sort((a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));
        return list.ToArray();
    }

    private static int FrameIndex(string name)
    {
        int i = name.LastIndexOf('_');
        return i >= 0 && int.TryParse(name.Substring(i + 1), out int n) ? n : 0;
    }

    // 벽 타일맵의 그리기 층·순서 (막힌 문 벽 그림과 창살을 벽과 같은 층에 둔다)
    private static string WallLayer(GameObject root)
    {
        foreach (var r in root.GetComponentsInChildren<TilemapRenderer>(true))
            if (r.name.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0) return r.sortingLayerName;
        return "Default";
    }

    private static int WallOrder(GameObject root)
    {
        int order = 1;
        foreach (var r in root.GetComponentsInChildren<TilemapRenderer>(true)) order = Mathf.Max(order, r.sortingOrder);
        return order - 1 < 1 ? 1 : order - 1;   // 가장 위 타일맵(소품·Object)보다 하나 아래 = 벽 순서 근처. 호출하는 쪽이 +로 올린다
    }

    private static void MakeGate(GameObject doorGo, Door door, Dir ddir, string sortLayer, int order, float wallBottom)
    {
        bool horiz = ddir == Dir.Up || ddir == Dir.Down;
        var frames = SheetFrames(horiz ? GateH : GateV);
        if (frames.Length == 0) return;
        var cv = new GameObject("ClosedVisual");
        cv.transform.SetParent(doorGo.transform, false);
        // 시트 스프라이트의 피벗이 제각각(가로형은 밑판 왼쪽 아래, 세로형은 자국 왼쪽 위)이라 프레임 안 기준점을 잡아 문 기준 목표점에 맞춘다.
        //   가로형 224x128: 밑판 바닥 가운데 (112, 15) → 위쪽 문은 벽 앞면 밑선(문에서 -2.5), 아래쪽 문은 바닥 끝선(+0.5)
        //   세로형 224x323: 바닥 자국 가운데 (108, 137) → 문 자리(구멍 가운데)
        Vector2 anchorPx, target;
        switch (ddir)
        {
            case Dir.Up:   anchorPx = new Vector2(112f, 15f);  target = new Vector2(0f, wallBottom); break;   // 위쪽 벽의 보이는 밑선
            case Dir.Down: anchorPx = new Vector2(112f, 15f);  target = new Vector2(0f, 0.5f); break;   // 아래쪽 문은 방 바닥 끝선(밴드 윗선)에
            default:       anchorPx = new Vector2(108f, 137f); target = Vector2.zero; break;
        }
        Vector2 off = (anchorPx - frames[0].pivot) / frames[0].pixelsPerUnit;
        cv.transform.localPosition = new Vector3(target.x - off.x, target.y - off.y, 0f);
        var sr = cv.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sortingLayerName = sortLayer;
        sr.sortingOrder = order;
        var gv = cv.AddComponent<DoorGateVisual>();
        gv.openFrames = frames;
        gv.fps = 10f;
        gv.raiseAnimated = true;
        gv.stayWhenOpen = false;   // 다 내려가면 숨긴다 — 바닥에 남는 받침 자국이 열린 문 앞을 가로질러 거슬린다
        cv.SetActive(false);
        var dso = new SerializedObject(door);
        dso.FindProperty("closedVisual").objectReferenceValue = cv;
        dso.ApplyModifiedPropertiesWithoutUndo();
    }

    // 타일맵 바깥 테두리를 n줄 이어 붙인다. 모서리까지 채우려고 위아래를 먼저 늘린 뒤 좌우를 늘린다.
    // 문 구멍 자리는 바닥이 이어져 복도가 계속되는 것처럼 보인다.
    private static void PadOutside(Tilemap tm, int n)
    {
        tm.CompressBounds();
        BoundsInt b = tm.cellBounds;
        if (b.size.x <= 0 || b.size.y <= 0) return;

        for (int x = b.xMin; x < b.xMax; x++)
        {
            CopyOut(tm, new Vector3Int(x, b.yMax - 1, 0), Vector3Int.up, n);
            CopyOut(tm, new Vector3Int(x, b.yMin, 0), Vector3Int.down, n);
        }
        for (int y = b.yMin - n; y < b.yMax + n; y++)
        {
            CopyOut(tm, new Vector3Int(b.xMin, y, 0), Vector3Int.left, n);
            CopyOut(tm, new Vector3Int(b.xMax - 1, y, 0), Vector3Int.right, n);
        }
        tm.CompressBounds();
    }

    private static void CopyOut(Tilemap tm, Vector3Int from, Vector3Int step, int n)
    {
        // 가장자리 칸이 비어 있으면(문 구멍 자리) 안쪽으로 몇 칸 들어가 찾은 타일을 쓴다
        Vector3Int src = from;
        TileBase t = tm.GetTile(src);
        for (int i = 1; t == null && i <= 4; i++) { src = from - step * i; t = tm.GetTile(src); }
        if (t == null) return;
        Matrix4x4 m = tm.GetTransformMatrix(src);
        for (int i = 1; i <= n; i++)
        {
            Vector3Int p = from + step * i;
            if (tm.GetTile(p) != null) continue;
            tm.SetTile(p, t);
            tm.SetTransformMatrix(p, m);
        }
    }

    // 스프라이트에서 실제로 그려지는(투명 아닌) 범위 — 메시 꼭짓점 기준 (Tight 메시). 스프라이트 로컬 좌표
    private static Bounds VisibleBounds(Sprite sp)
    {
        var v = sp.vertices;
        if (v == null || v.Length == 0) return sp.bounds;
        Vector2 mn = v[0], mx = v[0];
        foreach (var p in v) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
        var b = new Bounds();
        b.SetMinMax(mn, mx);
        return b;
    }

    private static Dir ParseDir(string s)
    {
        switch ((s ?? "").ToLowerInvariant())
        {
            case "up": return Dir.Up;
            case "down": return Dir.Down;
            case "left": return Dir.Left;
            default: return Dir.Right;
        }
    }

    private static void SetLocal(GameObject root, string path, Vector3 p)
    {
        Transform t = root.transform.Find(path);
        if (t != null) t.localPosition = p;
    }

    private static string FindSortingLayer(GameObject root)
    {
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            if (sr.transform.parent != null && sr.transform.parent.name == "Props") return sr.sortingLayerName;
        foreach (var r in root.GetComponentsInChildren<TilemapRenderer>(true))
            if (r.name.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0) return r.sortingLayerName;
        return "Default";
    }

    // Tiled 플래그 → 타일 변환 행렬. 대각 뒤집기(D)는 90도 회전과 좌우 뒤집기의 조합이다
    private static Matrix4x4 FlipMatrix(uint f)
    {
        bool h = (f & FlipH) != 0, v = (f & FlipV) != 0, d = (f & FlipD) != 0;
        Quaternion rot = Quaternion.identity;
        Vector3 scale = Vector3.one;
        if (d)
        {
            rot = Quaternion.Euler(0f, 0f, -90f);
            if (h && v) { scale.x = -1f; }
            else if (h) { rot = Quaternion.Euler(0f, 0f, -90f); }
            else if (v) { rot = Quaternion.Euler(0f, 0f, 90f); }
            else { rot = Quaternion.Euler(0f, 0f, 90f); scale.x = -1f; }
        }
        else
        {
            if (h) scale.x = -1f;
            if (v) scale.y = -1f;
        }
        return Matrix4x4.TRS(Vector3.zero, rot, scale);
    }

    // 폴더 안의 JSON 전부 가져오기 (파이썬이 뽑아 둔 것)
    public static string ImportFolder(string jsonFolder, string templatePrefabPath, string outFolder)
    {
        var sb = new System.Text.StringBuilder();
        foreach (string f in Directory.GetFiles(jsonFolder, "*.json"))
        {
            string name = Path.GetFileNameWithoutExtension(f);
            sb.AppendLine(Import(f, templatePrefabPath, outFolder.TrimEnd('/') + "/" + name + ".prefab"));
        }
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }
}
