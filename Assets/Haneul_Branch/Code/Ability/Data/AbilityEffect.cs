using UnityEngine;

// 처형 임팩트가 일어난 사실. 어디서, 누구를 처형했는지만 담는다.
public struct HarvestImpact
{
    public Vector3 position;
    public Enemy target;

    public HarvestImpact(Vector3 position, Enemy target)
    {
        this.position = position;
        this.target = target;
    }
}

// 효과가 게임에 손대기 위해 필요한 것들.
// 싱글톤을 여기저기서 직접 부르면 테스트도 못 하고 의존성도 안 보인다.
// (기존 SkillContext와 같은 발상)
public class AbilityContext
{
    public GameObject player;
    public Transform playerTransform;
    public PlayerStatus status;
    public MonoBehaviour runner;   // 코루틴이 필요한 효과용

    public AbilityContext(GameObject player, MonoBehaviour runner)
    {
        this.player = player;
        this.playerTransform = player != null ? player.transform : null;
        this.status = player != null ? player.GetComponent<PlayerStatus>() : null;
        this.runner = runner;
    }
}

// 카드가 실제로 하는 일 한 가지.
//
// 카드 1장 = 효과 여러 개일 수 있다 ("처형 시 충격파 + 소울 +2" 같은 카드).
// 그래서 카드가 효과를 소유하고, 효과는 카드를 모른다.
//
// ★ 중요: 이 에셋은 모든 플레이어/세션이 공유하는 하나의 인스턴스다.
//   런타임 상태(쿨타임, 스택 수 등)를 여기 필드에 저장하면 에디터에까지 남는다.
//   상태가 필요하면 AbilityContext나 러너 쪽에 두고, 효과는 무상태로 유지할 것.
//
// 훅 추가 방법: 여기에 virtual 메서드를 하나 추가하고,
//   AbilityEffectRunner에서 해당 게임 이벤트를 구독해 뿌려 주면 끝이다.
//   기존 효과들은 아무것도 고칠 필요가 없다.
public abstract class AbilityEffect : ScriptableObject
{
    [Tooltip("툴팁에 덧붙일 한 줄 설명 (선택). 비우면 카드 설명만 쓴다")]
    [TextArea(1, 3)]
    [SerializeField] private string summary = "";

    public string Summary => summary;

    // 카드를 얻은 순간 / 잃은 순간. 스탯 증가처럼 상시로 붙는 효과가 여기를 쓴다.
    public virtual void OnAcquire(AbilityContext ctx) { }
    public virtual void OnRemove(AbilityContext ctx) { }

    // 처형이 적중한 순간.
    // 같은 카드를 2장 갖고 있으면 2번 불린다 — 중첩이 자연스럽게 동작한다.
    public virtual void OnHarvestImpact(AbilityContext ctx, HarvestImpact impact) { }
}
