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

    private Renderer[] renderers;

    private void Awake()
    {
        renderers = includeChildren
            ? GetComponentsInChildren<Renderer>(true)
            : GetComponents<Renderer>();
    }

    protected override void Apply(bool visible)
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = visible;
    }
}
