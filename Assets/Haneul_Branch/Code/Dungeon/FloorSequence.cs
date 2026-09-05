using System.Collections.Generic;
using UnityEngine;

// 층을 지나가는 순서. 스테이지를 추가하는 작업이 "여기 한 줄 끼우기"로 끝나게 하는 것이 전부다.
//
// 순서를 FloorData 안에 "다음 층" 참조로 넣지 않은 이유:
// 그렇게 하면 층을 중간에 끼울 때 앞뒤 두 에셋을 같이 고쳐야 하고,
// 전체가 몇 층인지(마지막 층인지) 알아내려면 사슬을 끝까지 따라가야 한다.
[CreateAssetMenu(fileName = "FloorSequence", menuName = "Harpe/Dungeon/Floor Sequence")]
public class FloorSequence : ScriptableObject
{
    [Tooltip("위에서부터 1층. 비어 있는 칸은 무시된다")]
    public List<FloorData> floors = new List<FloorData>();

    public int Count
    {
        get { return floors != null ? floors.Count : 0; }
    }

    public FloorData Get(int index)
    {
        if (floors == null || index < 0 || index >= floors.Count) return null;
        return floors[index];
    }

    public bool IsLast(int index)
    {
        return index >= Count - 1;
    }
}
