using UnityEngine;

// 등장 프레임을 한 번 튼 뒤 반복 프레임을 계속 돈다 (다음 층 소용돌이 포털 등).
// Animator 컨트롤러 없이 스프라이트만 넘긴다.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteLoop : MonoBehaviour
{
    [Tooltip("처음 한 번만 트는 프레임 (비워도 된다)")]
    public Sprite[] intro = new Sprite[0];
    [Tooltip("그 뒤로 계속 도는 프레임")]
    public Sprite[] loop = new Sprite[0];
    [Min(1f)] public float fps = 10f;

    private SpriteRenderer sr;
    private float t;

    private void Awake() { sr = GetComponent<SpriteRenderer>(); }

    private void OnEnable() { t = 0f; Apply(); }

    private void Update()
    {
        t += Time.deltaTime;
        Apply();
    }

    private void Apply()
    {
        if (sr == null) return;
        int f = Mathf.FloorToInt(t * fps);
        int n = intro != null ? intro.Length : 0;
        if (f < n) { sr.sprite = intro[f]; return; }
        if (loop == null || loop.Length == 0) return;
        sr.sprite = loop[(f - n) % loop.Length];
    }
}
