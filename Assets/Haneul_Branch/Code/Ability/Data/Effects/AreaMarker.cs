using UnityEngine;

// 게임 화면에 잠깐 뜨는 반투명 원판. 적 프리팹의 AttackRange 표시와 같은 역할이다.
//
// 범위 공격의 사거리를 눈으로 확인하는 용도이자, 이펙트가 준비되기 전의 임시 연출.
// 스프라이트를 코드로 만들어 쓰므로 에셋을 미리 준비할 필요가 없다.
public class AreaMarker : MonoBehaviour
{
    private static Sprite circleSprite;

    private SpriteRenderer sr;
    private float duration;
    private float elapsed;
    private Color baseColor;
    private bool fadeOut;

    public static AreaMarker Show(Vector3 position, float radius, Color color,
        float duration = 1.5f, bool fadeOut = true, float squash = 0.6f)
    {
        var go = new GameObject("AreaMarker");
        go.transform.position = position;
        // 탑다운이라 정원으로 그리면 공중에 떠 보인다. 세로를 눌러 바닥에 깔린 느낌을 낸다.
        go.transform.localScale = new Vector3(radius * 2f, radius * 2f * squash, 1f);

        var marker = go.AddComponent<AreaMarker>();
        marker.Init(color, duration, fadeOut);
        return marker;
    }

    private void Init(Color color, float duration, bool fadeOut)
    {
        this.baseColor = color;
        this.duration = duration;
        this.fadeOut = fadeOut;

        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = GetCircleSprite();
        sr.color = color;
        sr.sortingLayerName = "Skill";   // 바닥·적보다 위
        sr.sortingOrder = 40;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        if (duration > 0f && elapsed >= duration)
        {
            Destroy(gameObject);
            return;
        }

        if (fadeOut && duration > 0f)
        {
            float k = 1f - (elapsed / duration);
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * k);
        }
    }

    // 지름 1유닛짜리 원판 스프라이트를 한 번만 만들어 공유한다.
    // 가장자리만 살짝 흐리게 해서 계단이 안 보이게 한다.
    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r;
                float dy = (y + 0.5f - r) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                // 안쪽은 옅게 채우고 테두리 근처를 진하게 — 범위가 어디까지인지 잘 보인다
                float fill = Mathf.Clamp01((1f - d) / 0.04f);      // 바깥 경계 부드럽게
                float edge = Mathf.Clamp01(1f - Mathf.Abs(d - 0.94f) / 0.06f);
                float a = Mathf.Clamp01(fill * 0.35f + edge * 0.9f);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return circleSprite;
    }
}
