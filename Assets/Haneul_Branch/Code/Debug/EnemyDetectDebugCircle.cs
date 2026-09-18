using UnityEngine;

// 적의 플레이어 탐지 범위를 게임 화면에 원으로 그린다.
// DebugBoxManager가 꺼져 있으면 보이지 않는다.
//
// 원은 Simple 드로우 모드 + 스케일로 크기를 맞춘다.
// Sliced는 9-slice 테두리가 있는 스프라이트에만 제대로 동작해서,
// 테두리 없는 원에 쓰면 사각형처럼 뭉개진다.
[RequireComponent(typeof(Enemy))]
public class EnemyDetectDebugCircle : DebugVisual
{
    [SerializeField] private Color color = new Color(1f, 0.45f, 0.45f, 0.5f);

    [Tooltip("추격 중일 때 쓸 색. 지금 누구를 쫓고 있는지 한눈에 보인다")]
    [SerializeField] private Color chasingColor = new Color(1f, 0.25f, 0.25f, 0.75f);

    private Enemy enemy;
    private SpriteRenderer circle;
    private Transform circleTransform;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();

        var go = new GameObject("DebugDetectRange");
        go.transform.SetParent(transform, false);
        circleTransform = go.transform;

        circle = MakeRenderer(go, CircleOutline(), color);
        circle.drawMode = SpriteDrawMode.Simple;   // 원은 스케일로 키운다
        circle.sortingOrder = 190;                 // 공격 박스(200)보다 뒤
    }

    protected override void Apply(bool visible)
    {
        if (circle != null) circle.enabled = visible;
    }

    private void LateUpdate()
    {
        if (circle == null || !circle.enabled) return;

        // 부모가 좌우 반전되거나 스폰 배율로 커져도 원은 그대로여야 한다
        circleTransform.rotation = Quaternion.identity;

        float parent = Mathf.Abs(transform.lossyScale.x);
        float sprite = circle.sprite != null ? circle.sprite.bounds.size.x : 1f;

        if (parent > 0.0001f && sprite > 0.0001f)
        {
            float diameter = enemy.detectRange * 2f;
            float s = diameter / (sprite * parent);
            circleTransform.localScale = new Vector3(s, s, 1f);
        }

        circleTransform.position = new Vector3(
            transform.position.x, transform.position.y, circleTransform.position.z);

        // 추격 중이면 진하게
        bool chasing = enemy.player != null && !enemy.isDead && enemy.CanSeePlayer();
        circle.color = chasing ? chasingColor : color;
    }
}
