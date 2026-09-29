using UnityEngine;
using UnityEngine.Rendering;

// 캐릭터(플레이어·적·보스)의 앞뒤를 발 위치로 정한다.
//
// 몸을 이루는 그림(몸통·그림자·링 등)을 SortingGroup 하나로 묶어 모두 같은 그리기 층에 둔다.
// 렌더러 설정(Renderer2D)의 정렬 축이 (0,1,0)이라, 같은 층·같은 순서끼리는 y가 낮은(= 화면 아래,
// 앞쪽) 것이 나중에 그려져 위에 온다. 묶음의 기준점은 캐릭터 루트 위치 = 발밑이다.
// 예전엔 플레이어는 Player 층, 적은 Enemy 층이라 적이 위치와 상관없이 늘 플레이어 위에 그려졌다.
public static class CharacterSorting
{
    public const string Layer = "Enemy";
    public const int Order = 0;

    public static SortingGroup Ensure(GameObject root)
    {
        if (root == null) return null;
        var sg = root.GetComponent<SortingGroup>();
        if (sg == null) sg = root.AddComponent<SortingGroup>();
        sg.sortingLayerName = Layer;
        sg.sortingOrder = Order;
        return sg;
    }
}
