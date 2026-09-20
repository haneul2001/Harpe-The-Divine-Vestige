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

    // 효과마다 따로 쓰는 런타임 상태 (스택·쿨타임·타이머). 효과 에셋 필드에 두면 에디터에까지 남는다
    private readonly System.Collections.Generic.Dictionary<AbilityEffect, object> states =
        new System.Collections.Generic.Dictionary<AbilityEffect, object>();

    public AbilityContext(GameObject player, MonoBehaviour runner)
    {
        this.player = player;
        this.playerTransform = player != null ? player.transform : null;
        this.status = player != null ? player.GetComponent<PlayerStatus>() : null;
        this.runner = runner;
    }

    public T State<T>(AbilityEffect owner) where T : class, new()
    {
        object o;
        if (!states.TryGetValue(owner, out o) || !(o is T))
        {
            o = new T();
            states[owner] = o;
        }
        return (T)o;
    }

    public void ClearState(AbilityEffect owner)
    {
        states.Remove(owner);
    }

    // 몸 중심 (발 기준 위치 + 몸 높이)
    public Vector3 BodyCenter
    {
        get { return playerTransform != null ? playerTransform.position + Vector3.up * 0.32f : Vector3.zero; }
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

    // ─── 사건 (AbilityHooks.Notify… 가 뿌린다) ───
    public virtual void OnTick(AbilityContext ctx, float dt) { }
    public virtual void OnEnemyHit(AbilityContext ctx, HitInfo hit) { }
    public virtual void OnEnemyKilled(AbilityContext ctx, Enemy enemy) { }      // 처형으로 죽은 것도 포함
    public virtual void OnSwing(AbilityContext ctx, SwingInfo swing) { }        // 평타를 휘두른 순간
    public virtual void OnParrySuccess(AbilityContext ctx, Enemy attacker) { }
    public virtual void OnDashStart(AbilityContext ctx, Vector2 start, Vector2 dir) { }
    public virtual void OnDashEnd(AbilityContext ctx, Vector2 start, Vector2 end) { }
    public virtual void OnStealthEnter(AbilityContext ctx) { }
    public virtual void OnStealthExit(AbilityContext ctx) { }
    public virtual void OnSoulGained(AbilityContext ctx, int amount) { }

    // ─── 수치 (매번 다시 물어본다 — 카드를 빼면 값도 바로 사라진다) ───
    public virtual float DamageMultiplier(AbilityContext ctx, DamageQuery q) { return 1f; }
    public virtual bool ForcesCrit(AbilityContext ctx, DamageKind kind) { return false; }
    public virtual float AttackSpeedBonus(AbilityContext ctx) { return 0f; }      // 0.15 = +15%
    public virtual float MoveSpeedBonus(AbilityContext ctx) { return 0f; }
    public virtual float CritRateBonus(AbilityContext ctx) { return 0f; }         // %p
    public virtual float CritDamageBonus(AbilityContext ctx) { return 0f; }       // %p
    public virtual float MaxHpBonus(AbilityContext ctx) { return 0f; }
    public virtual float DefenseBonus(AbilityContext ctx) { return 0f; }
    public virtual float HarvestThresholdBonus(AbilityContext ctx) { return 0f; } // 0.1 = 처형 가능 체력 +10%p
    public virtual float HarvestSoulBonus(AbilityContext ctx) { return 0f; }      // 0.5 = +50%
    public virtual bool GrantsProjectilePierce(AbilityContext ctx) { return false; }
    public virtual float LungeBonus(AbilityContext ctx, bool consume) { return 0f; }

    // ─── 세트 시너지용 ───
    public virtual float StatAmplify(AbilityContext ctx) { return 0f; }            // 0.15 = 스탯 카드 수치 +15%
    public virtual float ProjectileSizeBonus(AbilityContext ctx) { return 0f; }    // 0.2 = 투사체 크기 +20%
    public virtual float ProjectileRangeBonus(AbilityContext ctx) { return 0f; }
    public virtual float SoulGainBonus(AbilityContext ctx) { return 0f; }          // 모든 소울 획득 +%
    public virtual float DashCooldownReduction(AbilityContext ctx) { return 0f; }  // 0.2 = 대시 쿨타임 -20%
    public virtual float ParryLockReduction(AbilityContext ctx) { return 0f; }     // 패링 후 조작 불가 시간 -초
    public virtual float SkillCooldownReduction(AbilityContext ctx, PlayerSkill skill) { return 0f; }  // -초
    public virtual bool GrantsHarvestInvincibility(AbilityContext ctx) { return false; }
}
