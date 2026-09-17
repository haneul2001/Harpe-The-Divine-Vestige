using UnityEngine;

// 처형 단계 버프 (최대 3단계).
//
// 처형이 끝날 때마다 한 단계씩 오른다(HarvestManager가 AddStage 호출). buffDuration 동안 다시 처형하지 않으면 0단계로 돌아간다.
//  · 연출: 발밑에 텔레포트 잔상(Vefects Teleport 01)을 계속 틀어 둔다. 좌우로 늘리고 단계마다 색을 바꾼다
//          1단계 원래 색(흰·보라) → 2단계 노란색 → 3단계 빨간색
//          앞쪽 흰 물결은 캐릭터를 덮고, 위쪽 진보라 물결은 캐릭터 뒤에 깔린다 (텍스처를 색으로 쪼갠 두 장)
//  · 효과: 1단계 공격력 +30%·이속 +20%, 2단계 공격력 +60%·이속 +40%, 3단계 이속 +60%·무조건 치명타 (CharacterStats.damageMult / forceCrit — 평타·대시 공격·스킬 전부)
public class HarvestBuff : MonoBehaviour
{
    [Header("단계")]
    [Tooltip("마지막 처형 후 이 시간(초) 안에 다시 처형하지 않으면 버프가 전부 사라진다")]
    [SerializeField] private float buffDuration = 5f;
    [Tooltip("단계별 공격력 보너스. [0]=1단계 ... 0.3이면 +30%")]
    [SerializeField] private float[] damageBonus = { 0.3f, 0.6f, 0.6f };
    [Tooltip("단계별 이동속도 보너스. [0]=1단계 ... 0.2이면 +20%")]
    [SerializeField] private float[] moveSpeedBonus = { 0.2f, 0.4f, 0.6f };
    [Tooltip("단계별 무조건 치명타. [0]=1단계 ...")]
    [SerializeField] private bool[] guaranteedCrit = { false, false, true };

    [Header("발밑 잔상")]
    [Tooltip("VFX_2D_Teleport_01_Color_Loop_Static")]
    [SerializeField] private GameObject auraPrefab;
    [Tooltip("캐릭터 뒤에 깔리는 장(위쪽 진보라 물결) 재질. [0]=1단계 ...")]
    [SerializeField] private Material[] backMaterials = new Material[3];
    [Tooltip("캐릭터를 덮는 장(앞쪽 흰 물결·파란 불티) 재질. [0]=1단계 ...")]
    [SerializeField] private Material[] frontMaterials = new Material[3];
    [Tooltip("좌우·상하 크기 배율 (원본 파티클은 2유닛)")]
    [SerializeField] private Vector2 auraScale = new Vector2(1.4f, 0.7f);
    [Tooltip("발 기준 위치")]
    [SerializeField] private Vector2 auraOffset = new Vector2(0f, -0.2f);
    [Tooltip("뒤 장: 캐릭터 그림 기준 정렬 순서 차이 (음수 = 캐릭터 뒤)")]
    [SerializeField] private int backSortingOffset = -1;
    [Tooltip("앞 장: 캐릭터 그림 기준 정렬 순서 차이 (양수 = 캐릭터 앞)")]
    [SerializeField] private int frontSortingOffset = 1;

    public int Stage { get; private set; }
    // 상태 아이콘(버프 남은 시간) 표시용
    public float Duration => buffDuration;
    public float Remaining => Stage > 0 ? Mathf.Max(0f, expireTime - Time.time) : 0f;

    public int MaxStage => damageBonus != null && damageBonus.Length > 0 ? damageBonus.Length : 3;

    private PlayerStatus status;
    private PlayerMove move;
    private SpriteRenderer body;
    private GameObject auraBack;
    private GameObject auraFront;
    private float expireTime;

    private void Awake()
    {
        status = GetComponent<PlayerStatus>();
        move = GetComponent<PlayerMove>();
        body = GetComponentInChildren<SpriteRenderer>();
    }

    public void AddStage()
    {
        SetStage(Mathf.Min(Stage + 1, MaxStage));
        expireTime = Time.time + buffDuration;
    }

    public void Clear()
    {
        SetStage(0);
    }

    private void Update()
    {
        if (Stage > 0 && Time.time >= expireTime) SetStage(0);
    }

    private void SetStage(int stage)
    {
        Stage = stage;

        if (status != null && status.Stats != null)
        {
            status.Stats.damageMult = 1f + (stage > 0 && damageBonus != null && stage <= damageBonus.Length ? damageBonus[stage - 1] : 0f);
            status.Stats.forceCrit = stage > 0 && guaranteedCrit != null && stage <= guaranteedCrit.Length && guaranteedCrit[stage - 1];
        }

        if (move != null)
            move.speedMult = 1f + (stage > 0 && moveSpeedBonus != null && stage <= moveSpeedBonus.Length ? moveSpeedBonus[stage - 1] : 0f);

        if (stage <= 0)
        {
            if (auraBack != null) Destroy(auraBack);
            if (auraFront != null) Destroy(auraFront);
            auraBack = auraFront = null;
            return;
        }

        // 한 장의 파티클은 캐릭터 앞이든 뒤든 통째로만 정렬된다(카메라 정렬 설정도 마찬가지).
        // 그래서 플립북을 색으로 앞/뒤 두 텍스처로 쪼개 두고, 같은 잔상을 두 장 겹쳐 앞뒤에 따로 그린다.
        if (auraBack == null && auraFront == null)
        {
            uint seed = (uint)Random.Range(1, int.MaxValue);
            auraBack = SpawnAura(backSortingOffset, seed);
            auraFront = SpawnAura(frontSortingOffset, seed);
        }

        ApplyMaterial(auraBack, backMaterials, stage);
        ApplyMaterial(auraFront, frontMaterials, stage);
    }

    private static void ApplyMaterial(GameObject go, Material[] mats, int stage)
    {
        if (go == null || mats == null || stage > mats.Length || mats[stage - 1] == null) return;
        foreach (var r in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
            if (r.renderMode != ParticleSystemRenderMode.None) r.sharedMaterial = mats[stage - 1];
    }

    private GameObject SpawnAura(int sortingOffset, uint seed)
    {
        if (auraPrefab == null) return null;

        GameObject go = Instantiate(auraPrefab, transform);
        go.transform.localPosition = auraOffset;

        // 플레이어 스케일(0.64 등)을 되돌려 월드 크기를 프리팹 기준으로 맞춘다
        Vector3 ls = transform.lossyScale;
        go.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(ls.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(ls.y)), 1f);

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            // 앞/뒤 두 장이 같은 프레임을 그리도록 난수를 맞춘다
            ps.useAutoRandomSeed = false;
            ps.randomSeed = seed;

            var main = ps.main;
            main.loop = true;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r == null || r.renderMode == ParticleSystemRenderMode.None) continue;

            // 좌우로만 늘리기 — 빌보드는 트랜스폼 비균일 스케일보다 3D 시작 크기가 확실하다
            float size = main.startSize.constant;
            main.startSize3D = true;
            main.startSizeX = size * auraScale.x;
            main.startSizeY = size * auraScale.y;
            main.startSizeZ = size;

            if (body != null)
            {
                r.sortingLayerID = body.sortingLayerID;
                r.sortingOrder = body.sortingOrder + sortingOffset;
            }
        }

        go.GetComponentInChildren<ParticleSystem>().Play(true);
        return go;
    }

    private void OnDisable()
    {
        Clear();
    }
}
