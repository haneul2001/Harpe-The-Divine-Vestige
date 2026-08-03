using UnityEngine;

// 처형이 적중하면 주변 몹에게 충격파 피해.
//
// 카드 효과 하나가 얼마나 작아질 수 있는지 보여주는 예시 —
// 이 클래스는 "언제 터지는지"도 "누가 이 효과를 가졌는지"도 모른다.
// 그건 각각 HarvestManager와 AbilityEffectRunner의 몫이다.
[CreateAssetMenu(fileName = "Effect_HarvestShockwave", menuName = "Harpe/Ability/Effect/처형 충격파")]
public class HarvestShockwaveEffect : AbilityEffect
{
    [Header("범위")]
    [Tooltip("처형 지점 기준 반경(유닛). 방이 18x10이므로 3~5면 근처 몹만 닿는다")]
    [SerializeField] private float radius = 3.5f;

    [Tooltip("적으로 취급할 레이어. 비우면 Enemy 레이어를 자동으로 쓴다")]
    [SerializeField] private LayerMask enemyLayers;

    [Header("피해")]
    [SerializeField] private int damage = 3;

    [Tooltip("체크하면 치명타 숫자로 표시된다")]
    [SerializeField] private bool asCritical = false;

    [Header("연출")]
    [Tooltip("충격파 이펙트 프리팹 (선택). 비우면 아래 링으로 대체된다")]
    [SerializeField] private GameObject vfxPrefab;

    [SerializeField] private float vfxLifetime = 1f;

    [Tooltip("프리팹이 없을 때 퍼져 나가는 원형 링을 그린다")]
    [SerializeField] private bool drawRing = true;
    [SerializeField] private Color ringColor = new Color(0.55f, 0.95f, 1f, 0.9f);
    [SerializeField] private float ringDuration = 0.35f;

    [Tooltip("터질 때 카메라 흔들림. 0이면 없음")]
    [SerializeField] private float shake = 0.35f;

    [Header("디버그")]
    [Tooltip("게임 화면에 반경을 반투명 원판으로 띄운다 (적의 AttackRange 표시와 같은 방식).\n" +
             "콘솔에도 감지/타격 수를 남긴다.")]
    [SerializeField] private bool debugArea = false;
    [SerializeField] private Color debugColor = new Color(1f, 0.35f, 0.35f, 0.55f);
    [SerializeField] private float debugDuration = 1.5f;

    // 매 처형마다 배열을 새로 잡지 않도록 재사용. 효과 에셋은 하나뿐이라 안전하다.
    private static readonly Collider2D[] buffer = new Collider2D[32];

    public override void OnHarvestImpact(AbilityContext ctx, HarvestImpact impact)
    {
        int mask = enemyLayers.value != 0 ? enemyLayers.value : LayerMask.GetMask("Enemy");

        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(mask);

        int count = Physics2D.OverlapCircle(impact.position, radius, filter, buffer);

        // 같은 적이 콜라이더 여러 개(몸통·발밑)로 잡히므로 중복 타격을 막는다
        int hits = 0;
        Enemy last = null;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = buffer[i];
            if (col == null) continue;

            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy == null || enemy == last) continue;

            // 처형 대상은 제외 — 이미 죽었고, 데미지 숫자도 뜨면 안 된다.
            // (isDead 검사만으로도 걸러지지만 의도를 드러내기 위해 둘 다 둔다)
            if (enemy == impact.target) continue;
            if (enemy.isDead) continue;

            enemy.TakeDamage(damage, asCritical);
            last = enemy;
            hits++;
        }

        if (debugArea)
        {
            Debug.Log($"[충격파] 위치 {impact.position} 반경 {radius} — 콜라이더 {count}개 감지, {hits}마리 타격 (마스크 {mask})");
            AreaMarker.Show(impact.position, radius, debugColor, debugDuration);
        }

        SpawnVfx(impact.position);

        // 아무도 안 맞았으면 화면을 흔들 이유가 없다
        if (hits > 0 && shake > 0f) CameraShake.Shake(shake);
    }

    private void SpawnVfx(Vector3 position)
    {
        if (vfxPrefab != null)
        {
            GameObject go = Object.Instantiate(vfxPrefab, position, Quaternion.identity);
            go.transform.localScale = Vector3.one * (radius * 2f);

            if (vfxLifetime > 0f) Object.Destroy(go, vfxLifetime);
            return;
        }

        if (drawRing)
            ShockwaveRing.Spawn(position, radius, ringColor, ringDuration);
    }

#if UNITY_EDITOR
    // 인스펙터에서 반경을 감 잡을 수 있게 (선택된 카드 에셋일 때만 의미 있음)
    private void OnValidate()
    {
        if (radius < 0.1f) radius = 0.1f;
    }
#endif
}
