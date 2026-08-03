using UnityEngine;

// 적 머리 위 감정 표시. 플레이어를 어떻게 인식하고 있는지를 한 글자로 보여 준다.
//
//   탐지 범위 밖        → 아무것도 안 뜸
//   범위 안 + 은신 아님  → 느낌표  (발견)
//   범위 안 + 은신 중    → 물음표  (뭔가 있는 것 같은데 안 보임)
//
// ★ 오브젝트 하나에서 스프라이트만 바꾼다.
//   느낌표용/물음표용을 따로 두면 상태가 겹치는 순간 둘 다 떠서 겹쳐 보인다.
//
// 공격과는 무관하다. 그래서 AttackEnemyBase가 아니라 별도 컴포넌트로 뒀고,
// 히트박스가 없는 원거리 적에게도 똑같이 붙는다.
[RequireComponent(typeof(Enemy))]
public class EnemyEmotion : MonoBehaviour
{
    private enum Mood { None, Alert, Suspicious }

    [Header("참조")]
    [Tooltip("머리 위 표시. 비우면 자식에서 'EmotionImage'를 찾는다")]
    [SerializeField] private SpriteRenderer target;

    [Header("스프라이트")]
    [Tooltip("플레이어를 발견했을 때 (느낌표)")]
    [SerializeField] private Sprite alertSprite;
    [Tooltip("은신한 플레이어가 근처에 있을 때 (물음표)")]
    [SerializeField] private Sprite suspiciousSprite;

    [Header("색 (스프라이트에 곱해진다. 흰색이면 원본 그대로)")]
    [SerializeField] private Color alertColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color suspiciousColor = Color.white;

    [Header("크기·위치 (월드 기준 — 적 크기와 무관하게 일정)")]
    [Tooltip("화면에 보이는 크기(유닛). 부모 스케일을 상쇄하므로 적이 커도 그대로다")]
    [SerializeField] private float worldSize = 0.45f;

    [Tooltip("적 스프라이트 위쪽에서 띄우는 거리(유닛)")]
    [SerializeField] private float heightMargin = 0.25f;

    [Header("연출")]
    [Tooltip("선명하게 떠 있는 시간(초). 지나면 서서히 사라지기 시작한다.\n" +
             "0 이하면 상태가 바뀔 때까지 계속 떠 있는다.")]
    [SerializeField] private float displayDuration = 0.5f;

    [Tooltip("사라지는 데 걸리는 시간(초). 0이면 즉시 꺼진다")]
    [SerializeField] private float fadeDuration = 0.5f;

    [Tooltip("표시가 바뀔 때 톡 튀어오르는 크기 배율. 1이면 연출 없음")]
    [SerializeField] private float popScale = 1.35f;
    [SerializeField] private float popDuration = 0.18f;

    [Tooltip("상태를 확인하는 주기(초). 매 프레임 할 필요는 없다")]
    [SerializeField] private float checkInterval = 0.08f;

    private Enemy enemy;
    private SpriteRenderer body;   // 적 본체 — 머리 위 높이를 여기서 잰다
    // 마지막으로 판정한 상태. 표시가 시간이 지나 사라져도 이 값은 유지된다 —
    // 그래야 같은 상태가 계속될 때 0.5초마다 다시 뜨는 깜빡임이 생기지 않는다.
    private Mood current = Mood.None;
    private float nextCheck;
    private float popTimer;
    private float holdTimer;
    private float fadeTimer;
    private Color tint;   // 지금 표시의 색 (알파는 페이드가 따로 곱한다)

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        body = GetComponent<SpriteRenderer>();

        if (target == null)
        {
            Transform t = transform.Find("EmotionImage");
            if (t != null) target = t.GetComponent<SpriteRenderer>();
        }

