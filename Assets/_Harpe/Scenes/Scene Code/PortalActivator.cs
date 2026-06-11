using UnityEngine;
using UnityEngine.SceneManagement;

public class PortalActivator : MonoBehaviour
{
    [Header("활성화할 포탈")]
    [SerializeField] private GameObject portal;

    [Header("예외 씬")]
    [SerializeField] private string townSceneName = "Town";

    private bool portalActivated = false;

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == townSceneName)
            return;

        if (portal != null)
        {
            portal.SetActive(false);
        }
        else
        {
            Debug.LogError("PortalActivator: Portal 오브젝트가 연결되지 않았습니다.");
        }
    }

    private void Update()
    {
        if (portalActivated)
            return;

        if (SceneManager.GetActiveScene().name == townSceneName)
            return;

        Enemy[] enemies = FindObjectsOfType<Enemy>();

        if (enemies.Length <= 0)
        {
            ActivatePortal();
        }
    }

    private void ActivatePortal()
    {
        if (portal == null)
        {
            Debug.LogError("PortalActivator: Portal 오브젝트가 연결되지 않았습니다.");
            return;
        }

        portalActivated = true;
        portal.SetActive(true);

        Debug.Log("모든 몬스터 처치 완료 → Portal 활성화");
    }
}