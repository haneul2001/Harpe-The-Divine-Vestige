using UnityEngine;

// 카드 연출에 쓰는 그림을 코드로 만든다. 에셋으로 두면 등급 색마다 파일이 늘어나는데,
// 어차피 색은 등급에서 오므로 흰색 한 장만 만들어 두고 색은 SpriteRenderer/Image로 입힌다.
public static class CardFx
{
    private const int BurstPixels = 48;
    private static Sprite burst;

    // 카드 뒤에 까는 픽셀 방사광 (8갈래 + 가운데 광원)
    public static Sprite Burst()
    {
        if (burst != null) return burst;

        const int n = BurstPixels;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        float half = (n - 1) * 0.5f;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = x - half, dy = y - half;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / half;
                if (r > 1f) { px[y * n + x] = new Color32(255, 255, 255, 0); continue; }

                float ang = Mathf.Atan2(dy, dx);
                float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 4f)), 6f) * (1f - r);
                float core = Mathf.Clamp01(1f - r * 1.6f);
                float a = Mathf.Clamp01(core + ray * 0.9f);

                // 알파를 4단으로 끊어야 픽셀 그림처럼 보인다 (부드러운 그라데이션은 이질적이다)
                a = Mathf.Floor(a * 4f) / 4f;
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 200f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();

        burst = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        burst.name = "CardBurst";
        return burst;
    }

    // 등급이 높을수록 빛이 두껍고 빠르게 뛴다 — 고르기 전에 이미 "센 카드"라는 게 읽혀야 한다
    public static float PulseSpeed(AbilityRarity r) { return 1.6f + 0.7f * (int)r; }
    public static float PulseDepth(AbilityRarity r) { return 0.08f + 0.06f * (int)r; }
    public static float BaseAlpha(AbilityRarity r) { return 0.55f + 0.15f * (int)r; }

    // 윤곽 빛은 그림 모양대로 한 색으로 칠해야 한다 — 기본 UI 셰이더는 색을 그림에 곱해서
    // 회색 카드에 회색 등급색을 곱하면 탁한 테두리만 남는다
    private const string HaloMaterialPath = "UI/M_CardHalo";
    private static Material haloMaterial;

    public static Material HaloMaterial()
    {
        if (haloMaterial == null) haloMaterial = Resources.Load<Material>(HaloMaterialPath);
        return haloMaterial;
    }

    // 윤곽 빛(D안): 카드 그림을 등급 색으로 물들여 조금씩 키운 복사본을 뒤에 겹친다.
    // 값은 그림 픽셀 단위로 "테두리에서 몇 픽셀 번지는가" — 등급이 높을수록 멀리 번진다.
    public static float[] HaloPads(AbilityRarity r)
    {
        switch (r)
        {
            case AbilityRarity.Legendary: return new[] { 2f, 4f, 7f, 11f };
            case AbilityRarity.Epic:      return new[] { 2f, 4f, 7f };
            case AbilityRarity.Rare:      return new[] { 2f, 4f };
            default:                      return new[] { 2f, 4f };
        }
    }

    // 안쪽 겹이 가장 진하고 바깥으로 갈수록 옅어진다
    public static float HaloLayerAlpha(int index, int count)
    {
        return Mathf.Lerp(1f, 0.18f, count <= 1 ? 0f : index / (float)(count - 1));
    }
}