        if (target != null) target.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (target == null) return;

        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + checkInterval;
            SetMood(Evaluate());
        }

        UpdateFade();
    }

    // 선명하게 유지 → 서서히 사라짐.
    // 상태가 바뀌면 SetMood가 타이머를 되돌리므로 페이드 중이라도 즉시 새 표시로 갈아탄다.
    private void UpdateFade()
    {
        if (!target.gameObject.activeSelf) return;

        if (holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;
            SetAlpha(1f);

            if (holdTimer <= 0f) fadeTimer = fadeDuration;
            return;
        }

        if (displayDuration <= 0f) return;   // 계속 떠 있는 설정

        if (fadeDuration <= 0f)
        {
            target.gameObject.SetActive(false);
            return;
        }

        fadeTimer -= Time.deltaTime;
        SetAlpha(Mathf.Clamp01(fadeTimer / fadeDuration));

        if (fadeTimer <= 0f) target.gameObject.SetActive(false);
    }

    private void SetAlpha(float a)
    {
        target.color = new Color(tint.r, tint.g, tint.b, tint.a * a);
    }

    // 적이 좌우를 볼 때 루트가 Y 180도로 뒤집히고, 스폰 시 스케일도 덮어써진다.
    // 표시는 그 영향을 받으면 안 되므로 매 프레임 월드 기준으로 다시 잡는다.
    //
    // LateUpdate인 이유: 적의 방향 전환(FaceToPlayer)이 Update에서 일어나므로
    // 그보다 뒤에 덮어써야 한 프레임도 뒤집혀 보이지 않는다.
    private void LateUpdate()
    {
        if (target == null || !target.gameObject.activeSelf) return;

        Transform t = target.transform;

        // ① 회전 상쇄 — 부모가 뒤집혀도 똑바로
        t.rotation = Quaternion.identity;

        // ② 크기 상쇄 — 부모 스케일과 무관하게 항상 worldSize 유닛
        float parent = Mathf.Abs(transform.lossyScale.x);
        float sprite = target.sprite != null ? target.sprite.bounds.size.x : 1f;

        if (parent > 0.0001f && sprite > 0.0001f)
        {
            float s = worldSize / (sprite * parent);
            t.localScale = new Vector3(s, s, 1f) * PopMultiplier();
        }

        // ③ 위치 — 적 스프라이트 머리 위로
        if (body != null && body.sprite != null)
        {
            Bounds b = body.bounds;
            t.position = new Vector3(b.center.x, b.max.y + heightMargin, t.position.z);
        }
    }

    private float PopMultiplier()
    {
        if (popScale <= 1f || popDuration <= 0f || popTimer <= 0f) return 1f;

        popTimer -= Time.deltaTime;
        float k = Mathf.Clamp01(popTimer / popDuration);   // 1 → 0
        return 1f + (popScale - 1f) * k;
    }

    private Mood Evaluate()
    {
        if (enemy == null || enemy.isDead) return Mood.None;

        // 등장 연출 중에는 Enemy 컴포넌트가 꺼져 있다. 아직 깨어나지 않은 적이
        // 플레이어를 알아보면 이상하므로 같이 숨긴다.
        if (!enemy.enabled) return Mood.None;

        if (enemy.player == null) return Mood.None;
        if (enemy.DistanceToPlayer() > enemy.detectRange) return Mood.None;

        // 거리로만 판단한다. CanSeePlayer는 은신 중이면 false라
        // 그걸 쓰면 물음표가 뜰 기회가 없다.
        return PlayerStealth.IsHidden ? Mood.Suspicious : Mood.Alert;
    }

    private void SetMood(Mood mood)
    {
        if (mood == current) return;
        current = mood;

        if (mood == Mood.None)
        {
            target.gameObject.SetActive(false);
            return;
        }

        Sprite sprite = mood == Mood.Alert ? alertSprite : suspiciousSprite;
        if (sprite == null)
        {
            target.gameObject.SetActive(false);
            return;
        }

        target.sprite = sprite;
        tint = mood == Mood.Alert ? alertColor : suspiciousColor;

        target.gameObject.SetActive(true);
        SetAlpha(1f);   // 페이드 중이었더라도 즉시 선명하게

        // 바뀔 때마다 톡 튀어야 눈에 들어온다
        popTimer = popDuration;
        holdTimer = displayDuration > 0f ? displayDuration : float.MaxValue;
        fadeTimer = 0f;
    }

}
