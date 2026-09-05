using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ESC 일시정지 메뉴.
//
// 하데스/할로우 나이트처럼 화면을 어둡게 깔고 왼쪽에 세로 메뉴를 세운다.
// 마우스와 키보드(↑↓/Enter)가 같은 선택 상태를 공유한다.
//
// 다른 패널과의 관계:
//   ESC는 "지금 열려 있는 것을 닫는다"가 우선이다.
//   능력 패널이 열려 있는데 ESC로 일시정지가 겹쳐 뜨면 빠져나올 길이 헷갈린다.
public class PauseMenu : MonoBehaviour
{
    [Header("입력")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    [Tooltip("방을 못 깬 상태(문이 잠긴 전투 중)에는 일시정지를 열 수 없게 한다")]
    [SerializeField] private bool blockDuringCombat = true;
    [SerializeField] private string combatBlockMessage = "전투 중에는 일시정지할 수 없습니다";

    [Header("배치")]
    [SerializeField] private float itemHeight = 58f;
    [SerializeField] private float itemSpacing = 6f;
    [SerializeField] private float menuWidth = 420f;
    [Tooltip("메뉴 전체를 화면 왼쪽에서 얼마나 띄울지")]
    [SerializeField] private float leftMargin = 180f;
    [Tooltip("선택된 항목이 오른쪽으로 밀리는 거리")]
    [SerializeField] private float slideDistance = 12f;

    [Header("색")]
    [SerializeField] private Color backdropColor = new Color(0.02f, 0.02f, 0.04f, 0.86f);
    [SerializeField] private Color accentColor = new Color(0.93f, 0.83f, 0.56f);
    [SerializeField] private Color idleTextColor = new Color(0.60f, 0.61f, 0.66f);
    [SerializeField] private Color titleColor = new Color(0.93f, 0.83f, 0.56f);

    [Header("글자 크기")]
    [SerializeField] private int titleSize = 54;
    [SerializeField] private int itemSize = 30;

    [Header("폰트 (비우면 씬에서 가져옴)")]
    [SerializeField] private Font font;

    public bool IsOpen { get; private set; }

    private GameObject rootGo;
    private readonly List<MenuItemView> items = new List<MenuItemView>();
    private int index;
    private float previousTimeScale = 1f;

    private AbilityPanel abilityPanel;
    private AbilityCheatPanel cheatPanel;

    private void Start()
    {
        abilityPanel = FindObjectOfType<AbilityPanel>();
        cheatPanel = FindObjectOfType<AbilityCheatPanel>();

        BuildUI();
        Close();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            HandleEscape();
            return;
        }

        if (!IsOpen) return;

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(1);
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(-1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            items[index].Submit();
    }

    // ESC는 "열려 있는 것부터 닫는다". 그래야 어디서 눌러도 예측이 된다.
    private void HandleEscape()
    {
        // 결과 화면은 닫을 수 없다. 여기서 나가는 길은 재시작과 타이틀뿐이라
        // ESC로 일시정지가 겹쳐 뜨면 "게임이 계속되는 중"이라는 잘못된 신호를 준다.
        if (GameOverScreen.IsShowing) return;

        if (abilityPanel != null && abilityPanel.IsOpen) { abilityPanel.Close(); return; }
        if (cheatPanel != null && cheatPanel.IsOpen) { cheatPanel.Close(); return; }

        // 닫는 건 언제나 허용. 막는 건 "여는 것"뿐이다.
        if (IsOpen) { Close(); return; }

        if (blockDuringCombat && IsInCombat())
        {
            ToastManager.Show(combatBlockMessage, ToastManager.Kind.Warn);
            return;
        }

        Open();
    }

    private bool IsInCombat()
    {
        RoomManager rm = RoomManager.Instance;
        return rm != null && rm.Current != null && rm.Current.IsInCombat;
    }

    // ─────────────────────────────────────────────
    // 열기 / 닫기
    // ─────────────────────────────────────────────

    public void Open()
    {
        rootGo.SetActive(true);
        IsOpen = true;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        SetPlayerInputLocked(true);
        Select(0);
    }

    public void Close()
    {
        if (rootGo != null) rootGo.SetActive(false);
        IsOpen = false;

        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        SetPlayerInputLocked(false);
    }

    private void SetPlayerInputLocked(bool locked)
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;

        PlayerMove move = p.GetComponent<PlayerMove>();
        if (move != null) move.inputLocked = locked;
    }

