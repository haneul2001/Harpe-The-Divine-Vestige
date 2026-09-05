using UnityEngine;

// Animator가 자식 오브젝트(SpriteRoot)로 옮겨진 뒤,
// Animation Event가 부모의 PlayerCombat 메서드를 찾도록 위임하는 프록시.
public class PlayerAnimationEventProxy : MonoBehaviour
{
    private PlayerCombat combat;
    private PlayerStatus status;

    void Awake()
    {
        combat = GetComponentInParent<PlayerCombat>();
        status = GetComponentInParent<PlayerStatus>();
    }

    public void AttackHit()
    {
        if (combat != null) combat.AttackHit();
    }

    public void EndAttack()
    {
        if (combat != null) combat.EndAttack();
    }

    // 패링 반격 애니메이션(내려찍는 프레임)에서 호출
    public void ParryCounterHit()
    {
        if (status != null) status.ParryCounterHit();
    }
}
