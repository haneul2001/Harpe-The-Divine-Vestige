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

    private PlayerMove move;

    void Awake()
    {
        player = GetComponent<Player>();
        move = GetComponent<PlayerMove>();
    }

    void Start()
    {
        ctx = new SkillContext(player);
    }

    void Update()
    {
        if (slots == null) return;
        if (move != null && move.IsControlLocked) return;   // 패링 직후 등 조작 불가 구간

        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (s.skill == null) continue;
            if (!Input.GetKeyDown(s.key)) continue;
            if (Time.time - s.lastUsedTime < Cooldown(s.skill)) continue;
            if (!s.skill.CanActivate(ctx)) continue;

            s.skill.Activate(ctx);
            s.lastUsedTime = Time.time;

            // 스킬을 실제로 쓴 순간에만 은신을 끊는다.
            // 쿨타임/자원 부족으로 불발된 키 입력까지 풀어 버리면 억울하다.
            PlayerStealth.BreakStealth();
        }
    }

    // 세트 시너지(철벽 등)가 줄인 최종 쿨타임
    private static float Cooldown(PlayerSkill skill)
    {
        return Mathf.Max(0f, skill.cooldown - AbilityHooks.SkillCooldownReduction(skill));
    }

    // 스킬 칸 UI가 "다음에 쓸 수 있을 때까지"를 그릴 수 있게 남은 쿨타임을 내준다.
    // 세트 시너지로 줄어든 값을 그대로 쓰므로, 철벽을 먹으면 칸도 같이 빨리 찬다.
    //
    // 타입으로 찾는 이유는 UI가 에셋(Parry.asset)을 직접 들고 있지 않기 때문이다.
    // 키도 같이 내줘서 키캡 글자까지 여기 설정을 따라가게 한다.
    public bool TryGetCooldownOf<T>(out float remain, out float total, out KeyCode key) where T : PlayerSkill
    {
        remain = 0f; total = 0f; key = KeyCode.None;
        if (slots == null) return false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || !(slots[i].skill is T)) continue;

            total = Cooldown(slots[i].skill);
            remain = Mathf.Max(0f, slots[i].lastUsedTime + total - Time.time);
            key = slots[i].key;
            return true;
        }
        return false;
    }

    public bool TryUseSkill(int index)
    {
        if (slots == null || index < 0 || index >= slots.Length) return false;
        var s = slots[index];
        if (s.skill == null) return false;
        if (Time.time - s.lastUsedTime < Cooldown(s.skill)) return false;
        if (!s.skill.CanActivate(ctx)) return false;

        s.skill.Activate(ctx);
        s.lastUsedTime = Time.time;
        PlayerStealth.BreakStealth();
        return true;
    }
}
