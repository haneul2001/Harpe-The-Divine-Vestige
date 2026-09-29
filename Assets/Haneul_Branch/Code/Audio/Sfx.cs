using System.Collections.Generic;
using UnityEngine;

// 효과음 재생 입구. Sfx.Play("PlayerHurt")처럼 id로 부른다.
//
// 씬마다 오디오 오브젝트를 깔 필요가 없게, 처음 부를 때 숨은 재생기(AudioSource 묶음)를 하나 만들어
// 씬이 바뀌어도 남겨 둔다. 소리는 전부 2D — 탑다운 화면이 좁아서 위치에 따른 좌우 차이가 오히려 어색하다.
public static class Sfx
{
    // 전체 효과음 크기. 나중에 설정 화면이 붙으면 여기를 바꾼다
    public static float Volume = 1f;

    private const string LibraryPath = "Audio/SfxLibrary";
    private const int Voices = 12;

    private static SfxLibrary library;
    private static bool libraryMissingLogged;
    private static AudioSource[] voices;
    private static int nextVoice;
    private static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

    // 게임이 켜질 때 라이브러리를 읽고 재생기를 만들고 소리를 전부 메모리에 올려 둔다.
    // 처음 부를 때 이걸 하면 첫 타격음이 한 박자 늦게 난다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Warm()
    {
        lastPlayed.Clear();
        voices = null;
        library = Resources.Load<SfxLibrary>(LibraryPath);
        if (library == null || library.All == null) return;
        NextVoice();
        foreach (var c in library.All)
            if (c != null && c.clips != null)
                foreach (var clip in c.clips)
                    if (clip != null && clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
    }

    public static void Play(string id)
    {
        SfxClip c = Lookup(id);
        if (c == null || c.clips == null || c.clips.Length == 0) return;

        // 멈춘 시간(페이즈 연출·일시정지)에도 소리는 나야 하므로 실제 시간으로 잰다
        float now = Time.unscaledTime;
        float last;
        if (lastPlayed.TryGetValue(id, out last) && now - last < c.minInterval) return;
        lastPlayed[id] = now;

        AudioClip clip = c.clips[Random.Range(0, c.clips.Length)];
        if (clip == null) return;

        AudioSource src = NextVoice();
        if (src == null) return;
        src.pitch = 1f + Random.Range(-c.pitchJitter, c.pitchJitter);
        src.PlayOneShot(clip, c.volume * Volume);
    }

    private static SfxClip Lookup(string id)
    {
        if (library == null) library = Resources.Load<SfxLibrary>(LibraryPath);
        if (library == null)
        {
            if (!libraryMissingLogged)
            {
                libraryMissingLogged = true;
                Debug.LogWarning("[Sfx] Resources/" + LibraryPath + " 를 찾지 못했다. 효과음이 나오지 않는다");
            }
            return null;
        }
        return library.Find(id);
    }

    // 목소리를 돌아가며 쓴다. PlayOneShot은 한 소스에서도 겹쳐 나지만, 음높이를 소리마다 달리 주려면 소스를 나눠야 한다
    private static AudioSource NextVoice()
    {
        if (voices == null || voices[0] == null)
        {
            var go = new GameObject("[Sfx]");
            go.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(go);
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                voices[i] = s;
            }
        }
        AudioSource v = voices[nextVoice];
        nextVoice = (nextVoice + 1) % Voices;
        return v;
    }
}
