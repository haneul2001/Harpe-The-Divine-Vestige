using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 던전 입장 연출.
//   검은 화면에서 시작 → 층 이름이 떠오름 → 이름이 떠 있는 동안 화면이 밝아짐 → 이름이 걷힘
// 하데스/아이작이 층 진입에서 쓰는 순서를 그대로 따랐다.
//
// 같은 배너를 보스 방 첫 입장에도 재사용한다.
//
// 씬 어디든 빈 오브젝트에 붙이고 FadeImage / 층 이름 UI만 연결하면 된다.
// 던전 생성이 끝날 때까지(RoomManager.Current가 생길 때까지) 검은 화면을 유지하므로
// 스크립트 실행 순서에 의존하지 않는다.
public class DungeonIntro : MonoBehaviour
{
    [Header("참조 (비우면 자동 탐색)")]
    [Tooltip("화면 전체를 덮는 검은 Image")]
    [SerializeField] private Image fadeImage;
    [Tooltip("층 이름 묶음. 알파를 여기서 한 번에 조절한다")]
    [SerializeField] private CanvasGroup titleGroup;
    [SerializeField] private Text titleLabel;
    [SerializeField] private Text subLabel;
    [Tooltip("연출 동안 숨길 캔버스(미니맵 등). 비우면 'MinimapCanvas'를 찾는다.\n" +
             "미니맵은 sortingOrder가 높아 페이드 위에 그려지므로 따로 꺼 줘야 한다.")]
    [SerializeField] private Canvas hideDuringIntro;

    [Header("층 정보")]
    [SerializeField] private string floorName = "지하 묘지";
    [SerializeField] private string floorSub = "1층";

    [Header("타이밍")]
    [Tooltip("던전 생성이 끝난 뒤 검은 화면을 그대로 두는 시간")]
    [SerializeField] private float holdBlack = 0.35f;
    [Tooltip("층 이름이 떠오르는 시간")]
    [SerializeField] private float titleFadeIn = 0.5f;
    [Tooltip("층 이름이 떠 있는 시간")]
    [SerializeField] private float titleHold = 1.2f;
    [Tooltip("층 이름이 사라지는 시간")]
    [SerializeField] private float titleFadeOut = 0.45f;
    [Tooltip("검은 화면이 걷히는 시간. 층 이름이 떠 있는 동안 같이 진행된다")]
    [SerializeField] private float screenFadeOut = 0.8f;
    [Tooltip("층을 넘어갈 때 화면이 덮이는 시간")]
    [SerializeField] private float floorFadeIn = 0.5f;

    [Header("보스 방 배너")]
    [SerializeField] private bool showBossBanner = true;
    [SerializeField] private string bossTitle = "보스";
    [SerializeField] private string bossSub = "";
    [SerializeField] private float bossBannerHold = 1f;
    [Tooltip("보스 방 입장 시 카메라 흔들림. 0이면 없음")]
    [SerializeField] private float bossShake = 0.35f;

    private bool bossBannerShown;
    private RoomManager subscribed;

    private void Awake()
    {
        ResolveRefs();

        // 첫 프레임부터 검게. 여기서 안 덮으면 던전이 만들어지는 한두 프레임이 그대로 보인다.
        SetScreenAlpha(1f);
        SetGroupAlpha(0f);
        SetHiddenCanvas(true);

        // 페이드는 다른 UI보다 뒤에 그려져야 화면 전체를 덮는다.
        if (fadeImage != null)
            fadeImage.transform.SetAsLastSibling();

        // 층 이름은 페이드보다도 위에 떠야 한다.
        if (titleGroup != null)
            titleGroup.transform.SetAsLastSibling();

        // 던전이 만들어지기 전에도 조작은 막아 둔다.
        // RoomManager를 기다렸다 잠그면 그 사이 몇 프레임을 검은 화면 뒤에서 걸어 다닐 수 있다.
        SetPlayerLocked(true);
    }

    // RoomManager가 아직 없을 수도 있어(생성 순서) 플레이어를 직접 찾는 경로도 둔다.
    private void SetPlayerLocked(bool locked)
    {
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.SetPlayerInputLocked(locked);
            return;
        }

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;

