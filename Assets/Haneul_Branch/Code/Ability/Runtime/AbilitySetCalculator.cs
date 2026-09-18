using System.Collections.Generic;

// 보유 카드로부터 세트 진행도를 계산한다.
//
// MonoBehaviour가 아닌 순수 함수로 둔 이유:
//  · UI는 물론이고 나중에 스탯 적용 쪽에서도 같은 계산을 써야 한다
//  · 보상 화면에서 "이 카드를 먹으면 세트가 열리는가"를 미리 계산해 보여줄 수 있다
//    (가상의 목록을 넘기기만 하면 된다)
public struct AbilitySetProgress
{
    public AbilitySet set;
    public int owned;           // 보유 장수
    public int activeTiers;     // 열린 단계 수
    public int nextRequirement; // 다음 단계 필요 장수 (전부 열렸으면 -1)

    public bool IsActive => activeTiers > 0;
}

public static class AbilitySetCalculator
{
    // 보유 목록에 등장하는 모든 세트의 진행도를 구한다.
    // 정렬: 열린 세트 먼저 → 보유 장수 많은 순 → 이름 순 (매번 순서가 흔들리지 않게)
    public static List<AbilitySetProgress> Collect(IReadOnlyList<AbilityCard> cards)
    {
        var counts = new Dictionary<AbilitySet, int>();

        if (cards != null)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                AbilityCard c = cards[i];
                if (c == null || c.Set == null) continue;

                int n;
                counts.TryGetValue(c.Set, out n);
                counts[c.Set] = n + 1;
            }
        }

        var result = new List<AbilitySetProgress>(counts.Count);
        foreach (KeyValuePair<AbilitySet, int> kv in counts)
        {
            result.Add(new AbilitySetProgress
            {
                set = kv.Key,
                owned = kv.Value,
                activeTiers = kv.Key.ActiveTierCount(kv.Value),
                nextRequirement = kv.Key.NextRequirement(kv.Value),
            });
        }

        result.Sort(Compare);
        return result;
    }

    private static int Compare(AbilitySetProgress a, AbilitySetProgress b)
    {
        if (a.IsActive != b.IsActive) return a.IsActive ? -1 : 1;
        if (a.owned != b.owned) return b.owned.CompareTo(a.owned);
        return string.CompareOrdinal(a.set.DisplayName, b.set.DisplayName);
    }
}
