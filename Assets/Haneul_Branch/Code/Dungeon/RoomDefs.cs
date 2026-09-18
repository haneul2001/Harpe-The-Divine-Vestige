using UnityEngine;

// 방 종류. 생성기가 이 값을 보고 어떤 프리팹을 쓸지 고른다.
public enum RoomType
{
    Start,      // 시작 방 (적 없음)
    Normal,     // 일반 전투 방
    Treasure,   // 보상 방
    Shop,       // 상점
    Boss        // 보스 방
}

// 문 방향. 값 순서(위/오른/아래/왼)를 바꾸면 Opposite() 계산이 깨지니 주의.
public enum Dir
{
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3
}

public static class DirUtil
{
    public static readonly Dir[] All = { Dir.Up, Dir.Right, Dir.Down, Dir.Left };

    // 그리드 좌표 오프셋. y+ 가 위쪽.
    public static Vector2Int Offset(this Dir dir)
    {
        switch (dir)
        {
            case Dir.Up:    return new Vector2Int(0, 1);
            case Dir.Right: return new Vector2Int(1, 0);
            case Dir.Down:  return new Vector2Int(0, -1);
            default:        return new Vector2Int(-1, 0);
        }
    }

    public static Dir Opposite(this Dir dir)
    {
        return (Dir)(((int)dir + 2) % 4);
    }

    public static bool IsVertical(this Dir dir)
    {
        return dir == Dir.Up || dir == Dir.Down;
    }
}
