using UnityEngine;

// 위험지역이 바닥에 덮는 자리. 크기와 기준점만 담은 값이다.
//
// 이 값 하나로 두 가지를 만든다 — 게임에서 뜨는 빨간 표시(DangerZone)와,
// 툴에서 그리는 범위 그림. 둘이 따로 계산하면 숫자를 고쳤을 때 한쪽만 바뀌고,
// 그때부터 툴이 보여 주는 범위는 거짓말이 된다.
public enum DangerOrigin
{
    HitBox,     // 보스의 공격 판정 상자 자리
    Boss,       // 보스 발밑
    Player,     // 플레이어 발밑 (솟아오르는 계열)
}

public struct DangerShape
{
    public bool circle;
    public Vector2 size;        // 상자면 가로x세로, 원이면 지름 (월드 단위)
    public float forward;       // 공격 방향으로 밀어낸 거리
    public DangerOrigin origin;
    public int count;           // 같은 모양을 몇 군데 깔지
    public float spread;        // 여러 개일 때 흩어지는 반경

    // 이 크기가 보스 몸 크기에서 나온 값인가.
    // 게임에서는 스폰된 보스를 재니 저절로 맞지만, 프리팹만 보는 툴은
    // 방이 덮어쓰는 배율을 모르면 실제보다 작게 그리게 된다 (프리팹 3배 vs 스폰 4배).
    public bool fromBossScale;

    // 부채꼴이면 반각(도). 0이면 부채꼴이 아니다.
    //
    // 베기는 칼이 호를 그리며 지나가는 공격이라 네모로 예고하면 모서리가 거짓말을 한다.
    // 각도는 상자에서 끌어낸다 — 길이 L, 높이 H인 베기는 끝에서 H만큼 벌어지므로
    // 반각이 atan((H/2)/L)이면 이펙트가 덮는 각과 정확히 같아진다.
    public float halfAngle;

    public bool IsSector { get { return halfAngle > 0.01f; } }

    public float Radius { get { return size.x * 0.5f; } }
    public bool Valid { get { return size.x > 0.001f && size.y > 0.001f; } }

    public static DangerShape Box(Vector2 size, float forward, DangerOrigin origin)
    {
        var s = new DangerShape();
        s.circle = false;
        s.size = size;
        s.forward = forward;
        s.origin = origin;
        s.count = 1;
        return s;
    }

    public static DangerShape Circle(float radius, DangerOrigin origin)
    {
        var s = new DangerShape();
        s.circle = true;
        s.size = Vector2.one * radius * 2f;
        s.origin = origin;
        s.count = 1;
        return s;
    }

    // 베기용 부채꼴. 꼭짓점이 보스 발밑, 반지름이 칼이 닿는 거리다.
    public static DangerShape Sector(float radius, float halfAngleDeg, DangerOrigin origin)
    {
        var s = new DangerShape();
        s.circle = true;                       // 둥근 계열 — 가운데서 자라난다
        s.halfAngle = Mathf.Clamp(halfAngleDeg, 1f, 180f);
        s.size = Vector2.one * radius * 2f;
        s.origin = origin;
        s.count = 1;
        return s;
    }

    // 길이 L x 높이 H인 베기를 같은 각도의 부채꼴로 바꾼다
    public static DangerShape SectorFromBox(Vector2 box, DangerOrigin origin)
    {
        if (box.x <= 0.001f) return new DangerShape();

        float half = Mathf.Atan2(box.y * 0.5f, box.x) * Mathf.Rad2Deg;
        return Sector(box.x, half, origin);
    }

    // 같은 모양이 여러 군데 깔리는 패턴 (무덤 균열 등)
    public DangerShape Scattered(int howMany, float radius)
    {
        count = Mathf.Max(1, howMany);
        spread = radius;
        return this;
    }

    // 보스 몸 크기를 따라가는 크기임을 표시한다
    public DangerShape ScaledWithBoss()
    {
        fromBossScale = true;
        return this;
    }

    // 보스가 실제로 스폰되는 배율로 고쳐 잰다 (툴 전용 — 게임은 이미 맞는 값을 본다)
    public DangerShape Rescaled(float multiplier)
    {
        if (!fromBossScale || multiplier <= 0f) return this;
        var s = this;
        s.size *= multiplier;
        s.forward *= multiplier;
        return s;
    }

    public override string ToString()
    {
        string one = IsSector
            ? "부채꼴 반지름 " + Radius.ToString("0.##") + " 각 " + (halfAngle * 2f).ToString("0") + "도"
            : circle
                ? "원 지름 " + size.x.ToString("0.##")
                : "상자 " + size.x.ToString("0.##") + "x" + size.y.ToString("0.##");
        if (count > 1) one += " x" + count + " (흩어짐 " + spread.ToString("0.#") + ")";
        return one;
    }
}
