using System.Collections;
using UnityEngine;

public class ZombieEnemy : Enemy
{
    [Header("좀비 공격 설정")]
    [SerializeField] private GameObject attackHitBox;
    [SerializeField] private GameObject attackRangeBox;

    [Header("돌진 설정")]
    [SerializeField] private float dashSpeed = 5f;
    [SerializeField] private float dashDuration = 0.3f;

    private EnemyHitBox hitBox;

    protected override void Start()
    {
        base.Start();

        // 히트박스 컴포넌트 가져오기
        hitBox = attackHitBox.GetComponent<EnemyHitBox>();

        // owner 연결
        hitBox.Initialize(this);

        // 처음엔 비활성화
        attackHitBox.SetActive(false);
        attackRangeBox.SetActive(false);
    }

    // CombatIdle에서 호출
    public void ShowAttackRange(bool show)
    {
        attackRangeBox.SetActive(show);
    }

    public override void Attack()
    {
        // 이미 공격 중이면 종료
        if (isAttacking)
            return;

        isAttacking = true;

        base.Attack();

        StartCoroutine(DashAttackCoroutine());
    }

    private IEnumerator DashAttackCoroutine()
    {
        // 이동 정지
        StopMove();

        // 플레이어 바라보기
        FaceToPlayer();

        // 공격 시작 방향 저장
        Vector3 dashDirection = transform.right;

        // 공격 시작 시 범위 끄기
        attackRangeBox.SetActive(false);

        // 공격 애니메이션
        animator.SetTrigger("attack");

        // 히트 초기화
        hitBox.ResetHit();

        // 실제 공격 시작
        attackHitBox.SetActive(true);

        float timer = dashDuration;

        while (timer > 0f && !isDead)
        {
            timer -= Time.deltaTime;

            transform.position +=
                dashDirection * dashSpeed * Time.deltaTime;

            yield return null;
        }

        // 공격 종료
        attackHitBox.SetActive(false);

        isAttacking = false;
    }

    private void OnDisable()
    {
        attackHitBox.SetActive(false);
        attackRangeBox.SetActive(false);
    }
}