using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    private SpriteRenderer sr;
    private Animator anim;

    public bool isAttacking = false;
    public bool isCharging = false;

    private float chargeTime;
    [SerializeField] private float maxChargeTime = 1.5f;
    [SerializeField] private float chargedThreshold = 1.4f;//풀차징 기준

    public int attackNum = 0;

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
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (!isAttacking)
        {
            sr.color = new Color(1f, 1f, 1f);
        }
       
        NormalAttack(); // Z입력
        UpdateChargeUI();

        // ===== 처형 =====
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

    //public void TryExecuteEnemy()
    //{
    //    Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 7f);

    //    SpriteRenderer playerSR = GetComponent<SpriteRenderer>();
    //    Vector2 playerForward = playerSR.flipX ? Vector2.left : Vector2.right;

    //    foreach (var hit in hits)
    //    {
    //        Enemy enemy = hit.GetComponentInParent<Enemy>();
    //        if (enemy == null) continue;
    //        if (!enemy.CanExecute()) continue;

    //        Vector2 dirToEnemy = (enemy.transform.position - transform.position).normalized;
    //        float dot = Vector2.Dot(playerForward, dirToEnemy);

    //        if (dot > 0.5f)
    //        {
    //            Debug.Log("정면 처형 성공");
    //            enemy.TryExecution(transform);
    //            break;
    //        }
    //    }
    //}

    public void NormalAttack()
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
            Debug.Log(chargeTime);
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

                StartCoroutine(FullChargeFlash(0.1f));
            }
        }

        // 버튼 뗐을 때 공격
        if (isCharging && Input.GetKeyUp(KeyCode.Z))
        {
            isCharging = false;
            isAttacking = true; //어택 시작시 공격 중 상태로 전환 공격 인덱스 버그 방지 위해 공격 시작 시점에 true로 변경

            if (fullCharged)
            {
                sr.color = new Color(1f, 0f, 0f);
            }
            

            // 공격 애니메이션
            Attack(attackNum);

            attackNum = (attackNum + 1) % 3;
        }
    }

    public void Attack(int attackNum)
    {
        bool isCharged = chargeTime >= chargedThreshold;

        anim.SetBool("Charged", isCharged);

        anim.SetFloat("Blend", attackNum);
        anim.SetTrigger("Attack");
    }

    // 공격 판정 (애니메이션 이벤트)
    public void AttackHit()
    {
        int damage = chargeTime >= chargedThreshold ? 3 : 1;

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

                enemy.TakeDamage(damage);
            }
        }
    }

    //public void StartAttack()
    //{
    //    isAttacking = true;
    //}

    public void EndAttack()
    {
        isAttacking = false;
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
        sr.color = new Color(1f, 0f, 0f);

        yield return new WaitForSeconds(duration);

        sr.color = Color.white;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(AttackBoxPos.position, boxSize);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, 2f);
    }

    public void StartParry()
    {
        Debug.Log("패링 시작");
    }
}