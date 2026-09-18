using UnityEngine;

// 일반 공격 베기 이펙트 (Vefects Pixel Craft — 2D Sword Slash 01).
//
// 팩의 슬래시 8종은 전부 같은 플립북(4x2) 한 장이고 파티클 시작 회전과 좌우 반전만 다르다.
// 플립북을 뜯어 보면 회전 0일 때 초승달의 볼록한 면이 오른쪽(→)을 향하고, 위에서 아래로
// 시계 방향으로 벤다. 여기서 규칙이 나온다:
//   · Mask_Static / _90 / _180 / _270        : 볼록면 방향 = -회전 (시계 방향 회전)  → →, ↓, ←, ↑
//   · Mask_Static_Flip / _Flip_90 / 180 / 270 : 좌우 반전이라 볼록면 = 180 - 회전      → ←, ↑, →, ↓
//                                              스윙도 반대(반시계)로 돈다
// 8방향 중 대각선은 팩에 없으므로, 가장 가까운 프리팹을 고른 뒤 시작 회전을 45도 더 돌린다.
// 콤보 두 번째 타는 Flip 계열을 써서 반대로 베는 것처럼 보이게 한다.
public class PlayerSlashVfx : MonoBehaviour
{
    [Tooltip("순서 고정: [0]Mask_Static [1]_90 [2]_180 [3]_270")]
    [SerializeField] private GameObject[] normal = new GameObject[4];
    [Tooltip("순서 고정: [0]Mask_Static_Flip [1]_Flip_90 [2]_Flip_180 [3]_Flip_270")]
    [SerializeField] private GameObject[] flipped = new GameObject[4];

    [Tooltip("이펙트 크기 배율. 파티클 기본 크기가 2유닛이라 1이면 2유닛")]
    [SerializeField] private float scale = 1f;
    [Tooltip("공격 판정 중심에서 플레이어 쪽으로 당기는 거리(0~1). 0이면 판정 중심에 그린다")]
    [Range(0f, 1f)]
    [SerializeField] private float pullTowardPlayer = 0.2f;
    [SerializeField] private float lifeTime = 0.5f;
    [Tooltip("캐릭터보다 이만큼 앞에 그린다")]
    [SerializeField] private int sortingOrderOffset = 10;

    private SpriteRenderer body;

    private void Awake()
    {
        body = GetComponentInChildren<SpriteRenderer>();
    }

    // angleDeg: 공격 방향 (→ 0, ↑ 90, 반시계). reverseSwing: 콤보 반대 베기
    public void Play(Vector3 hitCenter, float angleDeg, bool reverseSwing)
    {
        Play(hitCenter, angleDeg, reverseSwing, null);
    }

    // colorMaterial: 색을 바꾼 Mask 재질 (대시 공격의 붉은 베기 등). null이면 원래 색
    public void Play(Vector3 hitCenter, float angleDeg, bool reverseSwing, Material colorMaterial)
    {
        // 원하는 볼록면 방향 θ에 필요한 시작 회전 (Unity 파티클 회전은 시계 방향이 +)
        float rotation = reverseSwing ? 180f - angleDeg : -angleDeg;
        rotation = Mathf.Repeat(rotation, 360f);

        int quadrant = Mathf.RoundToInt(rotation / 90f) % 4;
        GameObject prefab = (reverseSwing ? flipped : normal)[quadrant];
        if (prefab == null) return;

        Vector3 pos = Vector3.Lerp(hitCenter, transform.position, pullTowardPlayer);
        GameObject go = Instantiate(prefab, pos, Quaternion.identity);
        go.transform.localScale = Vector3.one * scale;

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            // 인스턴스가 생기는 순간 이미 한 발 뿜었을 수 있으니 비우고 각도를 바꾼 뒤 다시 튼다
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            // 이 프리팹의 기본 회전(0/90/180/270)을 버리고 정확한 각도로 덮는다 — 대각선은 여기서 45도가 더해진다
            if (ps.GetComponent<ParticleSystemRenderer>() != null && ps.emission.burstCount > 0)
                main.startRotation = rotation * Mathf.Deg2Rad;
        }

        foreach (var r in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (body == null) break;
            r.sortingLayerID = body.sortingLayerID;
            r.sortingOrder = body.sortingOrder + sortingOrderOffset;
            if (colorMaterial != null && r.renderMode != ParticleSystemRenderMode.None)
                r.sharedMaterial = colorMaterial;
        }

        go.GetComponentInChildren<ParticleSystem>().Play(true);
        Destroy(go, lifeTime);
    }
}
