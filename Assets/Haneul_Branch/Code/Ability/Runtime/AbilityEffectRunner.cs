using System.Collections.Generic;
using UnityEngine;

// 보유 카드의 효과를 실제로 굴리는 곳. 능력 시스템의 "매니저".
//
// 설계 요점 — 이 클래스는 개별 카드를 하나도 모른다.
//   · 게임 이벤트 구독은 전부 여기서만 한다 (효과가 각자 구독하면 해제를 반드시 빠뜨린다)
//   · 이벤트가 오면 활성 효과 전부에게 그대로 뿌린다
//   · 카드를 2장 가지면 효과도 2번 호출된다 → 중첩이 공짜로 동작한다
//
// 카드를 새로 추가할 때 이 파일은 건드리지 않는다.
// 새 "트리거 종류"가 필요할 때만 구독 한 줄 + 뿌리기 한 줄이 늘어난다.
[RequireComponent(typeof(AbilityInventory))]
public class AbilityEffectRunner : MonoBehaviour
{
    [Tooltip("효과가 붙을 대상. 비우면 이 오브젝트를 쓴다")]
    [SerializeField] private GameObject target;

    private AbilityInventory inventory;
    private AbilityContext context;

    // 지금 적용 중인 효과들. 카드 중복 보유 시 같은 효과가 여러 번 들어간다.
    private readonly List<AbilityEffect> active = new List<AbilityEffect>();

    private HarvestManager harvest;

    private void Awake()
    {
        inventory = GetComponent<AbilityInventory>();
        if (target == null) target = gameObject;

        context = new AbilityContext(target, this);
    }

    private bool started;

    private void OnEnable()
    {
        inventory.Changed += Rebuild;

        // 첫 활성화 때는 Start까지 전부 미룬다 (아래 주석 참고).
        // 여기서 Rebuild를 부르면 AbilityInventory.Awake가 아직 안 돌아
        // 빈 목록을 읽고 효과가 하나도 안 붙는다.
        if (started)
        {
            SubscribeGameEvents();
            Rebuild();
        }
    }

    // ★ 초기화를 Awake/OnEnable이 아니라 Start에서 하는 이유 (두 가지 다)
    //   1) HarvestManager.Instance는 그쪽 Awake에서 채워지는데 컴포넌트 간
    //      Awake 순서는 보장되지 않는다 → OnEnable에서 잡으면 조용히 구독 실패
    //   2) AbilityInventory.owned도 그쪽 Awake에서 채워진다
    //      → OnEnable에서 Rebuild하면 빈 목록을 읽어 효과가 0개가 된다
    //   Start 시점에는 씬의 모든 Awake가 끝나 있어 둘 다 안전하다.
    private void Start()
    {
        started = true;
        SubscribeGameEvents();
        Rebuild();
    }

    private void OnDisable()
    {
        inventory.Changed -= Rebuild;
        UnsubscribeGameEvents();
        ClearActive();
    }

    // ─────────────────────────────────────────────
    // 게임 이벤트 구독 — 트리거가 늘면 여기에만 추가된다
    // ─────────────────────────────────────────────

    [Tooltip("구독/발동 상황을 콘솔에 남긴다. 효과가 안 터질 때 원인 찾기용")]
    [SerializeField] private bool debugLog = false;

    private void SubscribeGameEvents()
    {
        if (harvest != null) return;   // 중복 구독 방지

        harvest = HarvestManager.Instance;

        if (harvest != null)
        {
            harvest.ImpactLanded += OnHarvestImpact;
            if (debugLog) Debug.Log("[AbilityEffectRunner] HarvestManager 구독 완료", this);
        }
        else
        {
            Debug.LogWarning("[AbilityEffectRunner] HarvestManager를 찾지 못함 — 처형 관련 효과가 발동하지 않는다", this);
        }
    }

    private void UnsubscribeGameEvents()
    {
        if (harvest != null) harvest.ImpactLanded -= OnHarvestImpact;
        harvest = null;
    }

    private void OnHarvestImpact(HarvestImpact impact)
    {
        if (debugLog)
            Debug.Log($"[AbilityEffectRunner] 처형 임팩트 @{impact.position} — 활성 효과 {active.Count}개", this);

        for (int i = 0; i < active.Count; i++)
        {
            AbilityEffect e = active[i];
            if (e != null) e.OnHarvestImpact(context, impact);
        }
    }

    // ─────────────────────────────────────────────
    // 보유 카드 → 활성 효과
    // ─────────────────────────────────────────────

    // 카드가 바뀌면 통째로 다시 만든다.
    // 증분 갱신(추가된 것만 적용)은 빨라 보이지만, 상시 효과가 중복 적용되거나
    // 해제가 누락되는 버그를 반드시 만든다. 카드 수십 장 규모에선 전체 갱신이 안전하고 충분하다.
    private void Rebuild()
    {
        ClearActive();

        IReadOnlyList<AbilityCard> owned = inventory.Owned;
        for (int i = 0; i < owned.Count; i++)
        {
            AbilityCard card = owned[i];
            if (card == null || card.Effects == null) continue;

            for (int e = 0; e < card.Effects.Length; e++)
            {
                AbilityEffect effect = card.Effects[e];
                if (effect == null) continue;

                active.Add(effect);
                effect.OnAcquire(context);
            }
        }
    }

    private void ClearActive()
    {
        for (int i = active.Count - 1; i >= 0; i--)
            if (active[i] != null) active[i].OnRemove(context);

        active.Clear();
    }
}
