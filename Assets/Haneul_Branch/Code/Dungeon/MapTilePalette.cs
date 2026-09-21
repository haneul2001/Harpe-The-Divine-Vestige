using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 타일이 맵에서 어떤 역할인지.
// 순서를 바꾸면 이미 지정해 둔 팔레트 값이 어긋나므로 뒤에만 추가할 것.
public enum MapTileRole
{
    None = 0,

    Floor = 1,          // 바닥
    Wall = 2,           // 벽 (막힘)
    Object = 3,         // 소품 — 지나갈 수 있는 장식

    // 울타리 — 네 변과 네 모서리. 사각형으로 두를 때 자동으로 골라 쓴다
    FenceLeft = 10,
    FenceRight = 11,
    FenceTop = 12,
    FenceBottom = 13,
    FenceCornerTopLeft = 14,
    FenceCornerTopRight = 15,
    FenceCornerBottomLeft = 16,
    FenceCornerBottomRight = 17,
}

public static class MapTileRoleUtil
{
    public static string Label(this MapTileRole r)
    {
        switch (r)
        {
            case MapTileRole.Floor:  return "바닥";
            case MapTileRole.Wall:   return "벽";
            case MapTileRole.Object: return "오브젝트";
            case MapTileRole.FenceLeft:              return "울타리 ←";
            case MapTileRole.FenceRight:             return "울타리 →";
            case MapTileRole.FenceTop:               return "울타리 ↑";
            case MapTileRole.FenceBottom:            return "울타리 ↓";
            case MapTileRole.FenceCornerTopLeft:     return "모서리 ↖";
            case MapTileRole.FenceCornerTopRight:    return "모서리 ↗";
            case MapTileRole.FenceCornerBottomLeft:  return "모서리 ↙";
            case MapTileRole.FenceCornerBottomRight: return "모서리 ↘";
            default: return "없음";
        }
    }

    // 칸에 여러 역할을 겹쳐 표시할 때 쓰는 한두 글자
    public static string Short(this MapTileRole r)
    {
        switch (r)
        {
            case MapTileRole.Floor:  return "바";
            case MapTileRole.Wall:   return "벽";
            case MapTileRole.Object: return "오";
            case MapTileRole.FenceLeft:              return "←";
            case MapTileRole.FenceRight:             return "→";
            case MapTileRole.FenceTop:               return "↑";
            case MapTileRole.FenceBottom:            return "↓";
            case MapTileRole.FenceCornerTopLeft:     return "↖";
            case MapTileRole.FenceCornerTopRight:    return "↗";
            case MapTileRole.FenceCornerBottomLeft:  return "↙";
            case MapTileRole.FenceCornerBottomRight: return "↘";
            default: return "";
        }
    }

    public static bool IsFence(this MapTileRole r)
    {
        return (int)r >= (int)MapTileRole.FenceLeft;
    }

    // 브라우저와 맵 생성이 같은 순서를 쓰도록 한곳에 모아 둔다 (단축키 1~9,0,- 순서)
    public static readonly MapTileRole[] All =
    {
        MapTileRole.Floor, MapTileRole.Wall, MapTileRole.Object,
        MapTileRole.FenceCornerTopLeft, MapTileRole.FenceTop, MapTileRole.FenceCornerTopRight,
        MapTileRole.FenceLeft, MapTileRole.FenceRight,
        MapTileRole.FenceCornerBottomLeft, MapTileRole.FenceBottom, MapTileRole.FenceCornerBottomRight,
    };
}

