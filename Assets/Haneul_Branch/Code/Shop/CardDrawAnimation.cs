using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 상점 뽑기 연출: 엎어 둔 카드가 떨리다가 → 뒷면이 뒤집히고 → 앞면이 드러난다.
//
// 그림은 2D Pixel Quest Vol.3의 Flip 4프레임을 그대로 쓴다.
//   뒷면 01(원래) → 02(살짝 커짐) → 03(좁아짐) → 04(옆면)
//   앞면 01(좁음) → 02(빛 지나감) → 03(빛 빠져나감) → 04(정지)
// 앞면은 진짜 AbilityCardView의 테두리 그림만 프레임마다 갈아 끼운다 —
// 연출용 카드를 따로 만들면 아이콘·이름 위치가 결과 카드와 한 픽셀이라도 어긋난다.
//
// 상점은 시간을 멈추므로 전부 unscaled 시간으로 돈다.
public class CardDrawAnimation : MonoBehaviour
{
    // 앞면 01 프레임은 그림이 79px 폭(원래 96px)이라 아이콘·이름도 같은 비율로 좁힌다
    private const float FaceNarrowScale = 79f / 96f;

    private RectTransform holder;
    private AbilityCardView view;
    private CanvasGroup viewGroup;
    private Image back;
    private Image burst;
    private AbilityRarity rarity;
    private Sprite[] backFrames;
    private Sprite[] faceFrames;
    private Sprite restingFrame;
    private float pixel;
    private float chargeOverride = -1f;   // 0 이상이면 이 시간만큼만 떤다 (0 = 떨지 않고 바로 뒤집기)
    private float startDelay;
    private System.Action onDone;

    private Coroutine running;
    private bool finished;

    public bool IsPlaying => !finished;

    // holder: 카드 한 장 크기의 자리 / view: 이미 조립된 결과 카드 (holder의 자식)
    public static CardDrawAnimation Play(RectTransform holder, AbilityCardView view, AbilityRarity rarity,
        Sprite[] backFrames, Sprite[] faceFrames, Sprite restingFrame, float pixelScale, System.Action onDone,
        float chargeOverride = -1f, float startDelay = 0f)
    {
        var anim = holder.gameObject.AddComponent<CardDrawAnimation>();
        anim.holder = holder;
        anim.view = view;
        anim.rarity = rarity;
        anim.backFrames = backFrames;
        anim.faceFrames = faceFrames;
        anim.restingFrame = restingFrame;
        anim.pixel = Mathf.Max(1f, pixelScale);
        anim.chargeOverride = chargeOverride;
        anim.startDelay = startDelay;
        anim.onDone = onDone;
        anim.Build();
        anim.running = anim.StartCoroutine(anim.Run());
        return anim;
    }

