using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CutSceneManager : MonoBehaviour
{
    [Header("컷신 이미지 오브젝트")]
    [SerializeField] private GameObject[] cutScenes;

    [Header("시간 설정")]
    [SerializeField] private float sceneDuration = 5f;
    [SerializeField] private float fadeDuration = 1f;

    [Header("다음 씬")]
    [SerializeField] private string nextSceneName = "DemoScene 1";

    [Header("페이드 이미지")]
    [SerializeField] private Image fadeImage;

    private int currentIndex = 0;
    private bool isChanging = false;
    private Coroutine autoNextCoroutine;

    private void Start()
    {
        for (int i = 0; i < cutScenes.Length; i++)
        {
            cutScenes[i].SetActive(false);
        }

        SetFadeAlpha(0f);

        ShowCutScene(0);

        // 마지막 컷신이 아닐 때만 자동 넘김 시작
        if (!IsLastCutScene())
        {
            autoNextCoroutine = StartCoroutine(AutoNextRoutine());
        }
    }

    private void Update()
    {
        if (Input.anyKeyDown && !isChanging)
        {
            StartCoroutine(NextCutSceneRoutine());
        }
    }

    private IEnumerator AutoNextRoutine()
    {
        yield return new WaitForSeconds(sceneDuration);

        if (!isChanging && !IsLastCutScene())
        {
            StartCoroutine(NextCutSceneRoutine());
        }
    }

    private IEnumerator NextCutSceneRoutine()
    {
        isChanging = true;

        if (autoNextCoroutine != null)
        {
            StopCoroutine(autoNextCoroutine);
            autoNextCoroutine = null;
        }

        yield return StartCoroutine(FadeOut());

        currentIndex++;

        if (currentIndex >= cutScenes.Length)
        {
            SceneManager.LoadScene(nextSceneName);
            yield break;
        }

        ShowCutScene(currentIndex);

        yield return StartCoroutine(FadeIn());

        isChanging = false;

        // 마지막 컷신이 아닐 때만 5초 자동 넘김 시작
        if (!IsLastCutScene())
        {
            autoNextCoroutine = StartCoroutine(AutoNextRoutine());
        }
    }

    private void ShowCutScene(int index)
    {
        for (int i = 0; i < cutScenes.Length; i++)
        {
            cutScenes[i].SetActive(i == index);
        }
    }

    private bool IsLastCutScene()
    {
        return currentIndex == cutScenes.Length - 1;
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

    private IEnumerator FadeIn()
    {
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = 1f - (timer / fadeDuration);
            SetFadeAlpha(alpha);

            yield return null;
        }

        SetFadeAlpha(0f);
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