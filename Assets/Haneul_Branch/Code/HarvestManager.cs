using UnityEngine;

public class HarvestManager : MonoBehaviour
{
    public static HarvestManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    public void ExecuteHarvest(
        Enemy enemy,
        Transform player,
        Animator playerAnimator,
        SpriteRenderer playerSprite)
    {
        Debug.Log("ExecuteHarvest 호출");
        if (enemy == null)
            return;

        player.position =
            enemy.BackPosition.position;

        Vector2 dir =
            enemy.transform.position -
            player.position;

        if (dir.x != 0)
        {
            playerSprite.flipX = dir.x < 0;
        }

        playerAnimator.SetTrigger("Harvest");

        Destroy(enemy.gameObject);
    }
}