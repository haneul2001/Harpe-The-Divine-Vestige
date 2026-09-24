using System.Collections;
using UnityEngine;

// 회전 베기. 히트박스를 보스 둘레로 돌린다.
//
// "붙어서 계속 때리기"를 처벌하는 패턴이라, 정답은 한 박자 떨어지는 것이다.
// 도는 동안 판정을 주기적으로 다시 열어 한 바퀴에 한 번씩 맞게 한다.
public class BossPatternSpin : BossPattern
{
    [Header("회전")]
    [Tooltip("몇 바퀴 돌지")]
    [Min(0.5f)] [SerializeField] private float turns = 2f;

    [Tooltip("한 바퀴에 걸리는 시간(초). 짧을수록 피하기 어렵다")]
    [Min(0.2f)] [SerializeField] private float secondsPerTurn = 0.55f;

    [Tooltip("판정을 다시 여는 간격(초). 이 값이 작으면 한 바퀴에 여러 번 맞는다")]
    [Min(0.1f)] [SerializeField] private float rearmInterval = 0.45f;

    [Header("연출")]
    [SerializeField] private float shake = 0.2f;
    [Tooltip("도는 동안 남길 이펙트 id. 비우면 안 띄운다")]
    [SerializeField] private string vfxId = "SlashHit";
    [Min(0.05f)] [SerializeField] private float vfxInterval = 0.18f;

    public override DangerZone ShowDanger(BossEnemy boss, Vector2 dirToPlayer, float duration)
    {
        // 둘레 전체가 위험하다 — 상자가 아니라 원으로 보여 준다
        return DangerZone.Circle(boss.transform.position, DangerRadius(boss), duration, boss.transform);
    }

    private float DangerRadius(BossEnemy boss)
    {
        GameObject hb = boss.HitBoxObject;
        if (hb == null) return 2f;

        var box = hb.GetComponent<BoxCollider2D>();
        float reach = Vector2.Distance(hb.transform.position, boss.transform.position);
        float half = box != null ? box.size.x * Mathf.Abs(hb.transform.lossyScale.x) * 0.5f : 0.5f;
        return reach + half;
    }

    public override IEnumerator Run(BossEnemy boss, Vector2 dirToPlayer)
    {
        GameObject hb = boss.HitBoxObject;
        if (hb == null) yield break;

        Transform t = hb.transform;
        Vector3 localPos = t.localPosition;
        Quaternion localRot = t.localRotation;

        float total = turns * 360f;
        float speed = 360f / Mathf.Max(0.05f, secondsPerTurn);
        float turned = 0f;
        float rearmAt = 0f;
        float vfxAt = 0f;

        if (shake > 0f) CameraShake.Shake(shake);

        while (turned < total && !boss.isDead)
        {
            float step = speed * Time.deltaTime;
            turned += step;

            // 보스를 중심으로 돌린다. 히트박스는 자식이라 회전만으로 궤도를 그린다
            t.RotateAround(boss.transform.position, Vector3.forward, step);

            rearmAt -= Time.deltaTime;
            if (rearmAt <= 0f)
            {
                boss.RearmHitBox();
                rearmAt = rearmInterval;
            }

            vfxAt -= Time.deltaTime;
            if (vfxAt <= 0f && !string.IsNullOrEmpty(vfxId))
            {
                PixelVfx.Play(vfxId, t.position);
                vfxAt = vfxInterval;
            }

            if (boss.rb != null) boss.rb.velocity = Vector2.zero;
            yield return null;
        }

        // 돌려 놓지 않으면 다음 공격이 엉뚱한 쪽을 친다
        t.localPosition = localPos;
        t.localRotation = localRot;
    }
}
