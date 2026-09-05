using System.Collections.Generic;
using UnityEngine;

// 층 하나를 통째로 담는 데이터.
//
// 원래 던전 생성기의 인스펙터에 직접 물려 있던 설정을 에셋으로 옮긴 것이다.
// 스테이지를 추가할 때 코드도 씬도 건드리지 않고 에셋만 하나 더 만들면 되게 하는 것이 목적 —
// 생성 알고리즘은 층마다 다르지 않고, 다른 건 "무엇으로 짓는가"뿐이기 때문이다.
[CreateAssetMenu(fileName = "Floor_", menuName = "Harpe/Dungeon/Floor Data")]
public class FloorData : ScriptableObject
{
    [Header("표시")]
    [Tooltip("입장 연출에 뜨는 층 이름")]
    public string displayName = "지하 묘지";
    [Tooltip("이름 아래 작게 붙는 줄. 비우면 층 번호가 자동으로 들어간다")]
    public string subtitle = "";

    [Header("생성 규모")]
    [Tooltip("만들 방 개수 (시작 방 포함)")]
    [Min(1)] public int roomCount = 10;
    [Tooltip("방이 퍼질 수 있는 최대 그리드 범위")]
    public Vector2Int gridSize = new Vector2Int(9, 8);
    [Tooltip("세로 방향 문을 쓸지. 끄면 좌우로만 이어지는 구조가 된다")]
    public bool allowVerticalDoors = true;

    [Header("특수 방")]
    public Room startRoomPrefab;
    public Room bossRoomPrefab;
    public Room treasureRoomPrefab;
    public Room shopRoomPrefab;

    [Header("일반 방")]
    [Tooltip("Resources 아래 폴더에서 일반 방을 전부 불러온다. "
           + "층마다 타일셋이 다르므로 층마다 폴더를 나눠 쓰는 것이 기본이다.")]
    public bool loadRoomsFromResources = true;

    [Tooltip("Resources 기준 경로. 예: \"Rooms\" → Assets/*/Resources/Rooms")]
    public string roomsResourcePath = "Rooms";

    [Tooltip("위 자동 수집을 끈 경우에만 쓰는 직접 지정 목록")]
    public List<Room> normalRoomPrefabs = new List<Room>();

    // 부제가 비어 있으면 층 번호로 채운다. 층마다 "2층"을 손으로 적게 하면 반드시 어긋난다.
    public string ResolveSubtitle(int floorNumber)
    {
        return string.IsNullOrEmpty(subtitle) ? floorNumber + "층" : subtitle;
    }
}
