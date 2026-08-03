using UnityEngine;
using UnityEngine.UI;

// 알림 한 줄.
//
// 오버워치/FF14의 경고 토스트처럼 오른쪽에서 밀려 들어와 잠깐 머물다 사라진다.
//
// ★ 구조 주의
//   루트는 VerticalLayoutGroup이 위치를 소유한다. 애니메이션이 루트의
//   anchoredPosition을 건드리면 레이아웃이 다시 계산될 때마다 위치가 튄다.
//   그래서 내용물을 slide 자식으로 한 겹 내리고 그것만 움직인다.
public class ToastView : MonoBehaviour
{
    // Out과 Collapse를 나눈 이유:
    //   높이 줄이기와 오른쪽 밀기를 같이 하면, 그룹이 아래쪽 기준이라
    //   토스트가 내려가면서 오른쪽으로 가는 "대각선" 퇴장이 된다.
    //   먼저 정직하게 오른쪽으로 빠지고, 안 보이게 된 뒤에 자리를 접는다.
    private enum Phase { In, Hold, Out, Collapse }

    private RectTransform slide;      // 실제로 움직이는 자식
    private CanvasGroup group;
    private LayoutElement layout;

    private Phase phase;
    private float timer;
    private float holdDuration;
    private float height;

    private const float InDuration = 0.18f;
    private const float OutDuration = 0.22f;
    private const float CollapseDuration = 0.12f;
    private const float SlideDistance = 70f;

    public string Message { get; private set; }

    public void Build(string message, Font font, int fontSize, float width, float boxHeight,
        float labelPadding, float hold, Color background, Color textColor, Color accentColor)
    {
        Message = message;
        holdDuration = hold;
        height = boxHeight;

        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;   // 알림이 클릭을 막으면 안 된다
        group.interactable = false;

        // 루트는 크기만 담당한다. 그림은 전부 slide 안에.
        layout = gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = height;

        slide = UIFactory.Stretch("Slide", transform);

        Image bg = slide.gameObject.AddComponent<Image>();
        bg.color = background;
        bg.raycastTarget = false;

        // 왼쪽 색 띠 — 종류를 색으로 구분
        Image accent = UIFactory.Panel("Accent", slide, accentColor, false);
        UIFactory.SetAnchoredBox(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(4f, 0f));

        Text label = UIFactory.Label("Label", slide, font, fontSize, textColor, TextAnchor.MiddleLeft);
        UIFactory.SetAnchoredBox(label.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(18f, labelPadding), new Vector2(-14f, -labelPadding));
        // 한 줄로 고정 — 폭이 정해지기 전 preferredHeight를 믿으면 상자가 엉뚱하게 커진다
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.text = message;

        phase = Phase.In;
        timer = 0f;
        Apply(0f);
    }

    // 같은 알림이 또 오면 새로 쌓지 않고 이걸 부른다 (연타 시 화면 도배 방지)
    public void Bump()
    {
        phase = Phase.Hold;
        timer = 0f;
        if (slide != null) slide.localScale = Vector3.one * 1.06f;
    }

    private void Update()
    {
        // 일시정지 중에도 떠야 하므로 unscaled
        float dt = Time.unscaledDeltaTime;
        timer += dt;

        slide.localScale = Vector3.Lerp(slide.localScale, Vector3.one, dt / 0.08f);

        switch (phase)
        {
            case Phase.In:
                if (timer >= InDuration) { phase = Phase.Hold; timer = 0f; Apply(1f); }
                else Apply(Mathf.Clamp01(timer / InDuration));
                break;

            case Phase.Hold:
                if (timer >= holdDuration) { phase = Phase.Out; timer = 0f; }
                break;

            case Phase.Out:
                float k = Mathf.Clamp01(timer / OutDuration);
                ApplyOut(k);
                if (k >= 1f) { phase = Phase.Collapse; timer = 0f; }
                break;

            case Phase.Collapse:
                float c = Mathf.Clamp01(timer / CollapseDuration);
                layout.preferredHeight = height * (1f - c);
                if (c >= 1f) Destroy(gameObject);
                break;
        }
    }

    // 0 = 화면 밖 / 1 = 제자리
    private void Apply(float k)
    {
        float eased = 1f - (1f - k) * (1f - k);   // 빠르게 들어와 부드럽게 멈춤
        group.alpha = eased;
        SetSlideX(SlideDistance * (1f - eased));
    }

    // 오른쪽으로만 빠진다. 높이는 여기서 건드리지 않는다 (Collapse 단계에서 처리).
    private void ApplyOut(float k)
    {
        float eased = k * k;   // 천천히 시작해 빠르게 사라짐
        group.alpha = 1f - eased;
        SetSlideX(SlideDistance * eased);
    }

    private void SetSlideX(float x)
    {
        slide.offsetMin = new Vector2(x, 0f);
        slide.offsetMax = new Vector2(x, 0f);
    }
}
