using System.Collections.Generic;
using UnityEngine.SceneManagement;

// 씬에 미리 놓아 두지 않고 코드가 만들어 내는 상시 오브젝트(체력바 · 판 기록 등)의 등록소.
//
// [RuntimeInitializeOnLoadMethod]는 실행 시작에 딱 한 번만 돈다.
// 그래서 사망 후 재시작처럼 씬을 다시 불러오면 그 오브젝트들은 사라진 채 돌아오지 않는다.
// 여기에 등록해 두면 씬이 열릴 때마다 다시 챙긴다.
//
// 각 클래스가 "자기를 어떻게 만드는지"는 계속 자기가 들고 있다 —
// 여기는 언제 부를지만 안다.
public static class RuntimeSingletons
{
    private static readonly List<System.Action> spawners = new List<System.Action>();
    private static bool hooked;

    // 지금 한 번 만들고, 앞으로 씬이 열릴 때마다 다시 만든다.
    // 만드는 쪽은 "이미 있으면 아무것도 안 한다"를 스스로 보장해야 한다.
    public static void EnsureEachScene(System.Action spawn)
    {
        if (spawn == null) return;

        if (!spawners.Contains(spawn))
        {
            spawners.Add(spawn);

            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }

        spawn();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        for (int i = 0; i < spawners.Count; i++)
            if (spawners[i] != null) spawners[i]();
    }
}
