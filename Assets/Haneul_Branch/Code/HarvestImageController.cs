using UnityEngine;

public class HarvestImageController : MonoBehaviour
{
    [SerializeField] private Enemy enemy;
    [SerializeField] private GameObject harvestImage;

    private static HarvestMarkerStyle style;
    private static bool styleLoaded;

    private void Awake()
    {
        // 인스펙터 참조가 비어 있으면 같은/부모 오브젝트의 Enemy를 자동으로 찾음
        if (enemy == null)
            enemy = GetComponentInParent<Enemy>();
    }

    private void Start()
    {
        if (harvestImage != null)
        {
            baseLocalPos = harvestImage.transform.localPosition;
            ApplyStyle();
            harvestImage.SetActive(false);
        }
    }

    private void Update()
    {
        if (enemy == null || harvestImage == null)
            return;

        // CanHarvest도 사망 시 false지만, 실행 순서와 무관하게 확실히 꺼지도록 여기서도 막는다
        bool show = !enemy.isDead && enemy.CanHarvest;

        if (harvestImage.activeSelf != show)
            harvestImage.SetActive(show);

        // 스폰 후 몬스터 배율이 바뀌거나 다른 코드가 표시 배율을 되돌려도 크기가 유지되게, 보일 때마다 맞춘다
        if (show) FitScale();
    }

    private Transform backEffect;
    private Vector3 baseLocalPos;

    private void FitScale()
    {
        if (style == null) return;
        Transform t = harvestImage.transform;

        if (style.icon != null)
        {
            Vector3 ps = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localPosition = baseLocalPos + new Vector3(0f, style.iconOffsetY / Mathf.Max(0.0001f, Mathf.Abs(ps.y)), 0f);
            float k = style.iconWorldSize / Mathf.Max(style.icon.bounds.size.x, 0.0001f);
            t.localScale = new Vector3(k / Mathf.Max(0.0001f, Mathf.Abs(ps.x)), k / Mathf.Max(0.0001f, Mathf.Abs(ps.y)), 1f);
        }

        if (backEffect != null)
        {
            Vector3 ls = t.lossyScale;
            backEffect.localScale = new Vector3(
                style.backEffectScale / Mathf.Max(0.0001f, Mathf.Abs(ls.x)),
                style.backEffectScale / Mathf.Max(0.0001f, Mathf.Abs(ls.y)), 1f);
            backEffect.localPosition = new Vector3(
                style.backEffectOffset.x / Mathf.Max(0.0001f, Mathf.Abs(ls.x)),
                style.backEffectOffset.y / Mathf.Max(0.0001f, Mathf.Abs(ls.y)), 0f);
        }
    }

    // 공통 모양(해골 + 뒤에서 타오르는 이펙트)을 입힌다. 표시 오브젝트를 껐다 켜면
    // 루프 파티클도 같이 꺼졌다 켜지므로 따로 재생을 관리할 필요가 없다.
    private void ApplyStyle()
    {
        if (!styleLoaded)
        {
            style = Resources.Load<HarvestMarkerStyle>(HarvestMarkerStyle.ResourcePath);
            styleLoaded = true;
        }
        if (style == null) return;

        var sr = harvestImage.GetComponent<SpriteRenderer>();
        Transform t = harvestImage.transform;

        if (sr != null && style.icon != null)
        {
            sr.sprite = style.icon;
            sr.color = Color.white;
        }

        if (style.backEffect != null && t.Find("HarvestBackEffect") == null)
        {
            var fx = Instantiate(style.backEffect, t);
            fx.name = "HarvestBackEffect";
            backEffect = fx.transform;

            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            // 해골보다 뒤에 — 같은 정렬 레이어에서 한 칸 아래
            foreach (var r in fx.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                r.sortingLayerID = sr != null ? sr.sortingLayerID : r.sortingLayerID;
                r.sortingOrder = (sr != null ? sr.sortingOrder : 0) - 1;
                if (style.backEffectMaterial != null && r.renderMode != ParticleSystemRenderMode.None)
                    r.sharedMaterial = style.backEffectMaterial;
            }
        }
    }
}
