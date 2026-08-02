using UnityEngine;

public class HarvestImageController : MonoBehaviour
{
    [SerializeField] private Enemy enemy;
    [SerializeField] private GameObject harvestImage;

    private void Awake()
    {
        // 인스펙터 참조가 비어 있으면 같은/부모 오브젝트의 Enemy를 자동으로 찾음
        if (enemy == null)
            enemy = GetComponentInParent<Enemy>();
    }

    private void Start()
    {
        if (harvestImage != null)
            harvestImage.SetActive(false);
    }

    private void Update()
    {
        if (enemy == null || harvestImage == null)
            return;

        // CanHarvest도 사망 시 false지만, 실행 순서와 무관하게 확실히 꺼지도록 여기서도 막는다
        bool show = !enemy.isDead && enemy.CanHarvest;

        if (harvestImage.activeSelf != show)
            harvestImage.SetActive(show);
    }
}