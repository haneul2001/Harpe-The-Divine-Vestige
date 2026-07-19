using UnityEngine;

// ============================================================
// 캐릭터 스탯 데이터셋 (마비노기식 공식 적용)
//
// 설계 원칙:
//  - [기본 스탯]은 인스펙터/성장으로 올리는 원본 값
//  - [파생 스탯]은 프로퍼티로 매 참조 시 계산 (캐싱 필요해지면 그때 최적화)
//  - 마비노기 차용 요소:
//      1) 공격력은 최소~최대 "범위" + 밸런스(최대치 근처가 뜰 확률)
//      2) 스킬 대미지 = 스킬 기본 대미지 + 스킬공격력 × 스킬 계수
//      3) 방어는 비율 감산 (로그라이트 다수 잡몹 환경에 맞게 모바일식 채택)
// ============================================================
[System.Serializable]
public class CharacterStats
{
    // ─────────────────────────────────────────
    // 밸런스 계수 (여기 숫자만 만지면 게임 전체 밸런싱 조절)
    // ─────────────────────────────────────────
    public static class Coef
    {
        // STR (마비노기: 힘 2.5당 최대공격 1, 3당 최소공격 1)
        public const float StrToMaxAtk   = 0.4f;   // = 1 / 2.5
        public const float StrToMinAtk   = 0.33f;  // ≈ 1 / 3
        public const float StrToDefense  = 0.1f;   // 10당 방어 1 (적게)
        public const float StrToHp       = 0.5f;   // 2당 HP 1 (적게)

        // DEX
        public const float DexToBalance  = 0.25f;  // 4당 밸런스 1%
        public const float DexToCritRate = 0.1f;   // 10당 치확 1% (마비노기 의지 비율 차용)
        public const float DexToCritDmg  = 0.2f;   // 5당 치피 1%p
        public const float DexToAtkSpeed = 0.0005f;// 200 찍어야 +10% (굉장히 적게)

        // INT (마비노기: 지력 5당 마법공격력 1)
        public const float IntToSkillPower = 0.2f; // = 1 / 5
        public const float IntToMaxSoul    = 0.5f; // 2당 최대소울 1
        public const float IntSoulDiscount = 0.15f;// 소울 소모 감소 수확체감 강도

        // 기본값 & 상한
        public const float BaseBalance   = 50f;    // 기본 밸런스 50%
        public const float MaxBalance    = 80f;    // 마비노기도 밸런스 캡 존재
        public const float BaseCritRate  = 5f;     // 기본 치확 5%
        public const float MaxCritRate   = 50f;    // 로그라이트용 캡 (마비노기는 30)
        public const float BaseCritDmg   = 150f;   // 치명타 기본 150%
        public const float DefenseScale  = 100f;   // 방어 비율 감산 기준값
    }

    // ─────────────────────────────────────────
    // 기본 스탯 (원본 값 — 성장/장비/유물로 여기가 올라감)
    // ─────────────────────────────────────────
    [Header("체력")]
    [Tooltip("기본 체력 (STR 보너스 제외)")]
    public int baseHp = 100;

    [Header("스태미나")]
    [Tooltip("스태미나")]
    public int stamina = 100;

    [Header("소울")]
    [Tooltip("현재 소울 — 몬스터 처형 시 증가, 스킬의 마나처럼 사용")]
    public int soul = 0;
    [Tooltip("기본 최대 소울 (INT 보너스 제외)")]
    public int baseMaxSoul = 100;

    [Header("전투")]
    [Tooltip("기본 공격력 — 무기/장비에서 오는 값 (STR 보너스 제외)")]
    public int baseAttackPower = 100;
    [Tooltip("기본 방어력 (STR 보너스 제외)")]
    public int baseDefense = 0;

    [Header("능력치")]
    [Tooltip("힘 — 공격력, 방어력(적게), 체력(적게)")]
    public int str = 0;
    [Tooltip("민첩 — 밸런스, 치명타 확률/피해, 공격속도(굉장히 적게)")]
    public int dex = 0;
    [Tooltip("지능 — 스킬 공격력, 최대 소울, 소울 소모 감소")]
    public int intel = 0;

    // ─────────────────────────────────────────
    // 파생 스탯 (읽기 전용 — 다른 시스템은 전부 이걸 참조)
    // ─────────────────────────────────────────

    /// <summary>최종 최대 체력</summary>
    public int MaxHp => baseHp + Mathf.FloorToInt(str * Coef.StrToHp);

    /// <summary>최종 방어력</summary>
    public int Defense => baseDefense + Mathf.FloorToInt(str * Coef.StrToDefense);

    /// <summary>물리 최대 공격력 (마비노기: 힘 2.5당 1)</summary>
    public int MaxAttack => baseAttackPower + Mathf.FloorToInt(str * Coef.StrToMaxAtk);

    /// <summary>물리 최소 공격력 (마비노기: 힘 3당 1) — 최대보다 항상 낮거나 같음</summary>
    public int MinAttack
    {
        get
        {
            // 기본 공격력의 70%를 하한으로 시작 + str 보너스
            int min = Mathf.FloorToInt(baseAttackPower * 0.7f) + Mathf.FloorToInt(str * Coef.StrToMinAtk);
            return Mathf.Min(min, MaxAttack); // 마비노기 규칙: 민뎀은 맥뎀을 못 넘음
        }
    }

