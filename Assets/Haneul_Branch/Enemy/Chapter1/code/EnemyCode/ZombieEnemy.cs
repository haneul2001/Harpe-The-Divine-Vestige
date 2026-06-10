using System.Collections;
using UnityEngine;

public class ZombieEnemy : Enemy
{
    [Header("좀비 공격 설정")]
    [SerializeField] private GameObject attackHitBox;
    [SerializeField] private GameObject attackRangeBox;
    [SerializeField] private GameObject attackPivot;
    private AttackRangeSet attackRangeSet;
    private EnemyHitBox hitBox;
    [Header("돌진 설정")]
    [SerializeField] private float dashSpeed = 10f;
    [SerializeField] private float dashDuration = 0.3f;

    protected override void Start()
    {
        base.Start();

          attackRangeSet =
        attackPivot.GetComponent<AttackRangeSet>();
        // 히트박스 컴포넌트 가져오기
        hitBox = attackHitBox.GetComponent<EnemyHitBox>();

        // owner 연결
        hitBox.Initialize(this);

        // 처음엔 비활성화
        attackHitBox.SetActive(false);
        attackRangeBox.SetActive(false);
    }

    // CombatIdle에서 호출
    public override void ShowAttackRange(bool show)
    {
        attackRangeBox.SetActive(show);
    }

    public override void Attack()
    {
        // 이미 공격 중이면 종료
        if (isAttacking)
            return;

        isAttacking = true;


        StartCoroutine(DashAttackCoroutine());
    }

private IEnumerator DashAttackCoroutine()
{
    StopMove();

   FaceToPlayer();

  Vector2 dashDirection =
    (player.position - transform.position).normalized;

    attackRangeSet.SetDirection(dashDirection);

    attackRangeBox.SetActive(true);
    // 공격 예고
    attackRangeBox.SetActive(true);

    yield return new WaitForSeconds(attackWarningDuration);

    attackRangeBox.SetActive(false);

    animator.SetTrigger("attack");

    hitBox.ResetHit();

    attackHitBox.SetActive(true);

    float timer = dashDuration;

    while (timer > 0f && !isDead)
    {
        timer -= Time.deltaTime;

        rb.velocity = dashDirection*dashSpeed;

        yield return null;
    }
    rb.velocity = Vector2.zero;
    
    attackHitBox.SetActive(false);
    yield return new WaitForSeconds(1f);
    // 여기서 쿨타임 시작
    lastAttackTime = Time.time;

    isAttacking = false;
}

    private void OnDisable()
    {
        attackHitBox.SetActive(false);
        attackRangeBox.SetActive(false);
    }
}