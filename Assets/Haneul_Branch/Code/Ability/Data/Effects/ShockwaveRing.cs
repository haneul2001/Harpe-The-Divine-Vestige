using UnityEngine;

// 퍼져 나가는 원형 링. 그림 에셋 없이 LineRenderer로 그린다.
//
// 이펙트 프리팹이 준비되기 전까지의 임시 연출이자, 반경이 실제로 어디까지인지
// 눈으로 확인하는 디버그 수단을 겸한다. vfxPrefab을 꽂으면 이건 안 써도 된다.
[RequireComponent(typeof(LineRenderer))]
public class ShockwaveRing : MonoBehaviour
{
    private LineRenderer line;
    private float radius;
    private float duration;
    private float elapsed;
    private Color color;
    private float startWidth;

    // 어디서든 한 줄로 띄울 수 있게
    public static ShockwaveRing Spawn(Vector3 position, float radius, Color color,
        float duration = 0.35f, int segments = 48, float width = 0.12f)
    {
        var go = new GameObject("ShockwaveRing");
        go.transform.position = position;

        var ring = go.AddComponent<ShockwaveRing>();
        ring.Init(radius, color, duration, segments, width);
        return ring;
    }

    private void Init(float radius, Color color, float duration, int segments, float width)
    {
        this.radius = radius;
        this.color = color;
        this.duration = Mathf.Max(0.01f, duration);
        this.startWidth = width;

        line = GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segments;
        line.numCapVertices = 2;

        // 셰이더를 못 찾으면 선이 분홍색으로 뜬다 — URP/빌트인 양쪽을 시도
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        line.material = new Material(shader);

        line.sortingLayerName = "Skill";   // 적/바닥보다 위에
        line.sortingOrder = 50;

        // 원 모양은 한 번만 만들고, 이후엔 스케일로 키운다 (매 프레임 정점 재계산 방지)
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.6f, 0f));
        }

        Apply(0f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        Apply(t);
    }

    private void Apply(float t)
    {
        // 빠르게 퍼졌다 끝에서 느려진다
        float eased = 1f - (1f - t) * (1f - t);

        transform.localScale = Vector3.one * (radius * Mathf.Max(0.02f, eased));

        float fade = 1f - t;
        Color c = new Color(color.r, color.g, color.b, color.a * fade);
        line.startColor = c;
        line.endColor = c;

        // 커질수록 선이 같이 굵어지면 부담스러우므로 반대로 얇아지게
        float w = startWidth * (1.2f - eased * 0.7f);
        line.startWidth = w;
        line.endWidth = w;
    }
}
