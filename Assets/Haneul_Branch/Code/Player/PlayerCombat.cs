using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private float harvestRange = 5f;

    [Tooltip("체크 시 처형할 때 적 '뒤'가 아니라 적 '좌표'로 순간이동 (예: Reaper 내려찍기). 캐릭터별로 설정.")]
    [SerializeField] private bool harvestTeleportOntoTarget = false;
    public bool HarvestTeleportOntoTarget => harvestTeleportOntoTarget;

    [Header("공격 속도")]
    [Tooltip("일반 공격 애니메이션 재생 배율. 1 = 기본, 2 = 2배 빠름")]
    [SerializeField] private float attackSpeed = 1f;

    [Header("데미지")]
    [Tooltip("차징(풀차징) 공격 데미지 배율. 일반 공격 대비 몇 배인지.")]
    [SerializeField] private float chargedDamageMultiplier = 2f;

    private SpriteRenderer sr;
    private Animator anim;
    private PlayerOutline outline;
    private PlayerStatus status;

    public bool isAttacking = false;
    public bool isCharging = false;

    [Header("차징 공격 스킬 해금")]
    [Tooltip("체크 시 Z 홀드로 차징 공격 가능. 해제 시 차징 없이 일반 공격만 나감.")]
    public bool HasChargingAttackSkill = false;

    private float chargeTime;
    [SerializeField] private float maxChargeTime = 1.5f;
    [SerializeField] private float chargedThreshold = 1.4f;//풀차징 기준

    public int attackNum = 0;

    [Header("공격 예약 (선입력)")]
    [Tooltip("공격 중에 공격키를 누르면 다음 타가 예약되어 공격이 끝나는 즉시 이어진다. "
           + "여기 적은 시간 안에 눌린 입력만 유효하다. 창을 넓히면 초반에 누른 입력까지 살아나 "
           + "손을 뗀 뒤에도 계속 휘두르고, 좁히면 예약이 잘 안 걸린다. 0.2~0.3이 무난하다.")]
    [SerializeField] private float attackBufferWindow = 0.25f;

    // 마지막으로 예약된 입력 시각. 공격이 끝나는 순간 유효한지 확인한다.
    private float bufferedAttackTime = -999f;

    [Header("공격 종료 안전장치")]
    [Tooltip("isAttacking을 끄는 건 애니메이션 이벤트(EndAttack) 하나뿐이라, 이벤트가 씹히면 "
           + "플레이어가 영원히 공격 중으로 묶여 이동도 공격도 안 된다. "
           + "적 쪽 공격 코드와 같은 방식으로, 이 시간이 지나면 강제로 공격을 끝낸다. "
           + "가장 긴 공격 클립(0.83초)보다 넉넉해야 하며 공격 속도 배율만큼 줄어든다.")]
    [SerializeField] private float attackFailSafeTime = 1.2f;

    // 이 시각을 넘기면 강제 종료. 0 이하면 감시하지 않음.
    private float attackFailSafeDeadline = -1f;

    [Header("콤보")]
    [Tooltip("콤보 타수. 애니메이터의 Attack Blend 트리에 등록된 모션 수와 반드시 같아야 한다. "
           + "Reaper는 Slash(단타) + Double Slash(2연타 마무리)로 2타.")]
    [Min(1)]
    [SerializeField] private int comboCount = 2;

    [Tooltip("이전 공격 후 이 시간(초) 안에 다시 공격하지 않으면 콤보가 0타로 리셋됨.")]
    [SerializeField] private float comboResetTime = 1f;
    private float lastAttackTime = -999f;

    [SerializeField]
    private LayerMask enemyLayer;

    public Transform AttackBoxPos;
    public Vector2 boxSize;

    [Header("Charge UI")]
    [SerializeField] private Image chargeFill;
    [SerializeField] private GameObject chargeCanvas;
    private bool fullCharged = false;

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        sr = GetComponentInChildren<SpriteRenderer>();
        outline = GetComponent<PlayerOutline>();
        status = GetComponent<PlayerStatus>();
    }

    void Update()
    {
        
       
        NormalAttack(); // Z입력
        UpdateChargeUI();
        CheckAttackFailSafe();

        // ===== 처형 =====
        if (Input.GetKeyDown(KeyCode.V))
        {
            TryHarvest();
        }
    }

    void LateUpdate()
    {
        if (sr.flipX)
        {
            AttackBoxPos.localPosition = new Vector3(
                -Mathf.Abs(AttackBoxPos.localPosition.x),
                AttackBoxPos.localPosition.y,
                0
            );
        }
        else
        {
            AttackBoxPos.localPosition = new Vector3(
                Mathf.Abs(AttackBoxPos.localPosition.x),
                AttackBoxPos.localPosition.y,
                0
            );
        }
    }

    private void TryHarvest()
    {
        Enemy enemy = FindClosestHarvestEnemy();

        if (enemy == null)
        {
            Debug.Log("[Harvest] 처형 가능한 몹이 범위 안에 없음");
            return;
        }

        if (enemy.BackPosition == null)
        {
            Debug.LogWarning($"[Harvest] '{enemy.name}'에 BackPosition이 세팅 안 됨");
            return;
        }

        // 공격/차징 중이면 확실히 정리 (Attack Blend가 EndAttack 이벤트 없이 인터럽트되는 것 방지)
        CancelAttack();
        if (outline != null) outline.EndAttackOutline();

        HarvestManager.Instance.ExecuteHarvest(enemy, transform, anim, sr);
    }

    private Enemy FindClosestHarvestEnemy()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            harvestRange,
            enemyLayer);

        Enemy closestEnemy = null;
        float closestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null) continue;
            if (enemy.isDead) continue;         // 이미 처형/사망 진행 중이면 후보에서 제외
            if (!enemy.CanHarvest) continue;    // 체력 30% 초과면 처형 불가

            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy;
            }
        }

        return closestEnemy;
    }

    public void NormalAttack()
    {
        if (HasChargingAttackSkill)
            ChargingAttack(); // 차징 공격 해금됨
        else
            SimpleAttack();   // 차징 미해금: 일반 공격만
    }

    // 차징 공격 (해금 시): Z 홀드로 차징, 뗄 때 공격
    private void ChargingAttack()
    {
        // 공격 중 입력은 예약으로 돌린다 (차징 해금 상태에서도 콤보가 이어지도록)
        if (isAttacking && Input.GetKeyDown(KeyCode.Z))
            bufferedAttackTime = Time.time;

        // 차징 시작
        if (!isAttacking && Input.GetKeyDown(KeyCode.Z))
        {
            isCharging = true;
            chargeTime = 0f;

            fullCharged = false;
        }

        // 차징 중
        if (isCharging)
        {
            chargeTime += Time.deltaTime;

            chargeTime = Mathf.Clamp(
                chargeTime,
                0f,
                maxChargeTime
            );

            // 풀차징 도달
            if (!fullCharged && chargeTime >= chargedThreshold)
            {
                fullCharged = true;

                StartCoroutine(FullChargeFlash(0.25f));
            }
        }

        // 버튼 뗐을 때 공격
        if (isCharging && Input.GetKeyUp(KeyCode.Z))
        {
            isCharging = false;
            isAttacking = true; //어택 시작시 공격 중 상태로 전환 공격 인덱스 버그 방지 위해 공격 시작 시점에 true로 변경

            if (fullCharged)
            {
                outline.StartAttackOutline();
                Debug.Log("풀차징 공격!");
            }


            DoComboAttack();
        }
    }

    // 콤보 진행 + 리셋 타이머. 이전 공격 후 comboResetTime을 넘기면 0타부터 다시 시작.
    private void DoComboAttack()
    {
        if (Time.time - lastAttackTime > comboResetTime)
            attackNum = 0;

        // 공격 애니메이션
        Attack(attackNum);

        lastAttackTime = Time.time;
        attackNum = (attackNum + 1) % Mathf.Max(1, comboCount);
    }

    // 일반 공격 (차징 미해금): Z 누르면 즉시 공격, 차징 없음
    private void SimpleAttack()
    {
        if (!Input.GetKeyDown(KeyCode.Z))
            return;

        // 공격 중 입력은 버리지 않고 예약해 둔다
        if (isAttacking)
        {
            bufferedAttackTime = Time.time;
            return;
        }

        isCharging = false;
        isAttacking = true;
        chargeTime = 0f;      // 항상 비차징 판정(데미지 1)
        fullCharged = false;

        DoComboAttack();
    }

    public void Attack(int attackNum)
    {
        bool isCharged = chargeTime >= chargedThreshold;

        anim.SetBool("Charged", isCharged);

        // 민첩(dex) 공격속도 배율(AttackSpeedMult)을 애니메이션 속도에 곱함
        float effectiveAttackSpeed = attackSpeed;
        if (status != null)
            effectiveAttackSpeed *= status.Stats.AttackSpeedMult;

        anim.SetFloat("AttackSpeed", effectiveAttackSpeed);
        anim.SetFloat("Blend", attackNum);
        anim.SetTrigger("Attack");

        // 애니메이션이 빨라지면 이벤트가 씹힐 확률도 올라가므로 마감도 같이 당긴다
        attackFailSafeDeadline = Time.time + attackFailSafeTime / Mathf.Max(0.01f, effectiveAttackSpeed);
    }

    // 공격 판정 (애니메이션 이벤트)
    public void AttackHit()
    {
        bool isCharged = chargeTime >= chargedThreshold;

        // 스탯 기반 데미지 계산 (치명타는 한 번의 스윙당 1회 판정)
        int damage;
        bool isCritical;
        CalculateAttackDamage(isCharged, out damage, out isCritical);

        // 공격 판정
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            AttackBoxPos.position,
            boxSize,
            0,
            enemyLayer
        );

        Debug.Log("맞은 개수 : " + hits.Length);

        foreach (Collider2D hit in hits)
        {
            Debug.Log("충돌한 오브젝트 : " + hit.name);

            Enemy enemy = hit.GetComponentInParent<Enemy>();

            if (enemy != null)
            {
                Debug.Log("Enemy 찾음");

                enemy.TakeDamage(damage, isCritical);
            }
        }
    }

    // 스탯 기반 공격 데미지 계산.
    // CharacterStats.RollPhysicalDamage(민댐~맥댐 + 밸런스 + 치명타) 사용. 차징이면 배율.
    private void CalculateAttackDamage(bool isCharged, out int damage, out bool isCritical)
    {
        CharacterStats stats = status != null ? status.Stats : null;

        if (stats == null)
        {
            // 안전장치: 스탯 없으면 최소 데미지
            isCritical = false;
            damage = 1;
            return;
        }

        // 민댐~맥댐 사이에서 밸런스로 굴리고, 치명타까지 내부에서 판정
        int rolled = stats.RollPhysicalDamage(out isCritical);

        if (isCharged)
            rolled = Mathf.RoundToInt(rolled * chargedDamageMultiplier); // 차징 배율

        damage = Mathf.Max(1, rolled);
    }

    //public void StartAttack()
    //{
    //    isAttacking = true;
    //}

    // 애니메이션 이벤트가 오지 않아도 공격이 반드시 끝나게 한다.
    // 이벤트가 조금 늦게 끝나는 것보다, 영영 안 끝나 조작이 잠기는 쪽이 훨씬 나쁘다.
    private void CheckAttackFailSafe()
    {
        if (!isAttacking || attackFailSafeDeadline <= 0f) return;
        if (Time.time < attackFailSafeDeadline) return;

        Debug.LogWarning("[PlayerCombat] EndAttack 이벤트가 오지 않아 공격을 강제 종료합니다. " +
                         "공격 클립의 EndAttack 이벤트 위치를 확인하세요.", this);
        EndAttack();
    }

    public void EndAttack()
    {
        isAttacking = false;
        attackFailSafeDeadline = -1f;

        if (outline != null) outline.EndAttackOutline();

        ConsumeBufferedAttack();
    }

    // 공격이 끝나는 순간, 직전에 눌러 둔 입력이 아직 살아 있으면 다음 타로 이어 준다.
    // 애니메이션의 EndAttack 이벤트가 클립 끝보다 앞에 있으므로, 남은 회복 동작을
    // 다음 타가 끊고 들어가면서 콤보가 매끄럽게 연결된다.
    private void ConsumeBufferedAttack()
    {
        if (Time.time - bufferedAttackTime > attackBufferWindow) return;
        bufferedAttackTime = -999f;

        chargeTime = 0f;
        fullCharged = false;

        // 차징이 해금돼 있고 키를 계속 누르고 있으면 다음 타는 차징으로 시작한다
        if (HasChargingAttackSkill && Input.GetKey(KeyCode.Z))
        {
            isCharging = true;
            return;
        }

        isAttacking = true;
        DoComboAttack();
    }

    private void UpdateChargeUI()
    {
        // 차징 중에만 UI 표시
        chargeCanvas.SetActive(isCharging);

        if (isCharging)
        {
            chargeFill.fillAmount = chargeTime / maxChargeTime;
        }
    }
    private IEnumerator FullChargeFlash(float duration)
    {
        outline.StartAttackOutline();

        yield return new WaitForSeconds(duration);

        outline.EndAttackOutline();

        if (!isAttacking) { outline.EndAttackOutline(); }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(AttackBoxPos.position, boxSize);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, 2f);
    }
    public void CancelAttack()
    {
        isAttacking = false;
        isCharging = false;
        attackNum = 0;
        attackFailSafeDeadline = -1f;
        bufferedAttackTime = -999f;   // 피격·처형으로 끊긴 공격은 이어지면 안 된다
    }
}