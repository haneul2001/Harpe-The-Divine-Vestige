using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 사망 결과 화면. 로그라이트 한 판의 마침표.
//
// 일시정지 메뉴와 같은 조립 방식(런타임 코드 조립 + MenuItemView)을 쓴다.
// 다른 건 "닫을 수 없다"는 점 하나다 — 여기서 나가는 길은 재시작과 타이틀뿐이라
// ESC로 빠져나가는 경로를 일부러 막아 둔다.
public class GameOverScreen : MonoBehaviour
{
    public static GameOverScreen Instance { get; private set; }

    // 일시정지 메뉴가 "지금 결과 화면이 떠 있는지"를 물어본다.
    public static bool IsShowing
    {
        get { return Instance != null && Instance.isOpen; }
    }

    [Header("씬")]
    [Tooltip("타이틀로 나갈 때 불러올 씬 이름. 빌드 설정에 들어 있어야 한다")]
    [SerializeField] private string titleSceneName = "TITLE";

    [Header("배치")]
    [SerializeField] private float columnWidth = 640f;
    [SerializeField] private float rowHeight = 46f;
    [SerializeField] private float itemHeight = 54f;
    [SerializeField] private float itemSpacing = 6f;
    [SerializeField] private float slideDistance = 12f;

    [Header("색 (사신 톤)")]
    [SerializeField] private Color backdropColor = new Color(0.02f, 0.02f, 0.04f, 0.90f);
    [SerializeField] private Color accentColor = new Color(0.741f, 0.667f, 0.867f);
    [SerializeField] private Color titleColor = new Color(0.596f, 0.157f, 0.208f);
    [Tooltip("마지막 층을 깼을 때의 제목 색")]
    [SerializeField] private Color victoryTitleColor = new Color(0.741f, 0.667f, 0.867f);
    [SerializeField] private Color idleTextColor = new Color(0.616f, 0.635f, 0.671f);
    [SerializeField] private Color valueColor = new Color(0.878f, 0.855f, 0.914f);

    [Header("글자 크기")]
    [SerializeField] private int titleSize = 96;
    [SerializeField] private int subtitleSize = 24;
    [SerializeField] private int rowSize = 26;
    [SerializeField] private int itemSize = 30;

    [Header("연출")]
    [Tooltip("화면이 떠오르는 시간(초). 정지 중이라 실제 시간으로 잰다")]
    [SerializeField] private float fadeDuration = 0.4f;

    [Header("폰트 (비우면 씬에서 가져옴)")]
    [SerializeField] private Font font;

    private bool isOpen;
    private bool victory;
    private GameObject rootGo;
    private CanvasGroup group;
    private readonly List<MenuItemView> items = new List<MenuItemView>();
    private int index;

