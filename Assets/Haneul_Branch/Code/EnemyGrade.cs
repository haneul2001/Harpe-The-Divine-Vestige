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

    // 이름·테두리·등급칩에 쓰는 강조색
    public static Color Accent(this EnemyGrade grade)
    {
        switch (grade)
        {
            case EnemyGrade.Boss:  return new Color(0.96f, 0.62f, 0.24f);
            case EnemyGrade.Elite: return new Color(0.72f, 0.46f, 0.95f);
            default:               return new Color(0.80f, 0.82f, 0.86f);
        }
    }

    // 체력 채움색. 강조색보다 진해야 글자가 위에서 읽힌다.
    public static Color Fill(this EnemyGrade grade)
    {
        switch (grade)
        {
            case EnemyGrade.Boss:  return new Color(0.82f, 0.20f, 0.16f);
            case EnemyGrade.Elite: return new Color(0.52f, 0.24f, 0.78f);
            default:               return new Color(0.38f, 0.62f, 0.34f);
        }
    }

    // 등급이 올라갈수록 바가 커진다. 한눈에 "격"이 보이게 하는 부분.
    // 값은 1920 기준. 화면이 좁으면 EnemyHealthBar가 알아서 줄여 준다.
    public static float BarWidth(this EnemyGrade grade)
    {
        switch (grade)
        {
            case EnemyGrade.Boss:  return 1800f;
            case EnemyGrade.Elite: return 1280f;
            default:               return 920f;
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
