using UnityEngine;

// 2층(버려진 유적)의 새끼 용들 공통 부분.
//
// 넷 다 공중에 떠 있는 작은 용이라 좌우 줄을 맞출 필요 없이 어느 각도로든 공격한다.
// 근접 판정 상자(히트박스)는 쓰지 않는다 — 돌진·벼락·불꽃 모두 자기 방식으로 피해를 굴린다.
public abstract class BabyDragonBase : AttackEnemyBase
{
    protected static readonly int IdleHash = Animator.StringToHash("Idle");
    protected static readonly int FlyingHash = Animator.StringToHash("Flying");

    protected override bool OpensHitBox { get { return false; } }
    protected override bool ShouldSpawnSlash { get { return false; } }

    protected Transform PlayerTransform
    {
        get
        {
            if (player != null) return player;
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            return p != null ? p.transform : null;
        }
    }

    protected Vector2 PlayerPos
    {
        get { Transform p = PlayerTransform; return p != null ? (Vector2)p.position : (Vector2)transform.position; }
    }

    protected Vector2 DirToPlayer()
    {
        Vector2 d = PlayerPos - (Vector2)transform.position;
        return d.sqrMagnitude > 0.0001f ? d.normalized : (IsFacingRight ? Vector2.right : Vector2.left);
    }

    // 좌우만 돌린다 (그림이 옆모습이다)
    protected void FaceDir(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) < 0.01f) return;
        bool right = dir.x > 0f;
        if (right != IsFacingRight) SetFacing(right);
    }

    // 원 안에 플레이어가 있으면 피해. 보이는 원보다 판정이 조금 작다 (보스와 같은 규칙).
    // 받아칠 수 있다 — 공격한 쪽을 넘긴다
    protected bool HurtPlayerInRadius(Vector2 center, float radius)
    {
        Transform p = PlayerTransform;
        if (p == null) return false;
        if (Vector2.Distance(p.position, center) > ShrinkRadius(radius)) return false;

        PlayerStatus status = p.GetComponent<PlayerStatus>();
        if (status == null) status = p.GetComponentInParent<PlayerStatus>();
        return status != null && status.TakeDamage(AttackDamage, this);
    }

    protected void PlayState(int hash)
    {
        if (animator == null) return;
        animator.ResetTrigger("attack");
        animator.Play(hash, 0, 0f);
    }
}
