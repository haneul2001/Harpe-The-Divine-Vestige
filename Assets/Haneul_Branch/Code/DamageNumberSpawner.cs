using UnityEngine;
using UnityEngine.Rendering;
using DamageNumbersPro;

// 적이 데미지를 받을 때 플로팅 데미지 숫자를 띄우는 싱글톤 스포너.
// Enemy.TakeDamage / HarvestManager에서 호출한다. (Damage Numbers Pro 사용)
public class DamageNumberSpawner : MonoBehaviour
{
    public static DamageNumberSpawner Instance;

    [Header("프리팹")]
    [Tooltip("일반 데미지 숫자 프리팹 (Damage Numbers Pro).")]
    [SerializeField] private DamageNumber damageNumberPrefab;

    [Tooltip("크리티컬(풀차징 등) 데미지 숫자 프리팹. 비우면 일반 프리팹 사용.")]
    [SerializeField] private DamageNumber criticalNumberPrefab;

    [Tooltip("처형 'HARVEST!' 문구용 프리팹. 비우면 일반 프리팹 사용.")]
    [SerializeField] private DamageNumber harvestTextPrefab;

    [Tooltip("적 위치 기준 생성 오프셋. 보통 위로 살짝 올려 머리 위에 뜨게 함.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1f, 0f);

    [Header("정렬 (앞에 보이도록)")]
    [Tooltip("데미지 숫자를 그릴 정렬 레이어. 기본 Default는 맨 뒤라 가려지므로 앞쪽 레이어 지정.")]
    [SerializeField] private string sortingLayer = "Skill";
    [SerializeField] private int sortingOrder = 100;

    private void Awake()
    {
        Instance = this;
    }

    // 월드 위치에 데미지 숫자를 띄운다. critical이면 크리티컬 프리팹 사용.
    public void Show(Vector3 worldPosition, float amount, bool critical = false)
    {
        DamageNumber prefab = (critical && criticalNumberPrefab != null)
            ? criticalNumberPrefab
            : damageNumberPrefab;

        if (prefab == null) return;

        DamageNumber dn = prefab.Spawn(worldPosition + offset, amount);
        ApplySorting(dn);
    }

    // 월드 위치에 문구를 띄운다. (예: 처형 "HARVEST!")
    public void ShowText(Vector3 worldPosition, string text)
    {
        DamageNumber prefab = harvestTextPrefab != null ? harvestTextPrefab : damageNumberPrefab;
        if (prefab == null) return;

        DamageNumber dn = prefab.Spawn(worldPosition + offset, text);
        ApplySorting(dn);
    }

    // 생성된 숫자가 게임 화면 앞에 그려지도록 정렬을 설정한다.
    private void ApplySorting(DamageNumber dn)
    {
        if (dn == null) return;

        SortingGroup sg = dn.GetComponent<SortingGroup>();
        if (sg != null)
        {
            sg.sortingLayerName = sortingLayer;
            sg.sortingOrder = sortingOrder;
        }
    }
}
