using System.Collections;
using UnityEngine;

// 보스 공격 하나. 보스 오브젝트에 컴포넌트로 붙여 두면 BossEnemy가 알아서 모아 쓴다.
//
// 새 보스를 만드는 일은 결국 "이걸 세 개쯤 쓰는 것"이 된다.
// 예고 → 판정 → 회복 같은 공통 흐름은 AttackEnemyBase가 이미 처리하므로,
// 여기서 쓸 것은 "발동 구간에 무슨 짓을 하는가"뿐이다.
public abstract class BossPattern : MonoBehaviour
{
    [Header("패턴 공통")]
    [Tooltip("로그와 디버깅용 이름. 비우면 클래스 이름을 쓴다")]
    [SerializeField] private string patternName = "";

    [Tooltip("이 패턴이 열리는 페이즈 (0 = 1페이즈부터)")]
    [Min(0)] [SerializeField] private int minPhase = 0;

    [Tooltip("이 패턴이 닫히는 페이즈. -1이면 끝까지 쓴다.\n"
           + "후반에 사라지는 패턴을 만들 때만 쓴다")]
    [SerializeField] private int maxPhase = -1;

    [Tooltip("뽑힐 가중치. 클수록 자주 나온다. 0이면 안 나온다")]
    [Min(0f)] [SerializeField] private float weight = 1f;

    [Tooltip("쓰고 나서 다시 쓸 수 있을 때까지의 시간(초). 0이면 제한 없음")]
    [Min(0f)] [SerializeField] private float cooldown = 0f;

    [Tooltip("플레이어가 이 거리 안에 있어야 쓴다. 0이면 거리 무시")]
    [Min(0f)] [SerializeField] private float maxDistance = 0f;

    [Tooltip("플레이어가 이 거리 밖에 있어야 쓴다. 접근 후에는 안 쓰는 원거리 패턴용")]
    [Min(0f)] [SerializeField] private float minDistance = 0f;

    [Tooltip("이 패턴이 쓸 공격 애니메이션 번호.\n"
           + "애니메이터의 atkIndex 파라미터로 넘어가고, 컨트롤러가 그 번호의 클립으로 분기한다.\n"
           + "atkIndex 파라미터가 없는 애니메이터면 그냥 무시된다")]
    [Min(0)] [SerializeField] private int animationIndex = 0;

    public int AnimationIndex { get { return animationIndex; } }

    private float lastUsedTime = -999f;

    public string Name
    {
        get { return string.IsNullOrEmpty(patternName) ? GetType().Name : patternName; }
    }

    public float Weight { get { return weight; } }

    // 지금 이 패턴을 뽑아도 되는가.
    // 조건을 여기 모아 두면 새 패턴은 Run만 쓰면 되고, 선택 로직은 손대지 않는다.
    public virtual bool IsUsable(BossEnemy boss)
    {
        if (weight <= 0f) return false;
        if (boss.PhaseIndex < minPhase) return false;
        if (maxPhase >= 0 && boss.PhaseIndex > maxPhase) return false;
        if (cooldown > 0f && Time.time - lastUsedTime < cooldown) return false;

        float d = boss.DistanceToPlayer();
        if (maxDistance > 0f && d > maxDistance) return false;
        if (minDistance > 0f && d < minDistance) return false;

        return true;
    }

    public void MarkUsed()
    {
        lastUsedTime = Time.time;
    }

    // 쿨다운은 전투 단위다. 방을 다시 들어오거나 보스가 새로 스폰되면 처음부터.
    public void ResetCooldown()
    {
        lastUsedTime = -999f;
    }

    // 실제 동작. 여기서 yield하는 동안 보스는 "공격 중"이다.
    public abstract IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer);

    // 이 패턴이 도는 동안 보스가 스스로 움직이는가.
    // true면 발동 구간에 위치 잠금을 푼다(돌진처럼).
    public virtual bool MovesSelf { get { return false; } }
}
