using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Parry", menuName = "PlayerSkill/Parry")]
public class Parry : PlayerSkill
{
    [Header("Parry 설정")]
    [SerializeField] private float duration = 0.5f;

    public override void Activate(SkillContext ctx)
    {
        ctx.CoroutineRunner.StartCoroutine(ParryRoutine(ctx));
    }

    private IEnumerator ParryRoutine(SkillContext ctx)
    {
        Debug.Log($"[{skillName}] 패링 시작");
        // TODO: 무적/반사 판정. PlayerCombat 등에 IsParrying 플래그 노출 후 여기서 제어.
        yield return new WaitForSeconds(duration);
        Debug.Log($"[{skillName}] 패링 종료");
    }
}
