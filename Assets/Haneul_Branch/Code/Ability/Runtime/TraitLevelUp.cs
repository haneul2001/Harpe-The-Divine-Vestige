using System.Collections;
using UnityEngine;

// 소울을 경험치로 본다. 일정량을 모을 때마다 특성 카드를 한 장 고르게 한다.
//
// 세는 값은 "지금 들고 있는 소울"이 아니라 "지금까지 번 소울 총합"이다 —
// 스킬로 소울을 쓴다고 해서 모아 둔 성장이 날아가면 안 된다.
//
// 씬마다 플레이어를 놓는 방식이 달라서(프리팹 인스턴스가 아닌 씬 오브젝트도 있다)
// 어디에도 안 붙어 있으면 게임 시작 때 스스로 하나 만든다. 화면은 TraitPickPanel이 알아서 만든다.
public class TraitLevelUp : MonoBehaviour
{
    [Tooltip("특성 한 번을 고르는 데 필요한 누적 소울")]
    [Min(1)] [SerializeField] private int soulPerPick = 30;

    [Tooltip("조건을 채우고 화면이 뜨기까지의 뜸 (초). 처형이 끝나자마자 화면이 덮이면 뚝 끊기는 느낌이 든다")]
    [Min(0f)] [SerializeField] private float showDelay = 1f;

    [Tooltip("한 번에 보여 줄 카드 수")]
    [Min(2)] [SerializeField] private int choices = 3;

    [Header("등급 확률 (일반 · 희귀 · 에픽 · 전설)")]
    [SerializeField] private float[] rarityWeights = { 60f, 27f, 10f, 3f };

    [SerializeField] private AbilityUISkin skin;
    [SerializeField] private Font font;

    public static TraitLevelUp Instance { get; private set; }

    // 누적 소울과 다음 선택까지 남은 양 — 상태바 같은 곳에서 진행도를 그리고 싶을 때 쓴다
    public int TotalSoul { get; private set; }
    public int SoulPerPick => soulPerPick;
    public int SoulIntoLevel => TotalSoul % soulPerPick;
    public int PendingPicks { get; private set; }

    private TraitPickPanel panel;
    private bool waiting;

    // 씬에 없으면 스스로 만든다 — 한 판 내내 살아 있어야 층을 넘어가도 누적이 유지된다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        if (FindObjectOfType<TraitLevelUp>() != null) return;

        var go = new GameObject("TraitLevelUp");
        go.AddComponent<TraitLevelUp>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        PlayerStatus.SoulGained += OnSoulGained;
    }

    private void OnDisable()
    {
        PlayerStatus.SoulGained -= OnSoulGained;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnSoulGained(int amount)
    {
        if (amount <= 0) return;

        int before = TotalSoul / soulPerPick;
        TotalSoul += amount;
        int after = TotalSoul / soulPerPick;

        // 한 번에 여러 단계를 넘길 수도 있다 (보스 처형 등) — 넘긴 만큼 차례로 고르게 한다
        PendingPicks += Mathf.Max(0, after - before);
        TryShowNext();
    }

    // 새 판을 시작할 때 — 누적은 판마다 처음부터다
    public void ResetProgress()
    {
        TotalSoul = 0;
        PendingPicks = 0;
    }

    // 치트/테스트용 — 바로 한 장 고르게 한다
    [ContextMenu("특성 선택 띄우기")]
    public void GrantPick()
    {
        PendingPicks++;
        TryShowNext();
    }

    private void TryShowNext()
    {
        if (PendingPicks <= 0) return;
        if (waiting) return;
        if (panel != null && panel.IsOpen) return;   // 고르는 중이면 끝나고 이어서

        StartCoroutine(ShowAfterDelay());
    }

    // 뜸을 들이고 나서 연다. 처형 연출이 끝나는 걸 보고 화면이 덮여야 자연스럽다
    private IEnumerator ShowAfterDelay()
    {
        waiting = true;
        if (showDelay > 0f)
        {
            float end = Time.unscaledTime + showDelay;
            while (Time.unscaledTime < end) yield return null;
        }

        // 보스 피니시 슬로모션이 도는 동안은 열지 않는다 — 그쪽도 timeScale을 주무르고 있어서
        // 겹치면 연출이 끝난 뒤에도 시간이 느린 채로 남는다. 다른 창(일시정지·상점)도 마찬가지.
        while (Room.FinishRunning || Time.timeScale <= 0f) yield return null;

        waiting = false;

        Show();
    }

    private void Show()
    {
        var library = AbilityCardLibrary.Load();
        if (library == null)
        {
            Debug.LogWarning("[TraitLevelUp] Resources/" + AbilityCardLibrary.ResourcePath + " 가 없다 — [Harpe > 능력 > 카드 목록 갱신]", this);
            PendingPicks = 0;
            return;
        }

        var cards = library.RollDistinct(choices, rarityWeights);
        if (cards.Count == 0)
        {
            Debug.LogWarning("[TraitLevelUp] 뽑을 카드가 없다 — 카드 목록이 비어 있는지 확인할 것", this);
            PendingPicks = 0;
            return;
        }

        if (panel == null) panel = TraitPickPanel.Create(skin != null ? skin : library.skin, font, transform);
        panel.Show(cards, OnPicked);
    }

    private void OnPicked(AbilityCard card)
    {
        PendingPicks = Mathf.Max(0, PendingPicks - 1);

        if (card != null && AbilityInventory.Instance != null)
            AbilityInventory.Instance.Add(card);

        // 남은 선택이 있으면 이어서 (보스처럼 한 번에 여러 단계를 넘겼을 때)
        TryShowNext();
    }
}
