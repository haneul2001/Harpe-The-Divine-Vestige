using UnityEngine;

// 이펙트 한 종류의 정의. 프레임 목록과 재생 방식만 담는 순수 데이터다.
//
// 애니메이터 컨트롤러를 쓰지 않는 이유:
//   100PixelVFX의 컨트롤러는 상태 20개가 파라미터 없이 나열돼 있어 이름으로 Play해야 하고,
//   전부 루프로 잡혀 있어 일회성 재생을 하려면 어차피 코드로 제어해야 한다.
//   프레임 배열을 직접 넘기면 속도·틴트·트리밍을 전부 여기서 조절할 수 있다.
[System.Serializable]
public class VfxClip
{
    [Tooltip("코드에서 이 이름으로 재생한다. 오타가 나면 조용히 아무것도 안 나오므로 짧고 명확하게")]
    public string id;

    [Tooltip("재생할 프레임. 시트에서 잘라 순서대로 넣는다")]
    public Sprite[] frames;

    [Tooltip("초당 프레임. 원본은 24fps. 올리면 짧고 날카로워진다")]
    [Min(1f)] public float fps = 24f;

    [Tooltip("켜면 계속 반복한다. 투사체처럼 날아가는 동안 유지돼야 하는 것에만 쓴다.\n" +
             "끄면 한 번 재생하고 스스로 사라진다")]
    public bool loop = false;

    [Tooltip("원본이 흰색이라 곱하기로 색이 자유롭게 들어간다. 사신 톤은 BDAADD 계열")]
    public Color tint = Color.white;

    [Tooltip("월드 크기 배율. 프레임은 64px/PPU32 라서 기본이 2x2 유닛이다")]
    public float scale = 1f;

    [Tooltip("정렬 레이어. 이펙트는 보통 소품(Prop)보다 위")]
    public string sortingLayer = "Skill";
    public int sortingOrder = 0;
}

// 이펙트 정의를 한곳에 모은 목록.
// Resources/VFX/VfxLibrary 에 두면 PixelVfx가 알아서 찾는다.
[CreateAssetMenu(fileName = "VfxLibrary", menuName = "Harpe/VFX/Library")]
public class VfxLibrary : ScriptableObject
{
    [SerializeField] private VfxClip[] clips = new VfxClip[0];

    public VfxClip Find(string id)
    {
        if (string.IsNullOrEmpty(id) || clips == null) return null;
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null && clips[i].id == id) return clips[i];
        return null;
    }

    public VfxClip[] All { get { return clips; } }
}
