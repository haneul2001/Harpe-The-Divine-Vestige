using UnityEngine;

// 이펙트 하나를 재생하는 오브젝트. 프레임을 직접 넘겨 그리므로 애니메이터가 필요 없다.
//
//   PixelVfx.Play("EnemyHit", pos);                 // 한 번 재생하고 스스로 사라진다
//   var v = PixelVfx.Play("BatShot", pos, ang, tr); // 루프 이펙트 — 들고 있다가 직접 없앤다
//
// 정의는 Resources/VFX/VfxLibrary 에 모여 있다. 새 이펙트를 추가할 때 이 파일은 안 건드린다.
[RequireComponent(typeof(SpriteRenderer))]
public class PixelVfx : MonoBehaviour
{
    private static VfxLibrary library;
    private static bool libraryMissingLogged;

    private SpriteRenderer sr;
    private VfxClip clip;
    private float t;
    private bool playing;

    public bool IsPlaying { get { return playing; } }

    // 이펙트를 하나 띄운다. 정의를 못 찾으면 아무것도 안 하고 null을 돌려준다.
    public static PixelVfx Play(string id, Vector3 position, float rotationZ = 0f, Transform parent = null)
    {
        VfxClip c = Lookup(id);
        if (c == null || c.frames == null || c.frames.Length == 0) return null;

        var go = new GameObject("VFX_" + id);
        go.transform.SetParent(parent, false);
        // 부모에 붙여도 월드 좌표는 지정한 자리에 오게 한다
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);

        // 부모가 커져 있으면(적은 3배로 스폰된다) 이펙트까지 같이 커진다.
        // 화면에 보이는 크기가 정의값 그대로가 되도록 부모 배율을 나눠 준다.
        float s = Mathf.Max(0.01f, c.scale);
        Vector3 pl = parent != null ? parent.lossyScale : Vector3.one;
        go.transform.localScale = new Vector3(
            s / Mathf.Max(0.0001f, Mathf.Abs(pl.x)),
            s / Mathf.Max(0.0001f, Mathf.Abs(pl.y)),
            1f);

        var vfx = go.AddComponent<PixelVfx>();
        vfx.Begin(c);
        return vfx;
    }

    private static VfxClip Lookup(string id)
    {
        if (library == null) library = Resources.Load<VfxLibrary>("VFX/VfxLibrary");
        if (library == null)
        {
            // 한 번만 알린다. 매 타격마다 콘솔이 도배되면 진짜 오류가 묻힌다.
            if (!libraryMissingLogged)
            {
                libraryMissingLogged = true;
                Debug.LogWarning("[PixelVfx] Resources/VFX/VfxLibrary 를 찾지 못했다. 이펙트가 나오지 않는다");
            }
            return null;
        }

        VfxClip c = library.Find(id);
        if (c == null) Debug.LogWarning($"[PixelVfx] '{id}' 정의가 없다. VfxLibrary의 id를 확인할 것");
        return c;
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Begin(VfxClip c)
    {
        clip = c;
        t = 0f;
        playing = true;

        if (sr == null) sr = GetComponent<SpriteRenderer>();
        sr.sprite = c.frames[0];
        sr.color = c.tint;
        sr.sortingLayerName = string.IsNullOrEmpty(c.sortingLayer) ? "Default" : c.sortingLayer;
        sr.sortingOrder = c.sortingOrder;
    }

    // 이펙트는 연출이라 슬로우(보스 처치)에 같이 늘어지는 편이 자연스럽다.
    // 그래서 여기만은 스케일된 시간을 쓴다 — UI 막대와 반대다.
    private void Update()
    {
        if (!playing || clip == null) return;

        t += Time.deltaTime * clip.fps;
        int i = Mathf.FloorToInt(t);

        if (i >= clip.frames.Length)
        {
            if (!clip.loop) { playing = false; Destroy(gameObject); return; }
            i %= clip.frames.Length;
            t -= clip.frames.Length;
        }

        sr.sprite = clip.frames[i];
    }

    // 루프 이펙트를 끝낼 때. 즉시 없앤다.
    public void Stop()
    {
        playing = false;
        Destroy(gameObject);
    }
}
