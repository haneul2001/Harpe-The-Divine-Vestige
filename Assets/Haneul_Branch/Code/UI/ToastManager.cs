using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 인게임 알림(토스트) 표시기.
//
// Canvas 아래 'ToastGroup' 오브젝트에 붙여 두면 된다.
// 어디서든 ToastManager.Show("문구") 한 줄로 띄운다.
//
// 화면 오른쪽 아래에서 위로 쌓인다 — 최신 알림이 항상 눈높이(아래쪽)에 온다.
public class ToastManager : MonoBehaviour
{
    public static ToastManager Instance { get; private set; }

    [Header("배치")]
    [SerializeField] private float width = 340f;
    [Tooltip("알림 상자 높이. 글자 길이로 자동 계산하지 않는다 —\n" +
             "폭이 정해지기 전 preferredHeight를 재면 값이 부풀려져 상자가 들쭉날쭉해진다")]
    [SerializeField] private float height = 44f;
    [Tooltip("글자 위아래 여백")]
    [SerializeField] private float labelPadding = 6f;
    [SerializeField] private float spacing = 8f;
    [Tooltip("화면 가장자리에서 띄우는 여백")]
    [SerializeField] private Vector2 margin = new Vector2(28f, 28f);
    [Tooltip("동시에 떠 있을 수 있는 최대 개수. 넘으면 가장 오래된 것부터 사라진다")]
    [Min(1)]
    [SerializeField] private int maxVisible = 4;

    [Header("표시 시간")]
    [SerializeField] private float holdDuration = 2.4f;

    [Header("모양")]
    [SerializeField] private Color background = new Color(0.16f, 0.16f, 0.18f, 0.94f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color infoAccent = new Color(0.55f, 0.60f, 0.70f);
    [SerializeField] private Color warnAccent = new Color(0.92f, 0.68f, 0.32f);
    [SerializeField] private int fontSize = 20;

    [Header("정렬")]
    [Tooltip("다른 UI 위에 뜨도록 이 그룹만 별도 정렬 순서를 갖는다")]
    [SerializeField] private int sortingOrder = 400;

    [SerializeField] private Font font;

    private readonly List<ToastView> live = new List<ToastView>();

    public enum Kind { Info, Warn }

    private void Awake()
    {
        Instance = this;
        Setup();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 어디서든 부르기 쉽게. 매니저가 없으면 조용히 무시한다.
    public static void Show(string message, Kind kind = Kind.Info)
    {
        if (Instance != null) Instance.Push(message, kind);
    }

    private void Setup()
    {
        var rect = (RectTransform)transform;

        // 오른쪽 아래 고정, 위로 자란다
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-margin.x, margin.y);
        rect.sizeDelta = new Vector2(width, 0f);

        // 부모 Canvas가 order 0이라 패널들에 가린다.
        // 자체 Canvas로 정렬을 덮어써 항상 위에 오게 한다.
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        var layout = GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.LowerRight;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.reverseArrangement = false;   // 새 알림이 아래로

        var fitter = GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    public void Push(string message, Kind kind = Kind.Info)
    {
        if (string.IsNullOrEmpty(message)) return;

        Prune();

        // 같은 문구가 이미 떠 있으면 새로 쌓지 않고 시간만 되돌린다.
        // ESC를 연타하면 같은 알림이 화면을 도배하는 것을 막는다.
        for (int i = 0; i < live.Count; i++)
        {
            if (live[i] != null && live[i].Message == message)
            {
                live[i].Bump();
                return;
            }
        }

        // 너무 많으면 오래된 것부터 정리
        while (live.Count >= maxVisible)
        {
            ToastView oldest = live[0];
            live.RemoveAt(0);
            if (oldest != null) Destroy(oldest.gameObject);
        }

        RectTransform slot = UIFactory.Empty("Toast", transform);
        ToastView view = slot.gameObject.AddComponent<ToastView>();
        view.Build(message, font, fontSize, width, height, labelPadding, holdDuration,
            background, textColor, kind == Kind.Warn ? warnAccent : infoAccent);

        live.Add(view);
    }

    private void Prune()
    {
        for (int i = live.Count - 1; i >= 0; i--)
            if (live[i] == null) live.RemoveAt(i);
    }
}
