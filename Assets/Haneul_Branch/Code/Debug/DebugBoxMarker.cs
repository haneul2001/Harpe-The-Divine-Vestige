using UnityEngine;

// 이미 만들어져 있는 디버그 표시(적의 AttackRange·HitBox 등)에 붙이는 표식.
//
// GameObject를 끄지 않고 렌더러만 끈다 —
// AttackEnemyBase가 공격 타이밍에 맞춰 이 오브젝트를 SetActive로 켜고 끄는데,
// 여기서 오브젝트까지 건드리면 콜라이더 활성 타이밍이 어긋나 공격 판정이 깨진다.
// "그리느냐"와 "동작하느냐"를 분리해 두는 것이 요점이다.
public class DebugBoxMarker : DebugVisual
{
    [Tooltip("자식의 렌더러까지 포함할지")]
    [SerializeField] private bool includeChildren = true;

    // 켜고 끌 때마다 다시 모은다.
    // 판정 모양이 런타임에 바뀌면서(상자 → 부채꼴) 그림도 새로 붙으므로,
    // Awake에서 한 번만 모아 두면 나중에 생긴 표시가 영영 안 꺼진다.
    protected override void Apply(bool visible)
    {
        Renderer[] renderers = includeChildren
            ? GetComponentsInChildren<Renderer>(true)
            : GetComponents<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = visible;
    }
}