    // 플레이어가 죽으면 이걸 부른다. 화면이 없으면 그 자리에서 하나 만든다.
    // 마지막 층을 깨고 끝난 경우엔 won=true로 부른다 — 표는 같고 제목만 다르다.
    public static void Show(bool won = false)
    {
        if (Instance == null)
        {
            var found = FindObjectOfType<GameOverScreen>();
            if (found == null) found = new GameObject("GameOverScreen").AddComponent<GameOverScreen>();
            Instance = found;
        }

        Instance.victory = won;
        Instance.Open();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!isOpen || items.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(1);
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(-1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            items[index].Submit();
    }

    private void Open()
    {
        if (isOpen) return;
        isOpen = true;

        if (rootGo == null) BuildUI();
        rootGo.SetActive(true);

        // 결과 화면이 뜨는 순간 세상은 멈춘다. 뒤에서 적이 계속 움직이면
        // "죽었는데도 게임이 돌아간다"는 인상을 준다.
        Time.timeScale = 0f;

        Select(0);
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / Mathf.Max(0.01f, fadeDuration));
            yield return null;
        }
        group.alpha = 1f;
    }

    private void Move(int delta)
    {
        Select((index + delta + items.Count) % items.Count);
    }

    private void Select(int i)
    {
        index = Mathf.Clamp(i, 0, items.Count - 1);
        for (int n = 0; n < items.Count; n++)
            items[n].SetSelected(n == index);
    }

    // ─────────────────────────────────────────────
    // 메뉴 동작
    // ─────────────────────────────────────────────

    private void OnRestart()
    {
        Time.timeScale = 1f;   // 씬을 넘기기 전에 반드시 되돌린다

        Scene s = SceneManager.GetActiveScene();
        if (s.buildIndex < 0)
        {
            Debug.LogError("[GameOverScreen] 현재 씬이 빌드 설정에 없어 다시 시작할 수 없다: " + s.name);
            return;
        }
        SceneManager.LoadScene(s.buildIndex);
    }

    private void OnTitle()
    {
        Time.timeScale = 1f;

        if (Application.CanStreamedLevelBeLoaded(titleSceneName))
            SceneManager.LoadScene(titleSceneName);
        else
            Debug.LogError("[GameOverScreen] 타이틀 씬을 빌드 설정에서 찾지 못했다: " + titleSceneName);
    }

    private void OnQuit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─────────────────────────────────────────────
    // 조립
    // ─────────────────────────────────────────────

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("GameOverCanvas", typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 260;   // 일시정지(250) 위, 치트(300) 아래

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        rootGo = UIFactory.Stretch("Root", canvasGo.transform).gameObject;
        group = rootGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        Image backdrop = UIFactory.Panel("Backdrop", rootGo.transform, backdropColor);
        UIFactory.SetAnchoredBox(backdrop.rectTransform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);

        // 화면 중앙에서 아래로 쌓는다. y를 하나씩 내려가며 배치.
        float y = 330f;

        y = BuildTitle(y);
        y = BuildStats(y - 34f);
        BuildItems(y - 40f);
    }

    private float BuildTitle(float y)
    {
        Text title = CenteredLabel("Title", titleSize,
            victory ? victoryTitleColor : titleColor, FontStyle.Bold);
        PlaceCentered(title.rectTransform, y - 110f, y);
        title.text = victory ? "탈 출" : "패 배";

        Image rule = UIFactory.Panel("Rule", rootGo.transform, accentColor, false);
        UIFactory.SetAnchoredBox(rule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-60f, y - 124f), new Vector2(60f, y - 121f));

        Text sub = CenteredLabel("Subtitle", subtitleSize, idleTextColor, FontStyle.Normal);
        PlaceCentered(sub.rectTransform, y - 168f, y - 136f);
        sub.text = victory ? "던전의 끝에 닿았다" : "사신의 잔재는 여기서 끊겼다";

        return y - 168f;
    }

    private float BuildStats(float y)
    {
        RunStats r = RunStats.Instance;

        // (이름, 값) — 기록이 없으면 0으로 채운다. 화면이 비는 것보다 낫다.
        var rows = new List<KeyValuePair<string, string>>();
        rows.Add(new KeyValuePair<string, string>("도달 층", (r != null ? r.Floor : 1) + " 층"));
        rows.Add(new KeyValuePair<string, string>("탐험한 방",
            r != null ? r.RoomsCleared + " / " + r.TotalRooms : "0 / 0"));
        rows.Add(new KeyValuePair<string, string>("처치한 적", (r != null ? r.Kills : 0) + " 체"));
        rows.Add(new KeyValuePair<string, string>("획득한 카드", (r != null ? r.CardsOwned : 0) + " 장"));
        rows.Add(new KeyValuePair<string, string>("생존 시간",
            RunStats.FormatTime(r != null ? r.Elapsed : 0f)));

        for (int i = 0; i < rows.Count; i++)
        {
            float top = y - i * rowHeight;
            float bottom = top - rowHeight;

            Text name = UIFactory.Label("RowName_" + i, rootGo.transform, font, rowSize,
                idleTextColor, TextAnchor.MiddleLeft);
            PlaceCentered(name.rectTransform, bottom, top);
            name.text = rows[i].Key;

            Text value = UIFactory.Label("RowValue_" + i, rootGo.transform, font, rowSize,
                valueColor, TextAnchor.MiddleRight, FontStyle.Bold);
            PlaceCentered(value.rectTransform, bottom, top);
            value.text = rows[i].Value;

            // 줄 아래에 깔리는 옅은 선. 표를 그리지 않아도 어느 값이 어느 줄인지 읽힌다.
            Image dash = UIFactory.Panel("RowRule_" + i, rootGo.transform,
                new Color(accentColor.r, accentColor.g, accentColor.b, 0.12f), false);
            UIFactory.SetAnchoredBox(dash.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-columnWidth * 0.5f, bottom), new Vector2(columnWidth * 0.5f, bottom + 1f));
        }

        return y - rows.Count * rowHeight;
    }

    private void BuildItems(float y)
    {
        var defs = new List<KeyValuePair<string, System.Action>>
        {
            new KeyValuePair<string, System.Action>("다시 시작", OnRestart),
            new KeyValuePair<string, System.Action>("타이틀로",  OnTitle),
            new KeyValuePair<string, System.Action>("나가기",    OnQuit),
        };

        float step = itemHeight + itemSpacing;
        // 메뉴는 표보다 좁게 잡아 가운데로 모은다. 표 폭 그대로면 글자가 왼쪽에 홀로 떨어진다.
        float half = 150f;

        for (int i = 0; i < defs.Count; i++)
        {
            RectTransform slot = UIFactory.Empty("Item_" + i, rootGo.transform);
            UIFactory.SetAnchoredBox(slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-half, y - (i + 1) * step),
                new Vector2(half, y - i * step - itemSpacing));

            MenuItemView item = slot.gameObject.AddComponent<MenuItemView>();
            item.Build(defs[i].Key, font, itemSize, idleTextColor, accentColor, accentColor, slideDistance);

            int captured = i;
            System.Action action = defs[i].Value;
            item.onSelected = () => Select(captured);
            item.onSubmit = action;

            items.Add(item);
        }
    }

    private Text CenteredLabel(string name, int size, Color color, FontStyle style)
    {
        return UIFactory.Label(name, rootGo.transform, font, size, color, TextAnchor.MiddleCenter, style);
    }

    // 화면 중앙 기준으로 세로 구간만 지정하면 가로는 표 폭에 맞춰 준다.
    private void PlaceCentered(RectTransform rt, float bottom, float top)
    {
        UIFactory.SetAnchoredBox(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-columnWidth * 0.5f, bottom), new Vector2(columnWidth * 0.5f, top));
    }
}