        PlayerMove move = p.GetComponent<PlayerMove>();
        if (move != null) move.inputLocked = locked;
    }

    private void ResolveRefs()
    {
        if (fadeImage == null)
        {
            GameObject found = GameObject.Find("FadeImage");
            if (found != null) fadeImage = found.GetComponent<Image>();
        }

        if (titleGroup != null)
        {
            if (titleLabel == null || subLabel == null)
            {
                Text[] texts = titleGroup.GetComponentsInChildren<Text>(true);
                if (titleLabel == null && texts.Length > 0) titleLabel = texts[0];
                if (subLabel == null && texts.Length > 1) subLabel = texts[1];
            }
        }

        if (hideDuringIntro == null)
        {
            GameObject mm = GameObject.Find("MinimapCanvas");
            if (mm != null) hideDuringIntro = mm.GetComponent<Canvas>();
        }

        if (fadeImage == null)
            Debug.LogWarning("[DungeonIntro] FadeImage를 찾지 못함 — 화면 페이드 생략", this);
    }

    // 오브젝트가 아니라 Canvas 컴포넌트만 끈다.
    // 오브젝트를 끄면 미니맵의 Start가 밀려서 생성/구독 순서가 꼬인다.
    private void SetHiddenCanvas(bool hidden)
    {
        if (hideDuringIntro != null) hideDuringIntro.enabled = !hidden;
    }

    private void Start()
    {
        StartCoroutine(PlayIntro());
    }

    // 생성기가 이번 층 이름을 알려 준다. 연출은 던전이 다 지어진 뒤에 글자를 읽으므로
    // 생성 전에 넣어 두면 실행 순서를 신경 쓸 필요가 없다.
    public void SetFloorInfo(string name, string sub)
    {
        if (!string.IsNullOrEmpty(name)) floorName = name;
        if (sub != null) floorSub = sub;
    }

    // 층 이동. 화면을 덮고, 덮인 동안 던전을 갈아엎고, 새 층 이름으로 다시 연다.
    // 던전을 부수는 작업(whileBlack)을 화면이 덮인 뒤에 하는 게 핵심이다 —
    // 방이 사라지고 다시 생기는 걸 그대로 보여 주면 순간이동한 것처럼 보인다.
    public Coroutine PlayFloorTransition(string name, string sub, System.Action whileBlack)
    {
        return StartCoroutine(FloorTransition(name, sub, whileBlack));
    }

    private IEnumerator FloorTransition(string name, string sub, System.Action whileBlack)
    {
        SetPlayerLocked(true);
        SetHiddenCanvas(true);

        yield return FadeScreen(0f, 1f, floorFadeIn);

        if (whileBlack != null) whileBlack();

        // 새 던전이 다 서고 시작 방에 들어갈 때까지 검은 화면을 유지한다
        while (RoomManager.Instance == null || RoomManager.Instance.Current == null)
            yield return null;

        bossBannerShown = false;   // 새 층의 보스에게 배너를 다시 준다
        SetFloorInfo(name, sub);

        if (holdBlack > 0f) yield return new WaitForSeconds(holdBlack);

        SetTitle(floorName, floorSub);
        yield return FadeGroup(0f, 1f, titleFadeIn);

        StartCoroutine(FadeScreen(1f, 0f, screenFadeOut));

        if (titleHold > 0f) yield return new WaitForSeconds(titleHold);

        SetHiddenCanvas(false);
        yield return FadeGroup(1f, 0f, titleFadeOut);

        SetPlayerLocked(false);
    }

    private void OnDestroy()
    {
        if (subscribed != null)
            subscribed.OnRoomChanged -= HandleRoomChanged;
    }

    private IEnumerator PlayIntro()
    {
        // 던전이 다 만들어지고 시작 방에 들어갈 때까지 검은 화면을 유지한다.
        while (RoomManager.Instance == null || RoomManager.Instance.Current == null)
            yield return null;

        RoomManager rm = RoomManager.Instance;
        SetPlayerLocked(true);

        subscribed = rm;
        rm.OnRoomChanged += HandleRoomChanged;

        if (holdBlack > 0f) yield return new WaitForSeconds(holdBlack);

        SetTitle(floorName, floorSub);
        yield return FadeGroup(0f, 1f, titleFadeIn);

        // 이름이 떠 있는 동안 화면이 밝아진다 — 이름만 먼저 보여 주고 끝나면 늘어진다.
        StartCoroutine(FadeScreen(1f, 0f, screenFadeOut));

        if (titleHold > 0f) yield return new WaitForSeconds(titleHold);

        // 화면이 다 밝아진 시점(screenFadeOut < titleHold)에 지도를 되돌린다
        SetHiddenCanvas(false);

        yield return FadeGroup(1f, 0f, titleFadeOut);

        SetPlayerLocked(false);
    }

    private void HandleRoomChanged(Room room)
    {
        if (!showBossBanner || room == null) return;
        if (room.type != RoomType.Boss) return;
        if (bossBannerShown) return;

        bossBannerShown = true;
        StartCoroutine(PlayBossBanner());
    }

    private IEnumerator PlayBossBanner()
    {
        SetTitle(bossTitle, bossSub);

        if (bossShake > 0f) CameraShake.Shake(bossShake);

        yield return FadeGroup(0f, 1f, 0.25f);
        if (bossBannerHold > 0f) yield return new WaitForSeconds(bossBannerHold);
        yield return FadeGroup(1f, 0f, 0.35f);
    }

    // ─────────────────────────────────────────────
    // 보조
    // ─────────────────────────────────────────────

    private IEnumerator FadeScreen(float from, float to, float duration)
    {
        if (fadeImage == null || duration <= 0f)
        {
            SetScreenAlpha(to);
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            SetScreenAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t))));
            yield return null;
        }
        SetScreenAlpha(to);
    }

    private IEnumerator FadeGroup(float from, float to, float duration)
    {
        if (titleGroup == null || duration <= 0f)
        {
            SetGroupAlpha(to);
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            SetGroupAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t))));
            yield return null;
        }
        SetGroupAlpha(to);
    }

    private void SetScreenAlpha(float a)
    {
        if (fadeImage == null) return;

        Color c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;

        // 완전히 투명하면 굳이 그리지 않는다
        fadeImage.enabled = a > 0.001f;
    }

    private void SetGroupAlpha(float a)
    {
        if (titleGroup == null) return;
        titleGroup.alpha = a;
    }

    private void SetTitle(string main, string sub)
    {
        if (titleLabel != null) titleLabel.text = main;

        if (subLabel != null)
        {
            subLabel.text = sub;
            subLabel.gameObject.SetActive(!string.IsNullOrEmpty(sub));
        }
    }
}
