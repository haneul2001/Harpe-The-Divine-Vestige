using UnityEngine;

public class HarvestImageController : MonoBehaviour
{
    [SerializeField] private Enemy enemy;
    [SerializeField] private GameObject harvestImage;

private void Start()
    {
        harvestImage.SetActive(false);
    }
    private void Update()
    {
        harvestImage.SetActive(enemy.CanHarvest);
    }
}