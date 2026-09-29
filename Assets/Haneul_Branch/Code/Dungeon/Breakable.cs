using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 때려서 부수는 소품(나무 상자 등).
//
//  · 맞으면 번쩍이고 흔들리며 피해 숫자가 뜬다
//  · 체력이 0이 되면 부서진 그림으로 바뀌고 나무 조각이 튀며, 골드를 떨어뜨린다
//  · 부서진 잔해는 바닥 장식으로 남는다 (통과 가능)
//
// 팩에 상자 부서짐 애니메이션이 없어서, "crate N" ↔ "crate - broken - N" 그림 쌍과
// 원래 그림에서 떼어 낸 작은 조각들로 연출한다.
// 플레이어 공격은 적(Enemy)만 찾으므로, 공격 코드가 HitBox/HitCircle을 따로 불러 준다.
[RequireComponent(typeof(SpriteRenderer))]
public class Breakable : MonoBehaviour
{
    [Tooltip("체력")]
    public int maxHp = 100;
    [Tooltip("부서진 뒤 그림 (비우면 사라진다)")]
    public Sprite brokenSprite;
    [Tooltip("부서질 때 떨어뜨리는 골드 합계 범위")]
    public int goldMin = 5;
    public int goldMax = 10;

    [Header("연출")]
    [Tooltip("맞았을 때 흔들리는 폭(월드 단위)")]
    public float shakeAmount = 0.06f;
    public float shakeTime = 0.12f;
    [Tooltip("튀는 나무 조각 수")]
    public int debrisCount = 7;

    private static readonly List<Breakable> all = new List<Breakable>();

    private SpriteRenderer sr;
    private Collider2D body;
    private HitFlash flash;
    private int hp;
    private bool broken;
    private Vector3 restPos;

    public bool IsBroken => broken;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        body = GetComponent<Collider2D>();
        hp = maxHp;
        restPos = transform.localPosition;
    }

    private void OnEnable() { all.Add(this); }
    private void OnDisable() { all.Remove(this); }

    // ─────────────────────────────────────────────
    // 공격 코드가 부르는 판정 — 적 판정과 같은 모양·크기로 소품을 한 번 더 훑는다
    // ─────────────────────────────────────────────

    public static int HitBox(Vector2 center, Vector2 size, float angleDeg, int damage, bool crit, HashSet<Breakable> once = null)
    {
        int n = 0;
        Quaternion inv = Quaternion.Euler(0f, 0f, -angleDeg);
        Vector2 half = size * 0.5f;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            Breakable b = all[i];
            if (b == null || b.broken || b.body == null) continue;
            // 상자 그림 범위의 가장 가까운 점이 회전된 공격 상자 안에 있는가
            // (몸 충돌은 밑동 한 칸뿐이라, 맞는 판정은 보이는 그림 전체로 잡는다)
            Vector2 p = b.sr.bounds.ClosestPoint(center);
            Vector2 local = inv * (p - center);
            if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y) continue;
            if (once != null && !once.Add(b)) continue;
            b.TakeHit(damage, crit);
            n++;
        }
        return n;
    }

    public static int HitCircle(Vector2 center, float radius, int damage, bool crit, HashSet<Breakable> once = null)
    {
        int n = 0;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            Breakable b = all[i];
            if (b == null || b.broken || b.body == null) continue;
            if (Vector2.Distance(b.sr.bounds.ClosestPoint(center), center) > radius) continue;
            if (once != null && !once.Add(b)) continue;
            b.TakeHit(damage, crit);
            n++;
        }
        return n;
    }

    // ─────────────────────────────────────────────

    public void TakeHit(int damage, bool crit)
    {
        if (broken) return;
        damage = Mathf.Max(1, damage);
        hp -= damage;

        if (DamageNumberSpawner.Instance != null)
            DamageNumberSpawner.Instance.Show(transform.position + Vector3.up * 0.5f, damage, crit);
        if (flash == null) flash = HitFlash.For(sr);
        if (flash != null) flash.Flash();

        if (hp <= 0) Break();
        else { StopAllCoroutines(); StartCoroutine(Shake()); }
    }

    private IEnumerator Shake()
    {
        float t = 0f;
        while (t < shakeTime)
        {
            t += Time.deltaTime;
            float k = 1f - t / shakeTime;
            transform.localPosition = restPos + new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * shakeAmount * k;
            yield return null;
        }
        transform.localPosition = restPos;
    }

    private void Break()
    {
        broken = true;
        StopAllCoroutines();
        transform.localPosition = restPos;

        SpawnDebris();

        // 잔해는 밟고 지나가는 바닥 장식으로 남긴다
        if (body != null) body.enabled = false;
        if (brokenSprite != null)
        {
            sr.sprite = brokenSprite;
            sr.sortingOrder -= 1;
        }
        else sr.enabled = false;

        if (GoldDropper.Instance != null)
            GoldDropper.Instance.DropTotal(transform.position, Random.Range(goldMin, goldMax + 1));
    }

    // 원래 그림에서 3~5px 조각을 떼어 사방으로 튀긴다
    private void SpawnDebris()
    {
        Sprite src = sr.sprite;
        if (src == null || debrisCount <= 0) return;
        Rect r = src.textureRect;
        float ppu = src.pixelsPerUnit;
        for (int i = 0; i < debrisCount; i++)
        {
            int w = Random.Range(3, 6), h = Random.Range(3, 6);
            float x = Random.Range(r.xMin, r.xMax - w), y = Random.Range(r.yMin + r.height * 0.2f, r.yMax - h);
            Sprite piece = Sprite.Create(src.texture, new Rect(Mathf.Floor(x), Mathf.Floor(y), w, h), new Vector2(0.5f, 0.5f), ppu);
            var go = new GameObject("Debris");
            go.transform.position = transform.position + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.2f, 0.7f), 0f);
            var psr = go.AddComponent<SpriteRenderer>();
            psr.sprite = piece;
            // 발밑 정렬 묶음(SortingGroup) 안에 있으면 자식 그림의 층은 의미가 없다 — 묶음의 층을 따른다
            var sg = GetComponentInParent<UnityEngine.Rendering.SortingGroup>();
            psr.sortingLayerID = sg != null ? sg.sortingLayerID : sr.sortingLayerID;
            psr.sortingOrder = (sg != null ? sg.sortingOrder : sr.sortingOrder) + 1;
            go.AddComponent<BreakableDebris>().Launch(Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.2f));
        }
    }
}
