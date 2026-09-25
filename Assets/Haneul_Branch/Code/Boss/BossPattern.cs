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

    [Tooltip("예고가 끝난 뒤 실제로 칼이 닿기까지 걸리는 시간(초).\n"
           + "이 패턴이 쓰는 공격 애니메이션의 타격 이벤트 시각과 같게 둔다.\n"
           + "음수면 보스의 기본 추정값을 쓰고, 한 번 때리고 나면 실제로 잰 값으로 바뀐다")]
    [SerializeField] private float hitDelay = -1f;

    public int AnimationIndex { get { return animationIndex; } }
    public float HitDelay { get { return hitDelay; } }

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

    // 이 패턴이 바닥에 덮는 자리. 기본은 히트박스 상자 그대로다.
    //
    // 실제 표시도 이 값으로 만들고 툴도 이 값을 그린다. 크기를 두 군데서 따로 계산하면
    // 숫자를 고쳤을 때 게임과 툴이 다른 말을 하게 된다.
    public virtual DangerShape[] DangerShapes(BossEnemy boss)
    {
        // 베기는 호를 그리는 공격이라 부채꼴로 예고한다.
        // 판정 상자는 네모지만, 네모로 보여 주면 이펙트와 모양이 달라 어색해진다.
        return new DangerShape[] { DangerShape.SectorFromBox(HitBoxShape(boss).size, DangerOrigin.Boss) };
    }

    // 보스의 공격 판정 상자를 그대로 잰다 (BossEnemy.ShowHitBoxDanger가 쓰는 그 상자)
    public static DangerShape HitBoxShape(BossEnemy boss)
    {
        if (boss == null || boss.HitBoxObject == null) return new DangerShape();

        Transform t = boss.HitBoxObject.transform;

        // 실제 콜라이더는 "판정 여유"만큼 줄어 있고, 런타임에는 부채꼴로 바뀌어 있다.
        // 예고와 이펙트는 줄기 전 상자 크기를 써야 보이는 범위가 그대로 유지된다.
        Vector2 size = boss.HitBoxDisplaySize;
        if (size.x <= 0.001f || size.y <= 0.001f)
        {
            // 아직 시작 전(프리팹을 읽는 툴)이면 저장된 상자를 그대로 잰다
            var box = boss.HitBoxObject.GetComponent<BoxCollider2D>();
            if (box == null) return new DangerShape();

            Vector3 ls = t.lossyScale;
            size = new Vector2(box.size.x * Mathf.Abs(ls.x), box.size.y * Mathf.Abs(ls.y));
        }

        float forward = Vector2.Distance(t.position, boss.transform.position);

        return DangerShape.Box(size, forward, DangerOrigin.HitBox).ScaledWithBoss();
    }

    // 베기 그림이 덮을 자리. 기본은 예고한 칸과 같다.
    //
    // 둘이 다를 수 있다 — 돌진은 "지나갈 길 전체"를 예고하지만 칼은 칼 길이만큼만 긋는다.
    // 예고를 늘렸다고 검기까지 늘어나면 칼이 갑자기 두 배로 길어져 보인다.
    public virtual DangerShape SlashShape(BossEnemy boss)
    {
        // 예고는 부채꼴이지만 그림은 네모다 — 베기 그림이 사각형 한 장이라
        // 부채꼴을 그대로 넘기면 늘려 그릴 가로세로가 없어 옛 방식으로 떨어진다.
        DangerShape box = HitBoxShape(boss);
        if (!box.Valid) return box;

        DangerShape[] shapes = DangerShapes(boss);
        if (shapes == null || shapes.Length == 0 || !shapes[0].IsSector) return box;

        // 길이는 예고한 부채꼴에 맞춘다. 돌진처럼 멀리까지 예고해 놓고 검기만 짧으면
        // "저기까지 벤다"가 안 읽힌다. 높이는 칼 두께라 그대로 둔다.
        float reach = shapes[0].Radius;
        return DangerShape.Box(new Vector2(reach, box.size.y), reach * 0.5f, DangerOrigin.Boss);
    }

    // 예고 동안 보여 줄 위험지역. 기본은 히트박스 모양 그대로다.
    // 장판·투사체처럼 히트박스를 안 쓰는 패턴은 여기서 자기 모양을 그린다.
    public virtual DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration)
    {
        return boss.ShowHitBoxDanger(duration, dirToPlayer);
    }

    // 실제 동작. 여기서 yield하는 동안 보스는 "공격 중"이다.
    public abstract IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer);

    // 이 패턴이 보스의 근접 판정 상자를 쓰는가.
    //
    // false면 상자를 아예 열지 않는다. Run에서 닫는 것만으로는 한 프레임이 새고,
    // 그 한 프레임에 붙어 있던 플레이어가 맞는다 — 판정이 없다던 패턴에 패링이 뜨는 이유다.
    public virtual bool UsesHitBox { get { return true; } }

    // 이 패턴이 자기 베기 그림을 직접 그리는가.
    // true면 보스의 기본 베기 이펙트는 뜨지 않는다.
    public virtual bool DrawsOwnSlash { get { return false; } }

    // 이 패턴이 도는 동안 보스가 스스로 움직이는가.
    // true면 발동 구간에 위치 잠금을 푼다(돌진처럼).
    public virtual bool MovesSelf { get { return false; } }
}
