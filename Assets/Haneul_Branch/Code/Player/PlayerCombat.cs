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

    [Header("입력")]
    [Tooltip("공격 버튼. 기본은 마우스 왼클릭(Mouse0).\n"
           + "차징이 해금돼 있으면 이 버튼을 누른 채로 모으고, 뗄 때 나간다")]
    [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;

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
    [Tooltip("체크 시 공격 버튼 홀드로 차징 공격 가능. 해제 시 차징 없이 일반 공격만 나감.")]
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

    [Header("공격 전진")]
    [Tooltip("공격할 때마다 공격 방향으로 밀려 나가는 거리(월드 단위). 0이면 제자리")]
    [SerializeField] private float lungeDistance = 0.35f;
    [Tooltip("전진에 걸리는 시간(초). 짧을수록 툭 튀어 나간다")]
    [SerializeField] private float lungeDuration = 0.08f;

    [Header("콤보")]
    [Tooltip("콤보 타수. 애니메이터의 Attack Blend 트리에 등록된 모션 수와 반드시 같아야 한다. "
           + "Reaper는 Slash + Attack2(Double Slash 앞 절반) + Attack3(뒤 절반)로 3타.")]
    [Min(1)]
    [SerializeField] private int comboCount = 3;

    [Tooltip("타수별 베기 이펙트 방향. 체크 = 반대(올려베기). [0]=1타 [1]=2타 [2]=3타")]
    [SerializeField] private bool[] reverseSlashByHit = { false, true, false };

    [Tooltip("막타(마지막 타)가 끝난 뒤 다음 1타가 나가기까지의 텀(초). 그 사이 누른 입력은 텀이 끝나면 나간다")]
    [SerializeField] private float finisherRecovery = 0.35f;

    [Tooltip("공격 시작 후 이 시간(초) 안에 오는 판정 이벤트는 이전 타의 잔재로 보고 무시한다")]
    [SerializeField] private float minHitDelay = 0.1f;

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

    // 8방향 공격
    private PlayerAim aim;
    private PlayerSlashVfx slashVfx;

    [Tooltip("치명타일 때 베기 이펙트 재질 (M_Slash_Red). 치명타가 아니면 원래 흰색")]
    [SerializeField] private Material critSlashMaterial;

    // 공격 버튼을 누른 순간(Attack) 굴린 이번 타의 치명타 여부. 판정(AttackHit)과 베기 이펙트 색이 이 값을 같이 쓴다
    private bool pendingCrit;
    private PlayerHitSparkVfx hitSparkVfx;
    private PlayerMove move;
    private float attackReach;     // 몸 중심에서 판정 박스 중심까지 (처음 배치된 AttackBox의 x)
    private float attackPivotY;    // 몸 중심 높이 (처음 배치된 AttackBox의 y)
    private int currentHit;        // 지금 휘두르는 타수 (0 = 1타)
    private bool hitConsumed;      // 이번 타의 판정이 이미 나갔는지
    private float attackStartTime;
    private float nextAttackTime = -1f;   // 막타 뒤 텀 — 이 시각 전에는 다음 공격이 안 나간다

    // 판정 박스 회전 각도(도). 박스의 가로(boxSize.x)가 공격 방향을 따라 눕는다.
    public float AttackAngle => aim != null ? aim.Angle : (sr != null && sr.flipX ? 180f : 0f);

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        sr = GetComponentInChildren<SpriteRenderer>();
        outline = GetComponent<PlayerOutline>();
        status = GetComponent<PlayerStatus>();
        aim = GetComponent<PlayerAim>();
        slashVfx = GetComponent<PlayerSlashVfx>();
        hitSparkVfx = GetComponent<PlayerHitSparkVfx>();
        move = GetComponent<PlayerMove>();

        if (AttackBoxPos != null)
        {
            attackReach = Mathf.Abs(AttackBoxPos.localPosition.x);
            attackPivotY = AttackBoxPos.localPosition.y;
        }
    }

    void Update()
    {
        // 화면이 멈춰 있으면 입력을 받지 않는다.
        //
        // 공격이 마우스 왼클릭이 된 뒤로는 이게 필요하다 — 특성 카드·상점처럼 timeScale을 0으로
        // 두고 뜨는 창은 클릭으로 조작하므로, 안 막으면 카드를 고를 때마다 칼도 같이 휘두른다.
        if (Time.timeScale <= 0f)
        {
            // 멈춘 사이에 버튼을 떼면 GetKeyUp을 놓쳐 영영 차징 중으로 남는다. 모으던 건 접는다
            if (isCharging)
            {
                isCharging = false;
                chargeTime = 0f;
                fullCharged = false;
            }

            UpdateChargeUI();
            return;
        }

        // 패링 직후 같은 조작 불가 구간, 처형·대시 공격 같은 연출 중에는 공격·처형 입력을 받지 않는다
        if (move != null && (move.IsControlLocked || move.isExecuting))
        {
            UpdateChargeUI();
            CheckAttackFailSafe();
            return;
        }

        NormalAttack(); // 공격 버튼 입력
        UpdateChargeUI();
        CheckAttackFailSafe();

        // 막타 뒤 텀이 막 끝났고, 텀 끝나기 직전에 눌러 둔 입력이 있으면 1타로 이어 준다
        if (!isAttacking && !isCharging && nextAttackTime > 0f && Time.time >= nextAttackTime)
        {
            nextAttackTime = -1f;
            ConsumeBufferedAttack();
        }

        // ===== 처형 =====
        if (Input.GetKeyDown(HarvestKey))
        {
            TryHarvest();
        }
    }

    // 판정 박스를 공격 방향(8방향)으로 옮긴다. 몸 중심(attackPivotY 높이)을 축으로 돈다.
    void LateUpdate()
    {
        float rad = AttackAngle * Mathf.Deg2Rad;
        AttackBoxPos.localPosition = new Vector3(
            Mathf.Cos(rad) * attackReach,
            attackPivotY + Mathf.Sin(rad) * attackReach,
            0f);
    }

    public const KeyCode HarvestKey = KeyCode.V;

    // 공격 버튼(좌클릭)이 UI에 칸으로 올라가므로, 다음 평타가 나갈 수 있을 때까지를 내준다.
    //
    // 평타에는 따로 쿨타임이 없다. 실제로 "지금 못 친다"가 되는 구간은 막타 뒤 텀뿐이라
    // 그걸 쿨타임으로 삼는다. 휘두르는 도중은 세지 않는다 — 그 사이 누른 입력은 예약돼
    // 이어서 나가므로, 칸을 가려 두면 못 치는 것처럼 보여 거짓말이 된다.
    public float AttackCooldown { get { return Mathf.Max(0f, finisherRecovery); } }

    public float AttackCooldownRemaining
    {
        get { return nextAttackTime > 0f ? Mathf.Max(0f, nextAttackTime - Time.time) : 0f; }
    }

    public KeyCode AttackKey { get { return attackKey; } }

    // 지금 처형 키를 누르면 처형이 나가는가 (상태 UI의 처형 칸을 빛내는 데 쓴다)
    public bool CanHarvestNow
    {
        get
        {
            if (HarvestManager.Instance == null || HarvestManager.Instance.IsHarvesting) return false;
            if (move != null && (move.isExecuting || move.inputLocked)) return false;
            Enemy enemy = FindClosestHarvestEnemy();
            return enemy != null && enemy.BackPosition != null;   // TryHarvest와 같은 조건
        }
    }

    private void TryHarvest()
    {
        Enemy enemy = FindClosestHarvestEnemy();

        if (enemy == null)
        {
            Debug.Log("[Harvest] 처형 가능한 몹이 범위 안에 없음");
            ToastManager.Show("처형시킬 대상이 없다");   // 같은 문구 연타는 ToastManager가 하나로 합친다
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

    // 차징 공격 (해금 시): 공격 버튼 홀드로 차징, 뗄 때 공격
    private void ChargingAttack()
    {
        // 공격 중 입력은 예약으로 돌린다 (차징 해금 상태에서도 콤보가 이어지도록)
        if (isAttacking && Input.GetKeyDown(attackKey))
            bufferedAttackTime = Time.time;

        // 막타 뒤 텀에는 차징도 시작하지 않는다
        if (!isAttacking && Time.time < nextAttackTime && Input.GetKeyDown(attackKey))
            bufferedAttackTime = Time.time;

        // 차징 시작
        if (!isAttacking && Time.time >= nextAttackTime && Input.GetKeyDown(attackKey))
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
        if (isCharging && Input.GetKeyUp(attackKey))
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

        // 이번 타의 방향을 지금 누르고 있는 방향키로 다시 잡는다 —
        // 콤보 중에는 조준이 잠겨 있어(isAttacking) 이 호출이 없으면 첫 타 방향으로 계속 때린다.
        // 그 뒤 몸을 돌리고 판정 박스도 즉시 옮긴다 (LateUpdate를 기다리면 첫 프레임이 어긋난다)
        if (aim != null)
        {
            aim.RefreshFromInput();
            aim.FaceBody();
        }
        LateUpdate();

        // 공격 방향으로 살짝 전진 — 이동키를 누르고 있을 때만 (제자리 공격은 제자리에서)
        bool holdingMove = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f;
        // 특성(피의 돌진)이 전진 거리를 더해 주면 이동키 없이도 전진한다
        float lungeBonus = AbilityHooks.LungeBonus(true);
        if (move != null && (lungeDistance > 0f && holdingMove || lungeBonus > 0f))
        {
            float rad = AttackAngle * Mathf.Deg2Rad;
            move.Lunge(new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)), (holdingMove ? lungeDistance : 0f) + lungeBonus, lungeDuration);
        }

        // 공격 애니메이션
        Attack(attackNum);

        lastAttackTime = Time.time;
        attackNum = (attackNum + 1) % Mathf.Max(1, comboCount);
    }

    // 일반 공격 (차징 미해금): 공격 버튼 누르면 즉시 공격, 차징 없음
    private void SimpleAttack()
    {
        if (!Input.GetKeyDown(attackKey))
            return;

        // 공격 중이거나 막타 뒤 텀이면 버리지 않고 예약해 둔다
        if (isAttacking || Time.time < nextAttackTime)
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

    // ── 다른 기술(대시 공격 등)이 공격 애니메이션만 빌려 쓸 때 ──
    // 애니메이션의 AttackHit/EndAttack 이벤트가 평타 판정을 내지 않게 막아 둔다.
    private float suppressAnimEventsUntil = -1f;

    public float AttackReachWorld => attackReach * Mathf.Abs(transform.lossyScale.x);
    public float AttackPivotYWorld => attackPivotY * Mathf.Abs(transform.lossyScale.y);

    public void PlayExternalAttackAnimation(int comboIndex, float suppressSeconds)
    {
        CancelAttack();
        float effectiveAttackSpeed = attackSpeed * (status != null ? status.Stats.AttackSpeedMult : 1f);
        anim.SetBool("Charged", false);
        anim.SetFloat("AttackSpeed", effectiveAttackSpeed);
        anim.SetFloat("Blend", comboIndex);
        anim.SetTrigger("Attack");

        hitConsumed = true;   // 이 애니메이션으로는 평타 판정이 안 나간다
        suppressAnimEventsUntil = Time.time + suppressSeconds;
    }

    public void EndExternalAttack()
    {
        suppressAnimEventsUntil = -1f;
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

        // 이번 타의 정보 — 판정·이펙트는 "몇 번째로 불렸나"가 아니라 이 타수로 정한다
        currentHit = attackNum;
        hitConsumed = false;
        pendingCrit = status != null && status.Stats != null && status.Stats.RollCritChance();
        if (AbilityHooks.ForcesCrit(SwingKind(isCharged))) pendingCrit = true;   // 특성: 그림자 칼날 등
        attackStartTime = Time.time;

        // 애니메이션이 빨라지면 이벤트가 씹힐 확률도 올라가므로 마감도 같이 당긴다
        attackFailSafeDeadline = Time.time + attackFailSafeTime / Mathf.Max(0.01f, effectiveAttackSpeed);
    }

    // 공격 판정 (애니메이션 이벤트)
    public void AttackHit()
    {
        // 한 타에 판정은 한 번만.
        // 1·2·3타가 같은 블렌드 트리를 쓰기 때문에, 빠르게 이어 누르면 넘어가는 중인 이전 타가
        // 새 타의 클립으로 바뀐 채 이벤트를 한 번 더 쏜다. 그걸 두 번째 판정으로 세면 데미지가 두 번 들어가고
        // 베기 방향도 한 칸씩 밀린다. 공격 시작 직후에 오는 이벤트도 이전 타의 잔재라 무시한다.
        if (Time.time < suppressAnimEventsUntil) return;
        if (hitConsumed || Time.time - attackStartTime < minHitDelay) return;
        hitConsumed = true;

        bool isCharged = chargeTime >= chargedThreshold;

        // 스탯 기반 데미지 계산 (치명타는 한 번의 스윙당 1회 판정)
        int damage;
        bool isCritical;
        CalculateAttackDamage(isCharged, out damage, out isCritical);

        // 특성 피해 배율 — 한 번 휘두를 때 한 번만 묻는다 ("다음 공격 1회" 효과가 여기서 소모된다)
        DamageKind kind = SwingKind(isCharged);
        damage = Mathf.Max(1, Mathf.RoundToInt(damage * AbilityHooks.DamageMultiplier(kind, null, true)));
        Vector3 swingFrom = transform.position + Vector3.up * attackPivotY * transform.lossyScale.y;
        AbilityHooks.NotifySwing(new SwingInfo { kind = kind, origin = swingFrom, angle = AttackAngle });

        // 공격 판정
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            AttackBoxPos.position,
            boxSize,
            AttackAngle,
            enemyLayer
        );

        // 베기 이펙트 — 타수마다 정해진 방향 (기본: 1타 내려베기, 2타 올려베기, 3타 내려베기)
        if (slashVfx != null)
        {
            bool reverse = reverseSlashByHit != null && reverseSlashByHit.Length > 0
                && reverseSlashByHit[Mathf.Clamp(currentHit, 0, reverseSlashByHit.Length - 1)];
            slashVfx.Play(AttackBoxPos.position, AttackAngle, reverse, isCritical ? critSlashMaterial : null);
        }

        Debug.Log("맞은 개수 : " + hits.Length);

        var struck = new System.Collections.Generic.HashSet<Enemy>();
        foreach (Collider2D hit in hits)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();

            // 콜라이더가 여러 개인 적도 한 번만 맞는다
            if (enemy != null && !enemy.isDead && struck.Add(enemy))
            {
                enemy.TakeDamage(damage, isCritical);

                // 가시가 플레이어 → 몬스터 방향(몬스터 너머)으로 뻗는 피격 이펙트
                if (hitSparkVfx != null)
                    hitSparkVfx.Play(hit.bounds, swingFrom, enemy);

                AbilityHooks.NotifyHit(new HitInfo
                {
                    kind = kind, enemy = enemy, damage = damage, critical = isCritical,
                    point = hit.bounds.center, from = swingFrom,
                });
            }
        }
    }

    // 이번 타가 어떤 공격인가 (특성이 막타·차징에만 붙는 경우를 가른다)
    private DamageKind SwingKind(bool isCharged)
    {
        if (isCharged) return DamageKind.Charged;
        return currentHit >= Mathf.Max(1, comboCount) - 1 ? DamageKind.Finisher : DamageKind.Basic;
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

        // 민댐~맥댐 사이에서 밸런스로 굴린다. 치명타는 공격 버튼을 누를 때(Attack) 이미 정해 뒀다
        isCritical = pendingCrit;
        int rolled = stats.RollPhysicalDamage(isCritical);

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
        if (Time.time < suppressAnimEventsUntil) return;   // 다른 기술이 빌려 쓴 애니메이션의 이벤트

        isAttacking = false;
        attackFailSafeDeadline = -1f;

        if (outline != null) outline.EndAttackOutline();

        // 막타였으면 텀을 둔다. 예약된 입력은 텀이 끝나는 순간 Update에서 이어 준다
        if (currentHit >= Mathf.Max(1, comboCount) - 1 && finisherRecovery > 0f)
        {
            nextAttackTime = Time.time + finisherRecovery;
            return;
        }

        ConsumeBufferedAttack();
    }

    // 공격이 끝나는 순간, 직전에 눌러 둔 입력이 아직 살아 있으면 다음 타로 이어 준다.
    // 애니메이션의 EndAttack 이벤트가 클립 끝보다 앞에 있으므로, 남은 회복 동작을
    // 다음 타가 끊고 들어가면서 콤보가 매끄럽게 연결된다.
    // 예약된 입력이 아직 살아 있는가.
    //
    // "누른 지 attackBufferWindow 이내"만 보면, 한 타가 그 창보다 길 때(1타 0.5초 vs 창 0.25초)
    // 타격 앞부분에 누른 입력이 스윙이 끝나기도 전에 만료돼 그대로 씹힌다. 빠르게 두 번 누르면
    // 두 번째가 안 나가던 원인이 이것이다. 그래서 "이번 스윙 도중에 누른 입력"은 언제 눌렀든 이어 준다.
    private bool HasBufferedAttack()
    {
        if (bufferedAttackTime < 0f) return false;
        if (bufferedAttackTime >= attackStartTime) return true;          // 이번 스윙 중에 누름
        return Time.time - bufferedAttackTime <= attackBufferWindow;      // 스윙 전에 눌렀다면 창 안일 때만
    }

    private void ConsumeBufferedAttack()
    {
        if (!HasBufferedAttack()) return;
        bufferedAttackTime = -999f;

        chargeTime = 0f;
        fullCharged = false;

        // 차징이 해금돼 있고 키를 계속 누르고 있으면 다음 타는 차징으로 시작한다
        if (HasChargingAttackSkill && Input.GetKey(attackKey))
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
        Gizmos.matrix = Matrix4x4.TRS(AttackBoxPos.position, Quaternion.Euler(0f, 0f, AttackAngle), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, boxSize);
        Gizmos.matrix = Matrix4x4.identity;

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