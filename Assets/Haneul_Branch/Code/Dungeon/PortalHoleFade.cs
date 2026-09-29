using UnityEngine;

// 포털 가운데 어두운 구멍 — 소용돌이가 모여드는 동안은 안 보이다가 서서히 드러난다.
[RequireComponent(typeof(SpriteRenderer))]
public class PortalHoleFade : MonoBehaviour
{
    [Tooltip("나타나기 시작하는 시간(초) — 소용돌이 등장 프레임이 거의 끝날 때")]
    public float delay = 0.3f;
    [Tooltip("다 드러나는 데 걸리는 시간(초)")]
    public float duration = 0.4f;

    private SpriteRenderer sr;
    private float t;

    private void Awake() { sr = GetComponent<SpriteRenderer>(); }

    private void OnEnable() { t = 0f; Apply(); }

    private void Update()
    {
        if (t > delay + duration) return;
        t += Time.deltaTime;
        Apply();
    }

    private void Apply()
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = Mathf.Clamp01((t - delay) / Mathf.Max(0.01f, duration));
        sr.color = c;
    }
}
