using System.Collections.Generic;
using UnityEngine;

// [대시] 유령의 발걸음 — 대시 직후 잠깐 은신
[CreateAssetMenu(fileName = "Effect_GhostStep", menuName = "Harpe/Ability/Effect/유령의 발걸음")]
public class GhostStepEffect : AbilityEffect
{
    [SerializeField] private float duration = 1.5f;

    public override void OnDashEnd(AbilityContext ctx, Vector2 start, Vector2 end)
    {
        if (PlayerStealth.Instance != null) PlayerStealth.Instance.CloakFor(duration);
    }
}