    // ─────────────────────────────────────────────
    // 메뉴 동작
    // ─────────────────────────────────────────────

    private void Move(int delta)
    {
        if (items.Count == 0) return;
        Select((index + delta + items.Count) % items.Count);
    }

    private void Select(int i)
    {
        index = Mathf.Clamp(i, 0, items.Count - 1);
        for (int n = 0; n < items.Count; n++)
            items[n].SetSelected(n == index);
    }

    private void OnResume()
    {
        Close();
    }

    private void OnOptions()
    {
        // 옵션 시스템이 생기면 여기서 열면 된다.
        Debug.Log("[PauseMenu] 옵션 — 아직 준비되지 않음");
    }

    private void OnArcana()
    {
        // 능력 패널이 스스로 일시정지를 관리하므로 이쪽은 먼저 닫는다.
        // 둘 다 timeScale을 건드리면 닫는 순서에 따라 시간이 멈춘 채 남는다.
        Close();

        if (abilityPanel != null) abilityPanel.Open();
        else Debug.LogWarning("[PauseMenu] AbilityPanel을 찾을 수 없다", this);
    }

    private void OnQuit()
    {
        Time.timeScale = 1f;   // 종료 전에 되돌려 둔다

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
        GameObject canvasGo = new GameObject("PauseCanvas", typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;   // 능력 패널(200) 위, 치트(300) 아래

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        rootGo = UIFactory.Stretch("Root", canvasGo.transform).gameObject;

        Image backdrop = UIFactory.Panel("Backdrop", rootGo.transform, backdropColor);
        UIFactory.SetAnchoredBox(backdrop.rectTransform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);

        // 왼쪽에서 오른쪽으로 옅어지는 띠 — 메뉴 글자가 배경에서 뜨게 한다
        Image veil = UIFactory.Panel("Veil", rootGo.transform, new Color(0f, 0f, 0f, 0.55f), false);
        UIFactory.SetAnchoredBox(veil.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(menuWidth + leftMargin + 120f, 0f));

        BuildTitle();
        BuildItems();
    }

    private void BuildTitle()
    {
        Text title = UIFactory.Label("Title", rootGo.transform, font, titleSize,
            titleColor, TextAnchor.LowerLeft, FontStyle.Bold);
        UIFactory.SetAnchoredBox(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(leftMargin, 128f), new Vector2(leftMargin + menuWidth, 200f));
        title.text = "일시정지";

        Image rule = UIFactory.Panel("Rule", rootGo.transform, accentColor, false);
        UIFactory.SetAnchoredBox(rule.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(leftMargin, 116f), new Vector2(leftMargin + 96f, 119f));
    }

    private void BuildItems()
    {
        // (표시 이름, 눌렀을 때)
        var defs = new List<KeyValuePair<string, System.Action>>
        {
            new KeyValuePair<string, System.Action>("게임 재개", OnResume),
            new KeyValuePair<string, System.Action>("옵션",     OnOptions),
            new KeyValuePair<string, System.Action>("아르카나",  OnArcana),
            new KeyValuePair<string, System.Action>("나가기",    OnQuit),
        };

        float step = itemHeight + itemSpacing;
        float top = 84f;   // 제목 아래에서 시작

        for (int i = 0; i < defs.Count; i++)
        {
            RectTransform slot = UIFactory.Empty("Item_" + i, rootGo.transform);
            UIFactory.SetAnchoredBox(slot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(leftMargin, top - (i + 1) * step),
                new Vector2(leftMargin + menuWidth, top - i * step - itemSpacing));

            MenuItemView item = slot.gameObject.AddComponent<MenuItemView>();
            item.Build(defs[i].Key, font, itemSize, idleTextColor, accentColor, accentColor, slideDistance);

            int captured = i;
            System.Action action = defs[i].Value;
            item.onSelected = () => Select(captured);
            item.onSubmit = action;

            items.Add(item);
        }
    }
}
