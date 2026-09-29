using UnityEngine;

// 부서진 조각 하나 — 튀어 올랐다 떨어지며 사라진다 (탑다운이라 높이는 가짜 y로 흉내)
public class BreakableDebris : MonoBehaviour
{
    private Vector2 vel;
    private float height, vHeight, life, spin;
    private Vector3 ground;
    private SpriteRenderer sr;

    public void Launch(Vector2 velocity)
    {
        vel = velocity;
        vHeight = Random.Range(2.5f, 4f);
        spin = Random.Range(-540f, 540f);
        life = Random.Range(0.45f, 0.7f);
        ground = transform.position;
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        ground += (Vector3)(vel * dt);
        vel *= 1f - 3f * dt;
        vHeight -= 14f * dt;
        height = Mathf.Max(0f, height + vHeight * dt);
        transform.position = ground + Vector3.up * height;
        transform.Rotate(0f, 0f, spin * dt);
        life -= dt;
        if (sr != null && life < 0.25f) { Color c = sr.color; c.a = Mathf.Clamp01(life / 0.25f); sr.color = c; }
        if (life <= 0f) Destroy(gameObject);
    }

    // 조각마다 Sprite.Create로 만든 그림이라 같이 치운다
    private void OnDestroy()
    {
        if (sr != null && sr.sprite != null) Destroy(sr.sprite);
    }
}
