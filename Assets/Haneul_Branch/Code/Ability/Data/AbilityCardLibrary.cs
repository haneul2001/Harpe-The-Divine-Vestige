using System.Collections.Generic;
using UnityEngine;

// 프로젝트의 모든 특성 카드 목록과 등급 추첨.
//
// 카드 에셋은 Ability/Traits/<이름>/ 아래에 흩어져 있어서 빌드에서는 긁어올 방법이 없다
// (AssetDatabase는 에디터 전용). 그래서 목록을 에셋 하나에 적어 Resources에 둔다.
// 목록 갱신은 [Harpe > 능력 > 카드 목록 갱신].
[CreateAssetMenu(fileName = "AbilityCardLibrary", menuName = "Harpe/Ability/카드 목록")]
public class AbilityCardLibrary : ScriptableObject
{
    public const string ResourcePath = "Ability/AbilityCardLibrary";

    [SerializeField] private List<AbilityCard> cards = new List<AbilityCard>();

    [Tooltip("카드 그림 스킨. 씬에 설정이 없을 때 특성 선택 화면이 이걸 쓴다")]
    public AbilityUISkin skin;

    public List<AbilityCard> Cards => cards;

    private static AbilityCardLibrary cached;

    public static AbilityCardLibrary Load()
    {
        if (cached == null) cached = Resources.Load<AbilityCardLibrary>(ResourcePath);
        return cached;
    }

    public void SetCards(List<AbilityCard> value)
    {
        cards = value;
    }

    // 등급 가중치로 한 장. exclude에 든 카드는 뽑지 않는다 (같은 화면에 두 장 나오면 고르는 의미가 없다)
    public AbilityCard Roll(float[] weightByRarity, List<AbilityCard> exclude = null)
    {
        var byRarity = new Dictionary<AbilityRarity, List<AbilityCard>>();
        for (int i = 0; i < cards.Count; i++)
        {
            AbilityCard c = cards[i];
            if (c == null) continue;
            if (exclude != null && exclude.Contains(c)) continue;

            List<AbilityCard> list;
            if (!byRarity.TryGetValue(c.Rarity, out list))
            {
                list = new List<AbilityCard>();
                byRarity[c.Rarity] = list;
            }
            list.Add(c);
        }
        if (byRarity.Count == 0) return null;

        float total = 0f;
        for (int i = 0; i < weightByRarity.Length; i++)
            if (byRarity.ContainsKey((AbilityRarity)i)) total += weightByRarity[i];

        if (total > 0f)
        {
            float roll = Random.value * total;
            for (int i = 0; i < weightByRarity.Length; i++)
            {
                var rarity = (AbilityRarity)i;
                if (!byRarity.ContainsKey(rarity)) continue;

                roll -= weightByRarity[i];
                if (roll <= 0f)
                {
                    var list = byRarity[rarity];
                    return list[Random.Range(0, list.Count)];
                }
            }
        }

        // 가중치가 0이거나 부동소수점 오차로 못 골랐을 때 — 아무거나 한 장은 반드시 준다
        foreach (var pair in byRarity)
            return pair.Value[Random.Range(0, pair.Value.Count)];
        return null;
    }

    // 서로 다른 카드 count장
    public List<AbilityCard> RollDistinct(int count, float[] weightByRarity)
    {
        var picked = new List<AbilityCard>();
        for (int i = 0; i < count; i++)
        {
            AbilityCard c = Roll(weightByRarity, picked);
            if (c == null) break;
            picked.Add(c);
        }
        return picked;
    }
}