    private void Build()
    {
        // 등급 빛 — 카드 뒤에 깔리는 픽셀 방사광
        burst = UIFactory.Panel("DrawBurst", holder, Color.clear, false);
        burst.sprite = CardFx.Burst();
        burst.rectTransform.anchorMin = burst.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        burst.rectTransform.sizeDelta = Vector2.one * 48f * pixel * 4.5f;   // 텍셀 하나 = UI 9칸 (정수배)
        burst.transform.SetAsFirstSibling();

        // 결과 카드는 뒷면이 다 넘어갈 때까지 숨긴다
        viewGroup = view.gameObject.AddComponent<CanvasGroup>();
        viewGroup.alpha = 0f;
        viewGroup.blocksRaycasts = false;

        back = UIFactory.Panel("DrawBack", holder, Color.white, false);
        back.sprite = backFrames[0];
        UIFactory.SetAnchoredBox(back.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private IEnumerator Run()
    {
        // 여러 장을 차례로 뒤집을 때 쓰는 시간차
        if (startDelay > 0f) yield return Wait(startDelay);

        // ── 1. 기대감: 뒷면이 점점 세게 떨리고 뒤에서 등급 빛이 샌다 ──
        // 높은 등급일수록 오래·세게 떨어서 "뭔가 온다"는 게 먼저 읽히게 한다
        float charge = chargeOverride >= 0f ? chargeOverride : ChargeTime(rarity);
        int maxShake = 1 + (int)rarity;              // 그림 픽셀 단위 (1~4)
        Color c = rarity.Color();
        float t = 0f;
        float nextJitter = 0f;
        while (t < charge && charge > 0f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / charge);

            // 떨림은 그림 픽셀 단위로만 — 반 픽셀 이동은 테두리가 번져 보인다
            if (t >= nextJitter)
            {
                nextJitter = t + 0.035f;
                int amp = Mathf.CeilToInt(maxShake * k * k);
                back.rectTransform.anchoredPosition = new Vector2(
                    Random.Range(-amp, amp + 1) * pixel,
                    Mathf.Round(4f * k) * pixel + Random.Range(-amp, amp + 1) * pixel * 0.5f);
            }

            burst.color = new Color(c.r, c.g, c.b, 0.45f * k * k);
            yield return null;
        }
        back.rectTransform.anchoredPosition = Vector2.zero;

        // ── 2. 뒷면 뒤집기 ──
        for (int i = 1; i < backFrames.Length; i++)
        {
            back.sprite = backFrames[i];
            yield return Wait(i == 1 ? 0.06f : 0.045f);
        }
        back.gameObject.SetActive(false);

        // ── 3. 앞면 드러나기 ──
        viewGroup.alpha = 1f;
        Image border = view.FrameImage;
        RectTransform content = view.Content;

        for (int i = 0; i < faceFrames.Length; i++)
        {
            border.sprite = faceFrames[i];
            content.localScale = new Vector3(i == 0 ? FaceNarrowScale : 1f, 1f, 1f);

            if (i == 1)
            {
                // 완전히 펴지는 순간 등급 빛이 터진다
                burst.color = new Color(c.r, c.g, c.b, 1f);
                view.transform.localScale = Vector3.one * PunchScale(rarity);
            }
            yield return Wait(i == 0 ? 0.045f : i == faceFrames.Length - 1 ? 0.09f : 0.07f);
        }

        // ── 4. 안착: 카드는 원래 크기로, 빛은 사그라든다 ──
        border.sprite = restingFrame != null ? restingFrame : faceFrames[faceFrames.Length - 1];
        float punch = view.transform.localScale.x;
        t = 0f;
        const float settle = 0.45f;
        while (t < settle)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / settle);
            float s = Mathf.Lerp(punch, 1f, 1f - (1f - Mathf.Clamp01(k * 3f)) * (1f - Mathf.Clamp01(k * 3f)));
            view.transform.localScale = Vector3.one * s;
            burst.color = new Color(c.r, c.g, c.b, 1f - k);
            yield return null;
        }

        Complete();
    }

    // 상점을 닫거나 다시 뽑으면 연출을 건너뛰고 바로 결과 상태로
    public void Complete()
    {
        if (finished) return;
        finished = true;

        if (running != null) StopCoroutine(running);

        if (back != null) Destroy(back.gameObject);
        if (burst != null) Destroy(burst.gameObject);

        if (view != null)
        {
            view.transform.localScale = Vector3.one;
            view.Content.localScale = Vector3.one;
            if (restingFrame != null) view.FrameImage.sprite = restingFrame;
            if (viewGroup != null)
            {
                viewGroup.alpha = 1f;
                viewGroup.blocksRaycasts = true;
            }
        }

        if (onDone != null) onDone();
        Destroy(this);
    }

    private void OnDisable()
    {
        // 상점 루트가 꺼지면 코루틴도 멈춘다 — 멈춘 채로 남지 않게 끝 상태로 보낸다
        Complete();
    }

    private static IEnumerator Wait(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end) yield return null;
    }

    private static float ChargeTime(AbilityRarity r)
    {
        switch (r)
        {
            case AbilityRarity.Legendary: return 0.8f;
            case AbilityRarity.Epic: return 0.55f;
            case AbilityRarity.Rare: return 0.4f;
            default: return 0.3f;
        }
    }

    private static float PunchScale(AbilityRarity r)
    {
        return 1.04f + 0.02f * (int)r;
    }
}
