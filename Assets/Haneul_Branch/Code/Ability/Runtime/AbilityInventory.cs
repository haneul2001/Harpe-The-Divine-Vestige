using System.Collections.Generic;
using UnityEngine;

// 플레이어가 보유한 능력 카드 목록.
//
// 이 클래스는 "무엇을 갖고 있는가"만 안다. 화면에 어떻게 그릴지는 UI가,
// 세트가 몇 단계 열렸는지는 AbilitySetCalculator가 따로 계산한다.
// 보상 시스템은 Add()만 부르면 되고, UI는 Changed만 구독하면 된다.
public class AbilityInventory : MonoBehaviour
{
    public static AbilityInventory Instance { get; private set; }

    [Tooltip("시작할 때 갖고 시작하는 카드. 테스트용으로 채워 두면 편하다")]
    [SerializeField] private List<AbilityCard> startingCards = new List<AbilityCard>();

    [Tooltip("같은 카드를 여러 장 가질 수 있는지. 끄면 중복 획득이 무시된다")]
    [SerializeField] private bool allowDuplicates = true;

    private readonly List<AbilityCard> owned = new List<AbilityCard>();

    public IReadOnlyList<AbilityCard> Owned => owned;

    // 카드가 늘거나 줄 때마다 발생. UI는 이것만 구독하면 된다.
    public event System.Action Changed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        for (int i = 0; i < startingCards.Count; i++)
            if (startingCards[i] != null) owned.Add(startingCards[i]);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool Add(AbilityCard card)
    {
        if (card == null) return false;
        if (!allowDuplicates && owned.Contains(card)) return false;

        owned.Add(card);
        RaiseChanged();
        return true;
    }

    public bool Remove(AbilityCard card)
    {
        if (card == null) return false;
        if (!owned.Remove(card)) return false;

        RaiseChanged();
        return true;
    }

    public void Clear()
    {
        if (owned.Count == 0) return;
        owned.Clear();
        RaiseChanged();
    }

    // 특정 세트의 카드를 몇 장 갖고 있는지
    public int CountOf(AbilitySet set)
    {
        if (set == null) return 0;

        int n = 0;
        for (int i = 0; i < owned.Count; i++)
            if (owned[i] != null && owned[i].Set == set) n++;
        return n;
    }

    private void RaiseChanged()
    {
        if (Changed != null) Changed();
    }
}
