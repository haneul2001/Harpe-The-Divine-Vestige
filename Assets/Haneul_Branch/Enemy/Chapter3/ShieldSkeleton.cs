using System.Collections;
using UnityEngine;

// 3층 검방패 해골. 칼질은 녹슨 검 해골과 같고, 앞에서 들어오는 공격은 방패로 막는다.
//
// 막는 순간 방패에서 불똥(block FX)이 튀고 플레이어는 튕겨 나가 잠깐 굳는다 — 그 틈에 곧바로 반격이 온다.
// 등 뒤를 치거나, 한 번 막은 뒤 방패가 내려간 동안(blockCooldown)을 노리면 들어간다.
// 휘두르는 도중에는 막지 못한다 — 공격을 받아쳐 반격하는 것도 답이다.
public class ShieldSkeleton : AttackEnemyBase
{
    [Header("공격")]
    [Tooltip("공격 히트박스가 켜져 있는 시간(초)")]
    [SerializeField] private float attackActiveDuration = 0.3f;

    [Header("방패 막기")]
    [Tooltip("한 번 막고 나서 다시 막을 수 있기까지(초). 이 틈이 공략 구간이다")]
    [SerializeField] private float blockCooldown = 2.2f;
    [Tooltip("막은 직후 이 시간 동안 들어오는 타격도 전부 막는다 — 한 번 휘두름에 여러 번 닿는 공격이 뚫고 들어가지 않게")]
    [SerializeField] private float blockWindow = 0.3f;
    [Tooltip("막혔을 때 플레이어가 굳는 시간(초)")]
    [SerializeField] private float playerStagger = 0.55f;
    [Tooltip("막고 나서 반격까지(초)")]
    [SerializeField] private float counterDelay = 0.15f;
    [Tooltip("방패 불똥 자리 (발밑 기준, 몸 배율 1일 때). x는 바라보는 쪽")]
    [SerializeField] private Vector2 fxOffset = new Vector2(0.32f, 0.36f);
    [SerializeField] private string blockFxId = "SkeletonBlockFx";
    [SerializeField] private string blockState = "Block";

    private float nextBlock;
    private float blockUntil;

    protected override IEnumerator AttackActivePhase(Vector2 dir)
    {
        rb.velocity = Vector2.zero;
        float timer = attackActiveDuration;
        while (timer > 0f && !isDead)
        {
            timer -= Time.deltaTime;
            rb.velocity = Vector2.zero;
            yield return null;
        }
    }

    protected override bool RejectsDamage(bool fromParry)
    {
        if (fromParry) return false;                    // 받아치기 반격은 방패로 못 막는다
        if (Time.time < blockUntil) return true;        // 방금 막은 그 휘두름
        if (isDead || isAttacking || Time.time < nextBlock) return false;

        Transform p = PlayerTransform();
        if (p == null) return false;
        float dx = p.position.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.05f && (dx > 0f) != IsFacingRight) return false;   // 등 뒤는 못 막는다

        Block(p);
        return true;
    }

    private void Block(Transform p)
    {
        nextBlock = Time.time + blockCooldown;
        blockUntil = Time.time + blockWindow;

        if (animator != null)
        {
            animator.ResetTrigger("hit");
            animator.ResetTrigger("attack");
            animator.Play(blockState, 0, 0f);
        }

        float s = Mathf.Abs(transform.lossyScale.x);
        Vector3 fx = transform.position + new Vector3((IsFacingRight ? 1f : -1f) * fxOffset.x, fxOffset.y, 0f) * s;
        PixelVfx.Play(blockFxId, fx);
        CameraShake.Shake(0.08f);

        // 방패에 튕겨 나가 잠깐 굳는다
        var move = p.GetComponent<PlayerMove>();
        if (move != null)
        {
            move.KnockBack(transform.position);
            move.LockControl(playerStagger);
        }

        // 곧바로 반격 — 다음 공격 대기를 거의 다 채워 둔다
        lastAttackTime = Time.time - attackIdleTime + counterDelay;
    }

    private Transform PlayerTransform()
    {
        if (player != null) return player;
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        return go != null ? go.transform : null;
    }
}
