using UnityEngine;
using UnityEngine.SceneManagement;

// 한 판(run)의 경계를 알려 주는 곳.
//
// 층 이동은 같은 씬에서 방만 갈아엎으므로 씬이 다시 로드되는 순간이 곧 "새 판"이다
// (죽고 다시 시작 / 타이틀에서 시작). 판을 넘겨 살아남으면 안 되는 것들은
// 여기서 한 번에 지운다 — 각자 알아서 지우게 하면 반드시 하나를 빠뜨린다.
public static class RunSession
{
    // 새 판이 시작될 때마다 발생. 판 단위 자원을 들고 있는 쪽이 구독한다.
    public static event System.Action NewRun;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        Begin();
    }

    public static void Begin()
    {
        // 골드는 판마다 처음부터 — 다음 판에 들고 가면 상점이 의미를 잃는다
        PlayerStatus status = Object.FindObjectOfType<PlayerStatus>();
        if (status != null) status.ResetGold();

        // 소울 누적(특성 선택 진행도)도 판 단위다
        if (TraitLevelUp.Instance != null) TraitLevelUp.Instance.ResetProgress();

        if (NewRun != null) NewRun();
    }
}
