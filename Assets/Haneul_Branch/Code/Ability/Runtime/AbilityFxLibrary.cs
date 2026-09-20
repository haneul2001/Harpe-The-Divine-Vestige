using UnityEngine;

// 특성 효과용 한 장짜리 그림 모음 (Resources/VFX/AbilityFxLibrary). 쓰는 쪽은 SpriteFx를 부른다
[CreateAssetMenu(fileName = "AbilityFxLibrary", menuName = "Harpe/Ability/Fx Library")]
public class AbilityFxLibrary : ScriptableObject
{
    [Tooltip("FxSprite 순서대로: Wave, Shard, SoulBolt, Phantom, Mark, EchoZone, Ring, SkullBurst, SoulBurst")]
    public Sprite[] sprites = new Sprite[9];

    private static AbilityFxLibrary cached;

    public static Sprite Get(FxSprite kind)
    {
        if (cached == null) cached = Resources.Load<AbilityFxLibrary>("VFX/AbilityFxLibrary");
        if (cached == null || cached.sprites == null) return null;
        int i = (int)kind;
        return i < cached.sprites.Length ? cached.sprites[i] : null;
    }
}

