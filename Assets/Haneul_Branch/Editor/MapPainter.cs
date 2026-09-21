using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// 방 프리팹에 타일을 깔아 주는 도구.
//
// [Harpe > 맵 타일 브라우저]에서 지정한 팔레트(테마별 역할표)만 보고 그린다 —
// 어떤 타일이 어떤 그림인지는 모른다.
//
//  · 바닥  : 방 전체를 채운다 (같은 역할에 타일이 여럿이면 칸마다 무작위)
//  · 울타리: 바깥 한 줄에 두른다. 네 모서리는 모서리 타일, 변은 변 타일.
//            문이 뚫려 있던 칸은 그대로 비워 둔다 (지금 벽 타일맵의 빈칸을 그대로 따른다)
//  · 오브젝트: 안쪽에 흩뿌린다 (지정된 타일이 있을 때만)
//
// 벽 타일맵에는 콜라이더가 붙어 있어서, 울타리를 여기에 그리면 그대로 막힌다.
public static class MapPainter
{
    public class Options
    {
        public bool paintFloor = true;
        public bool paintFence = true;
        public bool paintObjects = true;

        [Tooltip("오브젝트를 안쪽 칸 중 몇 %에 놓을지")]
        public float objectDensity = 0.04f;

        [Tooltip("문 앞 이 칸수 안에는 오브젝트를 놓지 않는다")]
        public int doorClearance = 2;

        public int seed = 0;
    }

    // 방 하나 그리기. roomRoot는 프리팹을 연 루트(또는 씬 인스턴스)
    public static string Paint(GameObject roomRoot, MapTilePalette palette, MapTilePalette.Theme theme, Options opt)
    {
        if (roomRoot == null || palette == null || theme == null) return "대상이 없다";

        Tilemap ground = FindMap(roomRoot, "Ground");
        Tilemap wall = FindMap(roomRoot, "Wall");
        Tilemap objects = FindMap(roomRoot, "Object");
        if (ground == null || wall == null) return "Ground/Wall 타일맵을 못 찾았다";

        var rng = new System.Random(opt.seed != 0 ? opt.seed : roomRoot.name.GetHashCode());

        // 방 범위는 바닥 타일맵 기준 (벽까지 포함한 바깥 한 줄이 테두리)
        BoundsInt area = ground.cellBounds;
        int painted = 0, fence = 0, props = 0;

        // 문 자리: 지금 벽 타일맵에서 비어 있는 테두리 칸
        var openings = new HashSet<Vector3Int>();
        foreach (Vector3Int p in Ring(area))
            if (wall.GetTile(p) == null) openings.Add(p);

        // 바닥 후보: 팔레트에 지정된 것. 지정이 없으면 테마 폴더의 랜덤 타일(이름에 rnd)을 자동으로 찾아 쓴다
        List<TileBase> floors = palette.Of(theme, MapTileRole.Floor);
        bool autoFloor = false;
        if (opt.paintFloor && floors.Count == 0)
        {
            floors = FindRandomTiles(theme.sourceFolder);
            autoFloor = floors.Count > 0;
        }

        if (opt.paintFloor && floors.Count > 0)
        {
            foreach (Vector3Int p in area.allPositionsWithin)
            {
                // 칸마다 무작위 — 후보가 여럿이면 섞여 깔린다
                TileBase t = floors[rng.Next(floors.Count)];
                if (t == null) continue;
                ground.SetTile(p, t);
                painted++;
            }
        }

        if (opt.paintFence && palette.HasFullFence(theme))
        {
            foreach (Vector3Int p in Ring(area))
            {
                if (openings.Contains(p)) { wall.SetTile(p, null); continue; }

                MapTileRole role = FenceRoleAt(p, area);
                TileBase t = palette.Pick(theme, role, rng);
                if (t == null) continue;
                wall.SetTile(p, t);
                fence++;
            }
        }

        if (opt.paintObjects && objects != null && palette.CountOf(theme, MapTileRole.Object) > 0)
        {
            objects.ClearAllTiles();

            // 문 앞은 비워 둔다 — 소품이 입구를 막으면 방을 못 지나간다
            var blocked = new HashSet<Vector3Int>();
            foreach (Vector3Int door in openings)
                for (int dx = -opt.doorClearance; dx <= opt.doorClearance; dx++)
                    for (int dy = -opt.doorClearance; dy <= opt.doorClearance; dy++)
                        blocked.Add(new Vector3Int(door.x + dx, door.y + dy, 0));

            foreach (Vector3Int p in Inner(area))
            {
                if (blocked.Contains(p)) continue;
                if (rng.NextDouble() > opt.objectDensity) continue;

                TileBase t = palette.Pick(theme, MapTileRole.Object, rng);
                if (t == null) continue;
                objects.SetTile(p, t);
                props++;
            }
        }

        ground.RefreshAllTiles();
        wall.RefreshAllTiles();
        if (objects != null) objects.RefreshAllTiles();

        return roomRoot.name + " — 바닥 " + painted + (autoFloor ? "(자동 랜덤타일 " + floors.Count + "종)" : "(지정 " + floors.Count + "종)")
             + ", 울타리 " + fence + " (문 " + openings.Count + "칸 비움), 오브젝트 " + props;
    }