// "이 타일은 울타리 왼쪽", "이 타일은 오브젝트" 같은 지정을 테마별로 모아 둔 표.
//
// 테마는 타일셋 팩 하나(낡은 감옥, 지하 묘실, 하수도 …)에 대응한다. 층마다 쓰는 타일이 다르므로
// 같은 역할이라도 테마별로 다른 타일이 지정된다.
//
// 맵을 그리는 쪽은 타일 이름이나 그림을 몰라도 된다 — "이 테마의 울타리 왼쪽"만 물어보면 된다.
// 지정은 [Harpe > 맵 타일 브라우저] 창에서 한다.
[CreateAssetMenu(fileName = "MapTilePalette", menuName = "Harpe/Map/타일 팔레트")]
public class MapTilePalette : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public TileBase tile;
        public MapTileRole role = MapTileRole.None;

        [Tooltip("같은 역할에 여러 타일이 있을 때 뽑힐 가중치")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("메모 (선택)")]
        public string note = "";
    }

    [System.Serializable]
    public class Theme
    {
        [Tooltip("테마 이름 (예: 낡은 감옥, 지하 묘실). 맵을 그릴 때 이 이름으로 고른다")]
        public string name = "새 테마";

        [Tooltip("어느 타일셋 폴더에서 온 테마인지 (선택). 브라우저가 폴더를 기억하는 용도")]
        public string sourceFolder = "";

        public List<Entry> entries = new List<Entry>();
    }

    [SerializeField] private List<Theme> themes = new List<Theme>();

    public List<Theme> Themes => themes;

    private static MapTilePalette cached;

    // Resources/Map/MapTilePalette 하나를 프로젝트 공용으로 쓴다
    public static MapTilePalette Load()
    {
        if (cached == null) cached = Resources.Load<MapTilePalette>("Map/MapTilePalette");
        return cached;
    }

    public Theme Find(string themeName)
    {
        for (int i = 0; i < themes.Count; i++)
            if (themes[i] != null && themes[i].name == themeName) return themes[i];
        return null;
    }

    public Theme GetOrCreate(string themeName, string sourceFolder = "")
    {
        Theme t = Find(themeName);
        if (t != null) return t;

        t = new Theme { name = themeName, sourceFolder = sourceFolder };
        themes.Add(t);
        return t;
    }

    public string[] ThemeNames()
    {
        var names = new string[themes.Count];
        for (int i = 0; i < themes.Count; i++) names[i] = themes[i] != null ? themes[i].name : "?";
        return names;
    }

    // ─── 한 테마 안에서의 지정 ───

    // 한 타일이 여러 역할을 맡을 수 있다 — 같은 울타리 조각이 ←와 → 둘 다인 경우가 흔하다.
    // 그래서 "타일 하나 = 역할 하나"가 아니라 (타일, 역할) 쌍을 여러 개 담는다.
    public List<MapTileRole> RolesOf(Theme theme, TileBase tile)
    {
        var roles = new List<MapTileRole>();
        if (theme == null || tile == null) return roles;

        for (int i = 0; i < theme.entries.Count; i++)
            if (theme.entries[i] != null && theme.entries[i].tile == tile && !roles.Contains(theme.entries[i].role))
                roles.Add(theme.entries[i].role);
        return roles;
    }

    public bool HasRole(Theme theme, TileBase tile, MapTileRole role)
    {
        if (theme == null || tile == null) return false;

        for (int i = 0; i < theme.entries.Count; i++)
            if (theme.entries[i] != null && theme.entries[i].tile == tile && theme.entries[i].role == role) return true;
        return false;
    }

    public bool HasAnyRole(Theme theme, TileBase tile)
    {
        return RolesOf(theme, tile).Count > 0;
    }

    // 이미 있으면 빼고, 없으면 넣는다. 돌려주는 값은 "지정된 상태인가"
    public bool ToggleRole(Theme theme, TileBase tile, MapTileRole role)
    {
        if (theme == null || tile == null || role == MapTileRole.None) return false;

        for (int i = theme.entries.Count - 1; i >= 0; i--)
        {
            if (theme.entries[i] == null || theme.entries[i].tile != tile || theme.entries[i].role != role) continue;
            theme.entries.RemoveAt(i);
            return false;
        }

        theme.entries.Add(new Entry { tile = tile, role = role });
        return true;
    }

    // 이 타일의 지정을 전부 지운다
    public void ClearRoles(Theme theme, TileBase tile)
    {
        if (theme == null || tile == null) return;

        for (int i = theme.entries.Count - 1; i >= 0; i--)
            if (theme.entries[i] != null && theme.entries[i].tile == tile) theme.entries.RemoveAt(i);
    }

    public List<TileBase> Of(Theme theme, MapTileRole role)
    {
        var list = new List<TileBase>();
        if (theme == null) return list;

        for (int i = 0; i < theme.entries.Count; i++)
            if (theme.entries[i] != null && theme.entries[i].role == role && theme.entries[i].tile != null)
                list.Add(theme.entries[i].tile);
        return list;
    }

    public int CountOf(Theme theme, MapTileRole role)
    {
        return Of(theme, role).Count;
    }

    // 가중치를 반영해 하나 고른다. 지정된 타일이 없으면 null
    public TileBase Pick(Theme theme, MapTileRole role, System.Random rng = null)
    {
        if (theme == null) return null;

        float total = 0f;
        for (int i = 0; i < theme.entries.Count; i++)
        {
            var e = theme.entries[i];
            if (e != null && e.role == role && e.tile != null) total += Mathf.Max(0.0001f, e.weight);
        }
        if (total <= 0f) return null;

        double roll = (rng != null ? rng.NextDouble() : Random.value) * total;
        for (int i = 0; i < theme.entries.Count; i++)
        {
            var e = theme.entries[i];
            if (e == null || e.role != role || e.tile == null) continue;
            roll -= Mathf.Max(0.0001f, e.weight);
            if (roll <= 0d) return e.tile;
        }
        return null;
    }

    public TileBase Pick(string themeName, MapTileRole role, System.Random rng = null)
    {
        return Pick(Find(themeName), role, rng);
    }

    // 울타리를 한 벌 다 지정했는지 (사각형을 두르려면 8종이 모두 필요하다)
    public bool HasFullFence(Theme theme)
    {
        for (int i = (int)MapTileRole.FenceLeft; i <= (int)MapTileRole.FenceCornerBottomRight; i++)
            if (CountOf(theme, (MapTileRole)i) == 0) return false;
        return true;
    }
}
