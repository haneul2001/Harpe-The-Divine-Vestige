using UnityEngine;

// 보스의 한 페이즈. 체력이 줄면서 넘어가는 "단계"다.
//
// 페이즈마다 새 패턴을 여는 것이 주된 역할이고, 배수들은 거들 뿐이다 —
// 같은 패턴이라도 예고가 짧아지고 간격이 좁아지면 체감이 확 달라진다.
[System.Serializable]
public class BossPhase
{
    [Tooltip("전환할 때 화면에 띄울 이름. 비우면 안 띄운다")]
    public string label = "";

    [Tooltip("체력 비율이 이 값 이하로 내려가면 이 페이즈로 들어간다.\n"
           + "1페이즈는 1로 두고, 아래로 갈수록 작은 값을 쓴다 (예: 1 → 0.6 → 0.3)")]
    [Range(0f, 1f)] public float enterAtHpRatio = 1f;

    [Tooltip("이동 속도 배수")]
    [Min(0.1f)] public float moveSpeedMultiplier = 1f;

    [Tooltip("공격 간격 배수. 작을수록 자주 때린다")]
    [Min(0.05f)] public float attackIntervalMultiplier = 1f;

    [Tooltip("공격 예고 시간 배수. 작을수록 피하기 어렵다.\n"
           + "이걸 0에 가깝게 두면 '보고 반응할 수 없는' 공격이 되므로 0.5 아래로는 신중하게")]
    [Min(0.05f)] public float warningMultiplier = 1f;

    [Tooltip("전환 연출 시간(초). 이 동안 보스는 멈추고 피해도 받지 않는다")]
    [Min(0f)] public float transitionDuration = 1.2f;
}
