using UnityEngine;

// 플레이어의 공격 판정 범위를 게임 화면에 그린다.
//
// PlayerCombat은 Physics2D.OverlapBoxAll(AttackBoxPos.position, boxSize)로 때리는데
// 지금까지 Gizmos로만 그려서 Scene 뷰에서만 보였다. 실제 플레이 화면에서
// 사거리를 확인하려면 별도 표시가 필요하다.
//
// 값을 복사해 두지 않고 매 프레임 PlayerCombat에서 읽는다 —
// 인스펙터에서 boxSize를 조절하면 즉시 반영돼 사거리 맞추기가 쉬워진다.
[RequireComponent(typeof(PlayerCombat))]
public class PlayerAttackDebugBox : DebugVisual
{
    [Header("공격 박스")]
    [SerializeField] private Color attackColor = new Color(1f, 0.85f, 0.3f, 0.85f);

    [Header("처형 범위")]
    [Tooltip("처형 가능 거리(harvestRange)를 원으로 함께 표시한다")]
    [SerializeField] private bool showHarvestRange = true;
    [SerializeField] private Color harvestColor = new Color(0.45f, 0.9f, 1f, 0.55f);

    private PlayerCombat combat;
    private SpriteRenderer boxRenderer;
    private SpriteRenderer circleRenderer;
    private Transform boxTransform;
    private Transform circleTransform;

    private float harvestRange;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();

        boxTransform = Create("DebugAttackBox", BoxOutline(), attackColor, out boxRenderer);
        circleTransform = Create("DebugHarvestRange", CircleOutline(), harvestColor, out circleRenderer);

        // harvestRange는 private이라 직렬화 값에서 꺼내 온다.
        // 공개 프로퍼티를 새로 뚫는 것보다 디버그 쪽이 감수하는 편이 낫다.
        harvestRange = ReadHarvestRange();
    }

    private Transform Create(string name, Sprite sprite, Color color, out SpriteRenderer sr)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        sr = MakeRenderer(go, sprite, color);
        return go.transform;
    }

    private float ReadHarvestRange()
    {
        var field = typeof(PlayerCombat).GetField("harvestRange",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field != null ? (float)field.GetValue(combat) : 0f;
    }

    protected override void Apply(bool visible)
    {
        if (boxRenderer != null) boxRenderer.enabled = visible;
        if (circleRenderer != null) circleRenderer.enabled = visible && showHarvestRange;
    }

    private void LateUpdate()
    {
        if (!DebugBoxManager.Visible) return;

        // 공격 박스 — AttackBoxPos가 좌우 반전에 따라 움직이므로 매 프레임 따라간다
        if (combat.AttackBoxPos != null)
        {
            boxTransform.position = combat.AttackBoxPos.position;
            boxTransform.rotation = Quaternion.identity;   // 부모가 뒤집혀도 박스는 그대로
            boxRenderer.size = combat.boxSize;
        }

        if (showHarvestRange)
        {
            circleTransform.position = transform.position;
            circleTransform.rotation = Quaternion.identity;
            circleTransform.localScale = Vector3.one;
            circleRenderer.drawMode = SpriteDrawMode.Sliced;
            circleRenderer.size = Vector2.one * (harvestRange * 2f);
        }
    }
}
