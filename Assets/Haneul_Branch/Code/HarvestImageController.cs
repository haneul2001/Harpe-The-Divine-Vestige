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

        harvestImage.SetActive(enemy.CanHarvest);
    }
}