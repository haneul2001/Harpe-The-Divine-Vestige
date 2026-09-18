using UnityEngine;

// 게임 화면에 뜨는 디버그 표시(공격 범위·히트박스 등)의 중앙 스위치.
//
// 규칙: 이 매니저가 씬에 없거나 꺼져 있으면 어떤 디버그 표시도 보이지 않는다.
//   빌드에 실수로 남더라도 매니저 오브젝트만 빼면 전부 사라진다.
//
// 표시하는 쪽은 DebugVisual을 상속해 등록만 하면 되고, 이 클래스는
// 무엇이 그려지는지 전혀 모른다.
public class DebugBoxManager : MonoBehaviour
{
    public static DebugBoxManager Instance { get; private set; }

    [Tooltip("디버그 표시 전체 on/off")]
    [SerializeField] private bool visible = true;

    [Tooltip("눌러서 켜고 끄는 키. None이면 키 토글 없음")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F2;

    [Tooltip("토글할 때 알림을 띄운다")]
    [SerializeField] private bool showToast = true;

    // 매니저가 없으면 무조건 숨김 — "존재해야 보인다"가 이 시스템의 약속이다
    public static bool Visible
    {
        get { return Instance != null && Instance.isActiveAndEnabled && Instance.visible; }
    }

    // 표시들이 구독한다. 매니저가 없어도 구독/해제가 가능하도록 static.
    public static event System.Action Changed;

    private bool lastApplied;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        lastApplied = Visible;
        Raise();
    }

    private void OnDisable()
    {
        // 꺼지는 순간에도 표시들이 따라 숨어야 한다
        Raise();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        Raise();
    }

    private void Update()
    {
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            SetVisible(!visible);

        // 인스펙터에서 체크박스를 직접 만졌을 때도 반영
        if (lastApplied != Visible)
        {
            lastApplied = Visible;
            Raise();
        }
    }

    public void SetVisible(bool value)
    {
        if (visible == value) return;

        visible = value;
        lastApplied = Visible;
        Raise();

        if (showToast)
            ToastManager.Show(value ? "디버그 표시 켬" : "디버그 표시 끔");
    }

    private static void Raise()
    {
        if (Changed != null) Changed();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) Raise();
    }
#endif
}
