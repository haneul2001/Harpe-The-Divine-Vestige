using UnityEngine;

public abstract class PlayerSkill : ScriptableObject
{
    [Header("공통")]
    public string skillName;
    public Sprite icon;
    [Tooltip("쿨타임(초)")] public float cooldown = 0f;
    [Tooltip("소모 소울")] public int soulCost = 0;

    public virtual bool CanActivate(SkillContext ctx) => true;

    public abstract void Activate(SkillContext ctx);
}
