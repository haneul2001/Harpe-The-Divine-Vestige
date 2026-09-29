using UnityEngine;

// 배경음악. 게임이 켜지는 순간(첫 씬이 뜨기 전) 한 번 틀고, 씬이 바뀌어도 끊지 않고 계속 반복한다 —
// 타이틀 → 스테이지로 넘어갈 때 음악이 처음부터 다시 시작되면 흐름이 끊긴다.
// 곡은 Resources/Audio/SfxLibrary 의 bgm 칸에 넣는다.
public static class Bgm
{
    private static AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var lib = Resources.Load<SfxLibrary>("Audio/SfxLibrary");
        if (lib == null || lib.bgm == null) return;

        var go = new GameObject("[Bgm]");
        go.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(go);

        source = go.AddComponent<AudioSource>();
        source.clip = lib.bgm;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = lib.bgmVolume;
        source.ignoreListenerPause = true;   // 일시정지 화면에서도 음악은 흐른다
        source.Play();
    }

    // 설정 화면 등에서 음량을 바꿀 때
    public static void SetVolume(float v)
    {
        if (source != null) source.volume = Mathf.Clamp01(v);
    }
}