    // 테마 폴더에서 랜덤 타일을 찾는다. RafaelMatos 팩은 무작위로 그림이 바뀌는 타일 이름에 rnd가 붙어 있다.
    // 바닥을 손으로 지정하지 않은 테마에서도 단조롭지 않게 깔리도록 하는 안전장치다.
    private static List<TileBase> FindRandomTiles(string themeFolder)
    {
        var result = new List<TileBase>();
        if (string.IsNullOrEmpty(themeFolder)) return result;

        string root = "Assets/ThirdParty/RafaelMatos/" + themeFolder;
        if (!AssetDatabase.IsValidFolder(root)) return result;

        foreach (string guid in AssetDatabase.FindAssets("t:TileBase", new[] { root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
            if (!name.Contains("rnd") && !name.Contains("random")) continue;

            // 구멍·가시·용암처럼 밟으면 안 되는 것은 바닥으로 쓰지 않는다
            if (name.Contains("hole") || name.Contains("spike") || name.Contains("pit") || name.Contains("lava")) continue;

            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (tile != null) result.Add(tile);
        }
        return result;
    }

    // 테두리 한 줄
    private static IEnumerable<Vector3Int> Ring(BoundsInt area)
    {
        for (int x = area.xMin; x < area.xMax; x++)
        {
            yield return new Vector3Int(x, area.yMax - 1, 0);
            yield return new Vector3Int(x, area.yMin, 0);
        }
        for (int y = area.yMin + 1; y < area.yMax - 1; y++)
        {
            yield return new Vector3Int(area.xMin, y, 0);
            yield return new Vector3Int(area.xMax - 1, y, 0);
        }
    }

    // 테두리를 뺀 안쪽
    private static IEnumerable<Vector3Int> Inner(BoundsInt area)
    {
        for (int x = area.xMin + 1; x < area.xMax - 1; x++)
            for (int y = area.yMin + 1; y < area.yMax - 1; y++)
                yield return new Vector3Int(x, y, 0);
    }

    private static MapTileRole FenceRoleAt(Vector3Int p, BoundsInt area)
    {
        bool left = p.x == area.xMin, right = p.x == area.xMax - 1;
        bool top = p.y == area.yMax - 1, bottom = p.y == area.yMin;

        if (top && left) return MapTileRole.FenceCornerTopLeft;
        if (top && right) return MapTileRole.FenceCornerTopRight;
        if (bottom && left) return MapTileRole.FenceCornerBottomLeft;
        if (bottom && right) return MapTileRole.FenceCornerBottomRight;
        if (top) return MapTileRole.FenceTop;
        if (bottom) return MapTileRole.FenceBottom;
        if (left) return MapTileRole.FenceLeft;
        return MapTileRole.FenceRight;
    }

    private static Tilemap FindMap(GameObject root, string keyword)
    {
        foreach (var m in root.GetComponentsInChildren<Tilemap>(true))
            if (m.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0) return m;
        return null;
    }

    // ─── 프리팹 파일에 바로 그리기 ───

    public static string PaintPrefab(string prefabPath, string themeName, Options opt)
    {
        var palette = AssetDatabase.LoadAssetAtPath<MapTilePalette>("Assets/Haneul_Branch/Resources/Map/MapTilePalette.asset");
        if (palette == null) return "팔레트가 없다";

        var theme = palette.Find(themeName);
        if (theme == null) return "'" + themeName + "' 테마가 없다";

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        string report;
        try
        {
            report = Paint(root, palette, theme, opt ?? new Options());
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return report;
    }
}
