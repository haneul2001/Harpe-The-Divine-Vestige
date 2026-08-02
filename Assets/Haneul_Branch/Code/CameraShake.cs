using UnityEngine;

// 액션게임식 카메라 흔들림.
// "트라우마" 값을 쌓고 시간에 따라 줄이며, 흔들림 세기는 트라우마의 제곱을 쓴다.
// 제곱을 쓰면 끝날 때 뚝 끊기지 않고 자연스럽게 잦아든다.
//
// Main Camera에 CameraFollow와 함께 붙이면 되고,
// CameraFollow가 위치를 정한 뒤 이 값을 더한다(LateUpdate 순서 문제 없음).
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("흔들림 세기")]
    [Tooltip("트라우마가 1일 때의 최대 위치 흔들림(월드 유닛)")]
    [SerializeField] private float maxOffset = 0.35f;

    [Tooltip("트라우마가 1일 때의 최대 회전(도). 픽셀아트는 회전 시 지글거릴 수 있어 기본 0")]
    [SerializeField] private float maxRoll = 0f;

    [Tooltip("초당 트라우마 감소량. 클수록 빨리 멎음")]
    [SerializeField] private float decay = 1.8f;

    [Tooltip("흔들리는 속도. 클수록 잘게 떨림")]
    [SerializeField] private float frequency = 22f;

    private float trauma;
    private float seed;

    // CameraFollow가 매 LateUpdate에 읽어간다
    public Vector3 Offset { get; private set; }
    public float Roll { get; private set; }

    private void Awake()
    {
        Instance = this;
        seed = Random.value * 100f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 흔들림 추가. amount 0~1 (0.2 가벼운 타격 / 0.6 처형 임팩트 / 1.0 최대)
    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    // 어디서든 부르기 쉽게 (카메라에 컴포넌트가 없으면 조용히 무시)
    public static void Shake(float amount)
    {
        if (Instance != null) Instance.AddTrauma(amount);
    }

    private void Update()
    {
        if (trauma <= 0f)
        {
            Offset = Vector3.zero;
            Roll = 0f;
            return;
        }

        trauma = Mathf.Max(0f, trauma - decay * Time.deltaTime);

        float s = trauma * trauma;
        float t = Time.time * frequency;

        // PerlinNoise는 0~1이라 -1~1로 옮긴다. 축마다 다른 샘플 지점을 써서 따로 움직이게 함
        Offset = new Vector3(
            (Mathf.PerlinNoise(seed + t, 0f) * 2f - 1f) * maxOffset * s,
            (Mathf.PerlinNoise(0f, seed + t) * 2f - 1f) * maxOffset * s,
            0f);

        Roll = (Mathf.PerlinNoise(seed + t, seed + t) * 2f - 1f) * maxRoll * s;
    }
}
