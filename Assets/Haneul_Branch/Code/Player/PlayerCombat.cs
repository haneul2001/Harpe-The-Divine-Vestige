using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private float harvestRange = 5f;

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

    [Header("콤보")]
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
        attackNum = (attackNum + 1) % 3;
    }

    // 일반 공격 (차징 미해금): Z 누르면 즉시 공격, 차징 없음
    private void SimpleAttack()
    {
        if (isAttacking || !Input.GetKeyDown(KeyCode.Z))
            return;

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

    public void EndAttack()
    {
        isAttacking = false;

        outline.EndAttackOutline();
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
    }
}