using UnityEngine;

[RequireComponent(typeof(Player))]
public class SkillInputController : MonoBehaviour
{
    [System.Serializable]
    public class SkillSlot
    {
        public KeyCode key = KeyCode.Q;
        public PlayerSkill skill;
        [HideInInspector] public float lastUsedTime = -999f;
    }

    [SerializeField] private SkillSlot[] slots;

    private Player player;
    private SkillContext ctx;

    void Awake()
    {
        player = GetComponent<Player>();
    }

    void Start()
    {
        ctx = new SkillContext(player);
    }

    void Update()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (s.skill == null) continue;
            if (!Input.GetKeyDown(s.key)) continue;
            if (Time.time - s.lastUsedTime < s.skill.cooldown) continue;
            if (!s.skill.CanActivate(ctx)) continue;

            s.skill.Activate(ctx);
            s.lastUsedTime = Time.time;
        }
    }

    public bool TryUseSkill(int index)
    {
        if (slots == null || index < 0 || index >= slots.Length) return false;
        var s = slots[index];
        if (s.skill == null) return false;
        if (Time.time - s.lastUsedTime < s.skill.cooldown) return false;
        if (!s.skill.CanActivate(ctx)) return false;

        s.skill.Activate(ctx);
        s.lastUsedTime = Time.time;
        return true;
    }
}
