using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ScenePortal : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "SelectChapter";

    [Header("페이드")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 1f;

    private bool isMoving = false;

    private void Start()
    {
        SetFadeAlpha(0f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isMoving)
            return;

        // 실제 움직이는 Exe의 Tag가 Player라서 이걸 기준으로 감지
        if (other.CompareTag("Player"))
        {
            StartCoroutine(MoveSceneRoutine());
        }
    }

    private IEnumerator MoveSceneRoutine()
    {
        isMoving = true;

        yield return StartCoroutine(FadeOut());

        SceneManager.LoadScene(targetSceneName);
    }

    private IEnumerator FadeOut()
    {
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;

            float alpha = timer / fadeDuration;
            SetFadeAlpha(alpha);

            yield return null;
        }

        SetFadeAlpha(1f);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null)
        {
            Debug.LogError("ScenePortal: fadeImage가 연결되지 않았습니다.");
            return;
        }

        Color color = fadeImage.color;
        color.a = alpha;
        fadeImage.color = color;
    }
}