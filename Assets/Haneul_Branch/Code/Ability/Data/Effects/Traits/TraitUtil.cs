using UnityEngine;

// 특성 효과들이 같이 쓰는 작은 도우미
public static class TraitUtil
{
    public static void Text(Vector3 pos, string text)
    {
        if (DamageNumberSpawner.Instance != null) DamageNumberSpawner.Instance.ShowText(pos + Vector3.up * 0.9f, text);
    }

    public static void Explosion(GameObject prefab, Vector3 pos, float scale, float lifetime = 1.2f)
    {
        VfxPrefab.Spawn(prefab, pos, 0f, scale, null, null, lifetime);
    }

    // 최근 시간 목록에서 window보다 오래된 것을 버린다
    public static void Trim(System.Collections.Generic.List<float> times, float window)
    {
        float cut = Time.time - window;
        times.RemoveAll(t => t < cut);
    }

    // 적을 from 반대쪽으로 짧게 밀어낸다. 벽에 막히면 거기서 멈춘다 (적에게는 넉백 기능이 따로 없다)
    public static void Push(AbilityContext ctx, Enemy enemy, Vector3 from, float distance, float duration)
    {
        if (ctx.runner == null || enemy == null) return;
        ctx.runner.StartCoroutine(PushRoutine(enemy, from, distance, duration));
    }

    private static System.Collections.IEnumerator PushRoutine(Enemy enemy, Vector3 from, float distance, float duration)
    {
        var rb = enemy.GetComponent<Rigidbody2D>();
        Vector2 dir = (Vector2)(enemy.transform.position - from);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();
        int wall = LayerMask.GetMask("Wall");

        float moved = 0f, speed = distance / Mathf.Max(0.01f, duration);
        while (moved < distance && enemy != null && !enemy.isDead)
        {
            float step = Mathf.Min(speed * Time.deltaTime, distance - moved);
            Vector2 pos = rb != null ? rb.position : (Vector2)enemy.transform.position;
            if (Physics2D.Raycast(pos, dir, step + 0.3f, wall).collider != null) yield break;
            if (rb != null) rb.position = pos + dir * step;
            else enemy.transform.position = pos + dir * step;
            moved += step;
            yield return null;
        }
    }
}
