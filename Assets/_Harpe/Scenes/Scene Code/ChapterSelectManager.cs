using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ChapterSelectManager : MonoBehaviour
{
    [Header("챕터 씬 이름")]
    [SerializeField] private string chapter1SceneName = "DemoScene 1";

    [Header("페이드")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 1f;

    private bool isLoading = false;

    private void Start()
    {
        SetFadeAlpha(0f);
    }

    public void SelectChapter1()
    {
        if (isLoading)
            return;

        StartCoroutine(LoadChapterRoutine(chapter1SceneName));
    }

    private IEnumerator LoadChapterRoutine(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("이동할 챕터 씬 이름이 비어 있습니다.");
            yield break;
        }

        isLoading = true;

        yield return StartCoroutine(FadeOut());

        SceneManager.LoadScene(sceneName);
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
            Debug.LogError("ChapterSelectManager: fadeImage가 연결되지 않았습니다.");
            return;
        }

        Color color = fadeImage.color;
        color.a = alpha;
        fadeImage.color = color;
    }
}