    /// <summary>밸런스(%) — 높을수록 최대 공격력에 가까운 값이 굴려짐</summary>
    public float Balance => Mathf.Min(Coef.BaseBalance + dex * Coef.DexToBalance, Coef.MaxBalance);

    /// <summary>치명타 확률(%)</summary>
    public float CritRate => Mathf.Min(Coef.BaseCritRate + dex * Coef.DexToCritRate, Coef.MaxCritRate);

    /// <summary>치명타 피해 배율(%) — 150 = 1.5배</summary>
    public float CritDamage => Coef.BaseCritDmg + dex * Coef.DexToCritDmg;

    /// <summary>공격속도 배율 — 1.0 기준, 애니메이션/쿨다운에 곱해서 사용</summary>
    public float AttackSpeedMult => 1f + dex * Coef.DexToAtkSpeed;

    /// <summary>스킬 공격력 (마비노기 마법공격력 포지션: 지능 5당 1)</summary>
    public int SkillPower => Mathf.FloorToInt(intel * Coef.IntToSkillPower);

    /// <summary>최종 최대 소울</summary>
    public int MaxSoul => baseMaxSoul + Mathf.FloorToInt(intel * Coef.IntToMaxSoul);

    /// <summary>
    /// 소울 소모 배율 (수확체감식 — 절대 0이 안 됨)
    /// intel 0 → 1.0배 / 100 → 0.87배 / 200 → 0.77배 / 400 → 0.63배
    /// </summary>
    public float SoulCostMult => 100f / (100f + intel * Coef.IntSoulDiscount);

    // ─────────────────────────────────────────
    // 대미지 계산
    // ─────────────────────────────────────────

    /// <summary>
    /// 물리 공격 1회의 대미지를 굴림 (마비노기식: 범위 + 밸런스 + 치명타)
    /// </summary>
    /// <param name="isCrit">치명타 발동 여부 (이펙트/사운드 분기용)</param>
    public int RollPhysicalDamage(out bool isCrit)
    {
        float dmg = RollWithBalance(MinAttack, MaxAttack);
        isCrit = RollCrit(ref dmg);
        return Mathf.Max(1, Mathf.RoundToInt(dmg));
    }

    /// <summary>
    /// 스킬 대미지 (마비노기 공식: 스킬 기본 대미지 + 스킬공격력 × 스킬 계수)
    /// 예) 파이어볼: skillBaseDamage=50, coefficient=3.0
    /// </summary>
    public int RollSkillDamage(int skillBaseDamage, float coefficient, out bool isCrit)
    {
        float dmg = skillBaseDamage + SkillPower * coefficient;
        // 스킬도 밸런스의 영향을 절반만 받게 — 스킬은 물리보다 안정적인 대미지
        float balanceRoll = Mathf.Lerp(0.85f, 1f, RollBalance01());
        dmg *= balanceRoll;
        isCrit = RollCrit(ref dmg);
        return Mathf.Max(1, Mathf.RoundToInt(dmg));
    }

    /// <summary>
    /// 받는 피해 계산 (비율 감산 — 마비노기 모바일식)
    /// 고정 감산은 잡몹 다수의 약한 공격을 전부 0으로 만들어 로그라이트에 부적합.
    /// 이 공식은 방어 100 = 피해 50% 감소, 방어 300 = 75% 감소 (수확체감).
    /// </summary>
    public int CalcIncomingDamage(int rawDamage)
    {
        float reduced = rawDamage * (Coef.DefenseScale / (Coef.DefenseScale + Defense));
        return Mathf.Max(1, Mathf.RoundToInt(reduced)); // 최소 1은 항상 들어옴
    }

    /// <summary>스킬 사용 시도 — 소울이 충분하면 차감하고 true</summary>
    public bool TrySpendSoul(int baseCost)
    {
        int actualCost = Mathf.Max(1, Mathf.RoundToInt(baseCost * SoulCostMult));
        if (soul < actualCost) return false;
        soul -= actualCost;
        return true;
    }

    /// <summary>처형 등으로 소울 획득</summary>
    public void GainSoul(int amount)
    {
        soul = Mathf.Min(soul + amount, MaxSoul);
    }

    // ─────────────────────────────────────────
    // 내부 헬퍼
    // ─────────────────────────────────────────

    /// <summary>
    /// 밸런스가 반영된 0~1 굴림.
    /// 두 번 굴려 밸런스만큼 큰 값 쪽으로 치우치게 함 —
    /// 밸런스 50% = 균등분포, 80% = 대부분 최대치 근처.
    /// </summary>
    private float RollBalance01()
    {
        float a = Random.value;
        float b = Random.value;
        float high = Mathf.Max(a, b);
        float flat = a;
        // 밸런스 50%를 균등분포 기준점으로 사용
        float t = Mathf.InverseLerp(Coef.BaseBalance, Coef.MaxBalance, Balance);
        return Mathf.Lerp(flat, high, t);
    }

    private float RollWithBalance(int min, int max)
    {
        return Mathf.Lerp(min, max, RollBalance01());
    }

    /// <summary>치명타 판정 후 dmg에 배율 적용. 발동 여부 반환</summary>
    private bool RollCrit(ref float dmg)
    {
        if (Random.value * 100f < CritRate)
        {
            dmg *= CritDamage / 100f;
            return true;
        }
        return false;
    }
}