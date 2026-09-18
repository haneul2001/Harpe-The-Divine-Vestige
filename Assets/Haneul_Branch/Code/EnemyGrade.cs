using UnityEngine;

// 적 등급. 상단 체력바의 색·이름·표기가 전부 이 값을 따라간다.
// 순서를 바꾸면 이미 저장된 프리팹의 직렬화 값이 어긋나므로 뒤에만 추가할 것.
public enum EnemyGrade
{
    Normal = 0,   // 일반 잡몹
    Elite  = 1,   // 정예 (방에 하나씩 섞는 강화 몹)
    Boss   = 2,   // 보스
}

// 등급 표현은 여기 한 곳에서만 정한다.
// 색을 바꾸려면 이 파일만 고치면 체력바 전체가 따라온다. (AbilityRarityUtil과 같은 방식)
public static class EnemyGradeUtil
{
    public static string Label(this EnemyGrade grade)
    {
        switch (grade)
        {
            case EnemyGrade.Boss:  return "보스";
            case EnemyGrade.Elite: return "정예";
            default:               return "일반";
        }
    }

    // 이름·등급칩에 쓰는 강조색.
    // 사신 컨셉에 맞춰 회색~연보라 계열로만 간다. 채도가 높으면 화면에서 UI만 튄다.
    public static Color Accent(this EnemyGrade grade)
    {
        switch (grade)
        {
            case EnemyGrade.Boss:  return new Color(0.741f, 0.667f, 0.867f);  // 창백한 연보라
            case EnemyGrade.Elite: return new Color(0.573f, 0.502f, 0.706f);  // 중간 보라회색
            default:               return new Color(0.616f, 0.635f, 0.671f);  // 회색
        }
    }

    // 체력 채움색. 어두운 프레임 위에 얹히므로 진하되 탁하게 간다.
    // 완전한 회색으로 두면 "체력"으로 안 읽히므로 붉은 기운은 남긴다.
    public static Color Fill(this EnemyGrade grade)
    {
        switch (grade)
        {
            case EnemyGrade.Boss:  return new Color(0.596f, 0.157f, 0.208f);  // 짙은 핏빛
            case EnemyGrade.Elite: return new Color(0.463f, 0.216f, 0.439f);  // 진한 자주
            default:               return new Color(0.435f, 0.216f, 0.275f);  // 어두운 적자
        }
    }

    // 등급이 올라갈수록 바가 커진다. 한눈에 "격"이 보이게 하는 부분.
    // 값은 1920 기준. 화면이 좁으면 EnemyHealthBar가 알아서 줄여 준다.
    public static float BarWidth(this EnemyGrade grade)
    {
        switch (grade)
        {
            case EnemyGrade.Boss:  return 1200f;
            case EnemyGrade.Elite: return 850f;
            default:               return 620f;
        }
    }

    // 보스는 체력바를 여러 칸으로 나눠 남은 양을 읽기 쉽게 한다. 1이면 칸 나눔 없음.
    public static int Segments(this EnemyGrade grade)
    {
        switch (grade)
        {
            case EnemyGrade.Boss:  return 4;
            case EnemyGrade.Elite: return 2;
            default:               return 1;
        }
    }
}
