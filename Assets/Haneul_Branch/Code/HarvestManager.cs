using System.Collections;
using UnityEngine;

public class HarvestManager : MonoBehaviour
{
    public static HarvestManager Instance;

    [SerializeField] private float harvestDuration = 1.5f;

    private bool isHarvesting = false;

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
        if (isHarvesting)
            return;

        if (enemy == null)
        {
            Debug.LogError("Harvest 실패: enemy가 null입니다.");
            return;
        }

        if (player == null)
        {
            Debug.LogError("Harvest 실패: player가 null입니다.");
            return;
        }

        if (playerAnimator == null)
        {
            Debug.LogError("Harvest 실패: playerAnimator가 null입니다.");
            return;
        }

        if (playerSprite == null)
        {
            Debug.LogError("Harvest 실패: playerSprite가 null입니다.");
            return;
        }

        if (enemy.BackPosition == null)
        {
            Debug.LogError("Harvest 실패: enemy.BackPosition이 null입니다.");
            return;
        }

        StartCoroutine(HarvestCoroutine(enemy, player, playerAnimator, playerSprite));
    }

    private IEnumerator HarvestCoroutine(
        Enemy enemy,
        Transform player,
        Animator playerAnimator,
        SpriteRenderer playerSprite)
    {
        isHarvesting = true;

        PlayerMove playerMove = player.GetComponent<PlayerMove>();

        if (playerMove != null)
        {
            playerMove.isExecuting = true;
        }

        // 플레이어를 몬스터 뒤로 이동
        player.position = 
                enemy.BackPosition.position + new Vector3(0f, -0.5f, 0f);
        // 플레이어가 몬스터를 바라보게 방향 설정
        Vector2 dir = enemy.transform.position - player.position;

        if (dir.x != 0)
        {
            playerSprite.flipX = dir.x < 0;
        }

        // 플레이어 처형 애니메이션
        playerAnimator.SetTrigger("Harvest");

        // 몬스터 사망 애니메이션 + 1.5초 뒤 제거
        enemy.HarvestDie(harvestDuration);

        // 나중에 여기 근처에 soul stack 증가 넣으면 됨
        // 예: playerStatus.AddSoulStack(1);

        yield return new WaitForSeconds(harvestDuration);

        if (playerMove != null)
        {
            playerMove.isExecuting = false;
        }

        isHarvesting = false;
    }
}