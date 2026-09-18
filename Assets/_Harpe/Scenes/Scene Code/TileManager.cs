using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleManager : MonoBehaviour
{
    [SerializeField] private string cutSceneName = "CutScene";

    [Header("페이드")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 1f;

    private bool isLoading = false;

    private void Start()
    {
        SetFadeAlpha(0f);
    }

    public void StartGame()
    {
        if (isLoading)
            return;

        StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        isLoading = true;

        yield return StartCoroutine(FadeOut());

        SceneManager.LoadScene(cutSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
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
            return;

        Color color = fadeImage.color;
        color.a = alpha;
        fadeImage.color = color;
    }
}