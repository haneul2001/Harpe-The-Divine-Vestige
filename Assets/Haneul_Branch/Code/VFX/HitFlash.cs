using UnityEngine;

// 맞는 순간 스프라이트가 한 색으로 번쩍였다 사라진다.
//
// 본체 머티리얼을 갈아 끼우지 않고 같은 그림을 그리는 자식을 하나 겹쳐 둔다 —
// 본체는 2D 조명(Sprite-Lit)을 받아야 하고, 아웃라인 같은 다른 머티리얼 연출과도 부딪히지 않는다.
// 애니메이션으로 그림이 바뀌므로 매 프레임 본체의 현재 그림을 따라간다.
[DisallowMultipleComponent]
public class HitFlash : MonoBehaviour
{
    [SerializeField] private Color color = new Color(1f, 0.55f, 0.15f, 1f);
    [Tooltip("번쩍이는 시간(초). 짧을수록 타격감이 날카롭다")]
    [SerializeField] private float duration = 0.12f;
    [Tooltip("맞은 직후 세기 (0~1). 1이면 한 프레임 동안 완전히 주황색")]
    [Range(0f, 1f)] [SerializeField] private float peak = 0.6f;

    private const string MaterialPath = "VFX/M_HitFlash";
    private static Material sharedMaterial;

    private SpriteRenderer source;
    private SpriteRenderer overlay;
    private float remaining;

    // 코드에서 붙일 때 쓰는 입구 — 이미 있으면 그걸 돌려준다
    public static HitFlash For(SpriteRenderer target)
    {
        if (target == null) return null;
        HitFlash f = target.GetComponent<HitFlash>();
        if (f == null) f = target.gameObject.AddComponent<HitFlash>();
        return f;
    }

    public void Flash()
    {
        if (!EnsureOverlay()) return;
        remaining = duration;
        overlay.enabled = true;
        Sync(1f);
    }

    private bool EnsureOverlay()
    {
        if (overlay != null) return true;

        if (source == null) source = GetComponent<SpriteRenderer>();
        if (source == null) return false;

        if (sharedMaterial == null) sharedMaterial = Resources.Load<Material>(MaterialPath);
        if (sharedMaterial == null)
        {
            Debug.LogWarning("[HitFlash] Resources/" + MaterialPath + " 머티리얼이 없다", this);
            return false;
        }

        var go = new GameObject("HitFlash");
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;

        overlay = go.AddComponent<SpriteRenderer>();
        overlay.sharedMaterial = sharedMaterial;
        overlay.enabled = false;
        return true;
    }

    private void LateUpdate()
    {
        if (overlay == null || !overlay.enabled) return;

        // 에셋 팩(RafaelMatos) 피격 모션은 첫 프레임(hurt_0)이 새하얗게 그려져 있다.
        // 그 프레임이 떠 있는 동안은 세기를 안 줄이고 유지한다 — 안 그러면 주황 → 흰색으로 두 번 번쩍인다
        if (IsBakedWhiteFrame(source.sprite))
        {
            remaining = duration;
            Sync(1f);
            return;
        }

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            overlay.enabled = false;
            return;
        }

        Sync(remaining / duration);
    }

    // k: 1(방금 맞음) → 0(끝). 끝으로 갈수록 빠르게 빠져야 "번쩍"으로 읽힌다
    private void Sync(float k)
    {
        overlay.sprite = source.sprite;
        overlay.flipX = source.flipX;
        overlay.flipY = source.flipY;
        overlay.sortingLayerID = source.sortingLayerID;
        overlay.sortingOrder = source.sortingOrder + 1;
        overlay.color = new Color(color.r, color.g, color.b, peak * k * k * source.color.a);
    }

    private static readonly System.Collections.Generic.Dictionary<Sprite, bool> bakedWhite =
        new System.Collections.Generic.Dictionary<Sprite, bool>();

    private static bool IsBakedWhiteFrame(Sprite sprite)
    {
        if (sprite == null) return false;

        bool white;
        if (!bakedWhite.TryGetValue(sprite, out white))
        {
            string n = sprite.name.ToLowerInvariant();
            white = n.Contains("hurt") && n.EndsWith("_0");
            bakedWhite[sprite] = white;
        }
        return white;
    }

    private void OnDisable()
    {
        if (overlay != null) overlay.enabled = false;
    }
}
