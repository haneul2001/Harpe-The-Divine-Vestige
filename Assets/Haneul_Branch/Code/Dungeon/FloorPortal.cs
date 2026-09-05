using UnityEngine;

// 보스를 잡으면 보스 방에 생기는 다음 층 입구.
//
// 밟는 즉시 넘어간다(아이작의 뚜껑문과 같다). 확인 키를 두지 않는 이유는
// 보스를 깬 직후엔 어차피 다음 층 말고 할 일이 없기 때문이다.
[RequireComponent(typeof(CircleCollider2D))]
public class FloorPortal : MonoBehaviour
{
    [Tooltip("생기고 나서 이 시간 동안은 밟혀도 반응하지 않는다(초).\n"
           + "보스가 쓰러진 자리에 그대로 생기므로, 없으면 처치 연출을 보기도 전에 넘어가 버린다")]
    [SerializeField] private float armDelay = 1.2f;

    private float armedAt;
    private bool used;

    private void Awake()
    {
        CircleCollider2D c = GetComponent<CircleCollider2D>();
        c.isTrigger = true;

        armedAt = Time.time + armDelay;
    }

    // Enter만으로는 부족하다. 포탈이 플레이어 발밑에 생기면 "들어오는" 순간이 없어
    // 영영 반응하지 않는다.
    private void OnTriggerEnter2D(Collider2D other) { TryUse(other); }
    private void OnTriggerStay2D(Collider2D other) { TryUse(other); }

    private void TryUse(Collider2D other)
    {
        if (used || Time.time < armedAt) return;
        if (other == null || !other.CompareTag("Player")) return;

        used = true;

        if (FloorFlow.Instance != null) FloorFlow.Instance.Advance();
        else Debug.LogWarning("[FloorPortal] FloorFlow가 없어 다음 층으로 갈 수 없다", this);
    }
}
