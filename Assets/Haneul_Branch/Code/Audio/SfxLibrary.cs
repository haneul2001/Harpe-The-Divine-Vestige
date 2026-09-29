using UnityEngine;

// 효과음 한 종류의 정의. 코드는 id로만 부른다 — 소리를 바꿀 때 코드를 안 건드려도 된다.
[System.Serializable]
public class SfxClip
{
    [Tooltip("코드에서 이 이름으로 재생한다")]
    public string id;

    [Tooltip("여러 개면 매번 하나를 골라 튼다 (같은 소리가 기계적으로 반복되지 않게)")]
    public AudioClip[] clips;

    [Range(0f, 1f)] public float volume = 1f;

    [Tooltip("재생할 때마다 음높이를 이만큼 흔든다. 연타할 때 덜 거슬린다")]
    [Range(0f, 0.3f)] public float pitchJitter = 0.05f;

    [Tooltip("이 간격(초)보다 촘촘하게 다시 부르면 무시한다 — 한 번 휘두름에 여러 마리가 맞아도 소리가 뭉개지지 않게")]
    [Min(0f)] public float minInterval = 0.04f;
}

// 효과음 정의 목록. Resources/Audio/SfxLibrary 에 두면 Sfx가 알아서 찾는다.
[CreateAssetMenu(fileName = "SfxLibrary", menuName = "Harpe/Audio/Sfx Library")]
public class SfxLibrary : ScriptableObject
{
    [SerializeField] private SfxClip[] clips = new SfxClip[0];

    [Header("배경음악 — 타이틀부터 스테이지까지 끊기지 않고 반복")]
    public AudioClip bgm;
    [Range(0f, 1f)] public float bgmVolume = 0.5f;

    public SfxClip[] All { get { return clips; } }

    public SfxClip Find(string id)
    {
        if (string.IsNullOrEmpty(id) || clips == null) return null;
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null && clips[i].id == id) return clips[i];
        return null;
    }
}
