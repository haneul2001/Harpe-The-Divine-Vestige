using System.Collections;
using UnityEngine;

// 문의 "잠김" 그래픽으로 쓰는 게이트. 프레임을 직접 넘기므로 Animator 컨트롤러가 필요 없다.
//   잠김  → 열림 프레임을 거꾸로 틀어 창살이 올라오거나(raiseAnimated), 닫힌 그림으로 바로 바꾼다
//   풀림  → 닫힘→열림 프레임을 튼 뒤, 열린 모습이 있는 문(감옥 문, 내려간 창살 자국)은 그대로 남기고 아니면 숨긴다
// Door.SetLocked가 부른다.
[RequireComponent(typeof(SpriteRenderer))]
public class DoorGateVisual : MonoBehaviour
{
    [Tooltip("닫힘 → 열림 순서의 프레임")]
    public Sprite[] openFrames;
    [Tooltip("초당 프레임")]
    public float fps = 10f;
    [Tooltip("잠길 때 열림 프레임을 거꾸로 틀어 올라오는 연출을 한다. 끄면 닫힌 그림으로 바로 바뀐다")]
    public bool raiseAnimated = true;
    [Tooltip("열린 뒤에도 마지막 프레임을 남겨 둔다 (열린 문·내려간 창살). 끄면 다 열리고 숨는다")]
    public bool stayWhenOpen = true;

    private SpriteRenderer sr;

    private void Awake() { sr = GetComponent<SpriteRenderer>(); }

    private bool HasFrames => openFrames != null && openFrames.Length > 0;
    private Sprite Closed => HasFrames ? openFrames[0] : null;
    private Sprite Opened => HasFrames ? openFrames[openFrames.Length - 1] : null;
    private float Duration => HasFrames ? openFrames.Length / Mathf.Max(1f, fps) : 0f;

    // 잠김. 즉시 통행이 막히므로 연출과 무관하게 바로 보인다
    public void Lock()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        gameObject.SetActive(true);
        StopAllCoroutines();
        if (raiseAnimated && gameObject.activeInHierarchy && openFrames != null && openFrames.Length > 1)
            StartCoroutine(Play(reverse: true, hideAfter: false));
        else
            sr.sprite = Closed;
    }

    // 풀림. 연출 시간을 돌려주면 Door가 그만큼 뒤에 통행을 튼다. animate=false면 바로 열린 상태로 둔다
    public float Unlock(bool animate)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        StopAllCoroutines();
        if (!animate || !gameObject.activeInHierarchy || openFrames == null || openFrames.Length < 2)
        {
            if (stayWhenOpen) { gameObject.SetActive(true); sr.sprite = Opened; }
            else gameObject.SetActive(false);
            return 0f;
        }
        gameObject.SetActive(true);
        StartCoroutine(Play(reverse: false, hideAfter: !stayWhenOpen));
        return Duration;
    }

    // 벽으로 막힌 문 등 — 아예 안 보이게
    public void Hide()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    private IEnumerator Play(bool reverse, bool hideAfter)
    {
        float step = 1f / Mathf.Max(1f, fps);
        int n = openFrames.Length;
        for (int i = 0; i < n; i++)
        {
            sr.sprite = openFrames[reverse ? n - 1 - i : i];
            yield return new WaitForSeconds(step);
        }
        if (hideAfter) gameObject.SetActive(false);
    }
}
