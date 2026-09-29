using UnityEngine;

// 용 보스가 쓰는 이펙트 모음. 그림 정의는 VfxLibrary의 "Dragon…" 항목에 있다.
//
// 원본 시트(ERW)는 100 PPU로 들어와 있다. 프로젝트는 32 PPU 기준이라 그대로 띄우면 1/3 크기가 된다 —
// 라이브러리 배율(scale)에 100/32를 넣어 두고, 여기서는 "원하는 크기에 맞춰 더 늘리는" 것만 한다.
public static class DragonFx
{
    // 3페이즈 어둠 불꽃 — 주황 불 그림을 보라로 물들인다
    public static readonly Color DarkTint = new Color(0.72f, 0.42f, 1f, 1f);

    public static PixelVfx Play(string id, Vector3 pos, float scaleMul = 1f, float rotationZ = 0f, Color? tint = null)
    {
        PixelVfx v = PixelVfx.Play(id, pos, rotationZ);
        if (v == null) return null;

        if (!Mathf.Approximately(scaleMul, 1f)) v.transform.localScale *= scaleMul;
        if (tint.HasValue)
        {
            var sr = v.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = tint.Value;
        }
        return v;
    }

    // 가로세로 배율을 따로 준다 (바닥 장판은 원 모양 판정에 맞춰 세로를 더 늘린다)
    public static PixelVfx PlayScaled(string id, Vector3 pos, Vector2 scaleMul, Color? tint = null)
    {
        PixelVfx v = Play(id, pos, 1f, 0f, tint);
        if (v != null)
        {
            Vector3 s = v.transform.localScale;
            v.transform.localScale = new Vector3(s.x * scaleMul.x, s.y * scaleMul.y, 1f);
        }
        return v;
    }

    public static Transform Player()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        return p != null ? p.transform : null;
    }
}
