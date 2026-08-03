using System.Collections;
using UnityEngine;

public class HarvestManager : MonoBehaviour
{
    public static HarvestManager Instance;

    [SerializeField] private float harvestDuration = 1.5f;

    [Tooltip("처형(V) 애니메이션 재생 배율. 1 = 기본, 2 = 2배 빠름")]
    [SerializeField] private float harvestSpeed = 1f;

    [Tooltip("처형 시 획득 소울량. 적 EnemyInfo에 Soul 값이 있으면 그 값을, 없으면 이 기본값을 사용.")]
    [SerializeField] private int defaultSoulGain = 10;

    [Tooltip("'적 좌표로 순간이동'(OntoTarget) 모드일 때 적 위치 기준 보정. 내려찍기 등 연출용.")]
    [SerializeField] private Vector3 onTargetOffset = Vector3.zero;

    [Header("임팩트 (내려찍기가 닿는 순간)")]
    [Tooltip("처형 애니메이션 시작 후 내려찍기가 적중하는 시점(초).\n" +
             "Reaper의 Surprise Attack은 12fps 클립의 10번째 프레임에서 임팩트 이펙트가 터지므로 10/12 = 0.833초.\n" +
             "harvestSpeed 배율은 자동으로 반영된다.")]
    [SerializeField] private float impactTime = 0.833f;

    [Tooltip("임팩트 순간에 처형 데미지 숫자를 띄운다. 값은 적의 남은 체력(= 처형으로 준 피해).\n" +
             "끄면 'HARVEST!' 문구만 남는다 — 문구와 숫자가 겹치면 시선이 분산된다.\n" +
             "이 옵션은 '처형 대상'의 숫자만 막는다. 처형 파생 효과(충격파 등)로\n" +
             "주변 몹이 받는 피해는 Enemy.TakeDamage를 타므로 그쪽 숫자는 정상적으로 뜬다.")]
    [SerializeField] private bool showDamageOnImpact = false;

    [Tooltip("처형 데미지를 크리티컬 숫자 프리팹으로 띄운다")]
    [SerializeField] private bool impactDamageAsCritical = true;

    [Tooltip("체크: 'HARVEST!' 문구도 임팩트에 맞춰 띄운다 / 해제: 기존처럼 처형 시작과 동시에 띄운다")]
    [SerializeField] private bool harvestTextOnImpact = true;

    [Tooltip("데미지 숫자와 겹치지 않도록 'HARVEST!' 문구만 추가로 올리는 높이")]
    [SerializeField] private float harvestTextExtraHeight = 0.8f;

    // 처형의 내려찍기가 실제로 닿은 순간. 능력 카드 효과가 여기에 올라탄다.
    // 이벤트로 둔 이유: 카드가 늘어날 때마다 이 클래스에 if가 쌓이면 안 된다.
    public event System.Action<HarvestImpact> ImpactLanded;

    [Header("카메라 흔들림")]
    [Tooltip("임팩트 순간 카메라를 흔든다 (Main Camera에 CameraShake 컴포넌트 필요)")]
    [SerializeField] private bool shakeOnImpact = true;

    [Tooltip("흔들림 세기 0~1. 0.2 가벼운 타격 / 0.6 처형 / 1.0 최대")]
    [Range(0f, 1f)]
    [SerializeField] private float impactShake = 0.6f;

    public enum DashTrailMode
    {
        SingleStretched,   // 이펙트 1개를 경로 길이에 맞춰 늘림 (슬래시/섬광선 형태에 적합)
        RepeatedAlongPath, // 경로를 따라 여러 개 생성 (잔상/고스트 형태에 적합)
        LineBeam           // 시작→끝을 잇는 직선 빔(LineRenderer). 두 점을 항상 정확히 연결
    }

    public enum LineBeamShape
    {
        Rectangle,    // 처음부터 끝까지 같은 두께 (네모)
        PointedEnds,  // 가운데가 굵고 양 끝이 뾰족 (슬래시/방추 형태)
        PointedTail,  // 시작(발사지점)이 뾰족, 도착점이 굵음
        PointedHead   // 시작이 굵음, 도착점이 뾰족
    }

    [Header("순간이동 섬광/잔상 이펙트")]
    [Tooltip("LineBeam: 시작→끝을 잇는 직선 빔(아래 LineBeam 옵션 사용, 프리팹 불필요)\nSingleStretched/RepeatedAlongPath: 아래 프리팹 사용")]
    [SerializeField] private DashTrailMode dashTrailMode = DashTrailMode.LineBeam;

    [Tooltip("SingleStretched / RepeatedAlongPath 모드에서 생성할 이펙트 프리팹. LineBeam 모드에서는 사용 안 함.")]
    [SerializeField] private GameObject dashTrailEffect;

    [Tooltip("이펙트 자동 제거 시간(초). 0 이하이면 자동 제거 안 함(프리팹이 스스로 정리).")]
    [SerializeField] private float dashTrailLifetime = 0.4f;

    [Tooltip("이펙트 경로의 세로 위치 보정(월드 Y). 캐릭터 기준점이 발이라 낮게 보일 때 올려줌.")]
    [SerializeField] private float dashTrailHeightOffset = 0.3f;

    [Tooltip("이펙트 자체 애니메이션(예: Fire Bolt) 재생 속도 배율. 1 = 프리팹 원본 속도, 2 = 2배 빠름.")]
    [SerializeField] private float dashTrailAnimSpeed = 1f;

    [Tooltip("체크 시 이펙트 애니메이션 속도를 처형 속도(harvestSpeed)에 맞춤. 위 배율까지 함께 곱해짐.")]
    [SerializeField] private bool matchHarvestSpeed = true;

    [Header("- SingleStretched 옵션")]
    [Tooltip("이펙트를 시작→도착 거리에 맞춰 X축으로 늘릴지 여부. 프리팹을 '가로 1유닛' 폭으로 맞춰두면 거리에 정확히 들어맞음.")]
    [SerializeField] private bool stretchToDistance = true;

    [Header("- RepeatedAlongPath 옵션")]
    [Tooltip("경로를 따라 균등 간격으로 생성할 잔상 개수")]
    [SerializeField] private int trailCount = 5;

    [Header("- LineBeam 옵션 (깨끗한 선/빔)")]
    [Tooltip("빔에 사용할 머티리얼. 비워두면 기본 스프라이트 머티리얼로 단색 빔 생성. 화염/에너지 머티리얼을 넣으면 그 질감이 적용됨.")]
    [SerializeField] private Material lineBeamMaterial;

    [Tooltip("빔 색상 (알파로 투명도 조절). 기본은 흰색.")]
    [SerializeField] private Color lineBeamColor = Color.white;

    [Tooltip("빔 두께(월드 유닛). 가장 굵은 부분 기준.")]
    [SerializeField] private float lineBeamWidth = 0.2f;

    [Tooltip("빔 모양. Rectangle=네모, PointedEnds=양끝 뾰족(슬래시), PointedTail/Head=한쪽만 뾰족.")]
    [SerializeField] private LineBeamShape lineBeamShape = LineBeamShape.PointedEnds;

    [Tooltip("체크 시 수명 동안 서서히 사라짐(페이드아웃).")]
    [SerializeField] private bool lineBeamFadeOut = true;

    private Material _defaultLineMat;

    private bool isHarvesting = false;

    private void Awake()
    {
        Instance = this;
    }

    public void ExecuteHarvest(
        Enemy enemy,
        Transform player,
        Animator playerAnimator,
        SpriteRenderer playerSprite)
    {
        if (isHarvesting)
            return;

        if (enemy == null)
        {
            Debug.LogError("Harvest 실패: enemy가 null입니다.");
            return;
        }

        if (player == null)
        {
            Debug.LogError("Harvest 실패: player가 null입니다.");
            return;
        }

        if (playerAnimator == null)
        {
            Debug.LogError("Harvest 실패: playerAnimator가 null입니다.");
            return;
        }

        if (playerSprite == null)
        {
            Debug.LogError("Harvest 실패: playerSprite가 null입니다.");
            return;
        }

        // '적 좌표로 순간이동' 모드가 아닐 때만 BackPosition 필요
        PlayerCombat combat = player.GetComponent<PlayerCombat>();
        bool ontoTarget = combat != null && combat.HarvestTeleportOntoTarget;
        if (!ontoTarget && enemy.BackPosition == null)
        {
            Debug.LogError("Harvest 실패: enemy.BackPosition이 null입니다.");
            return;
        }

        StartCoroutine(HarvestCoroutine(enemy, player, playerAnimator, playerSprite));
    }

    private IEnumerator HarvestCoroutine(
        Enemy enemy,
        Transform player,
        Animator playerAnimator,
        SpriteRenderer playerSprite)
    {
        isHarvesting = true;

        PlayerMove playerMove = player.GetComponent<PlayerMove>();

        if (playerMove != null)
        {
            playerMove.isExecuting = true;
        }

        // 순간이동 시작/도착 위치
        // OntoTarget(예: Reaper): 적 좌표로 / 기본: 적 뒤로
        PlayerCombat combat = player.GetComponent<PlayerCombat>();
        bool ontoTarget = combat != null && combat.HarvestTeleportOntoTarget;

        Vector3 startPos = player.position;
        Vector3 endPos = ontoTarget
            ? enemy.transform.position + onTargetOffset
            : enemy.BackPosition.position + new Vector3(0f, -0.5f, 0f);

        // 순간이동
        player.position = endPos;

        // 순간이동 경로(시작→도착)에 섬광/잔상 이펙트 생성
        SpawnDashTrail(startPos, endPos);

        // 플레이어가 몬스터를 바라보게 방향 설정
        Vector2 dir = enemy.transform.position - player.position;

        if (dir.x != 0)
        {
            playerSprite.flipX = dir.x < 0;
        }

        // 처형 속도 배율에 맞춰 애니메이션/타이밍 스케일
        float speed = Mathf.Max(0.01f, harvestSpeed);
        float scaledDuration = harvestDuration / speed;

        // 플레이어 처형 애니메이션
        playerAnimator.SetFloat("HarvestSpeed", speed);
        playerAnimator.SetTrigger("Harvest");

        // 처형으로 준 피해 = 적의 남은 체력. HarvestDie 전에 미리 확보해 둔다.
        int harvestDamage = Mathf.Max(1, enemy.hp);
        Vector3 impactPos = enemy.transform.position;

        // 기존 동작: 처형 시작과 동시에 문구 표시
        if (!harvestTextOnImpact)
            ShowHarvestText(impactPos);

        // 처형 보상: 소울 획득 (스킬 자원)
        PlayerStatus playerStatus = player.GetComponent<PlayerStatus>();
        if (playerStatus != null)
        {
            int soulGain = (enemy.Info != null && enemy.Info.Soul > 0) ? enemy.Info.Soul : defaultSoulGain;
            playerStatus.AddSoul(soulGain);
        }

        // 몬스터 사망 애니메이션 + scaledDuration 뒤 제거
        enemy.HarvestDie(scaledDuration);

        // 나중에 여기 근처에 soul stack 증가 넣으면 됨
        // 예: playerStatus.AddSoulStack(1);

        // ── 내려찍기가 닿는 순간까지 대기 ──
        float impactDelay = Mathf.Clamp(impactTime / speed, 0f, scaledDuration);
        if (impactDelay > 0f)
            yield return new WaitForSeconds(impactDelay);

        // 적이 아직 살아 있으면 최신 위치를 쓴다 (넉백 등으로 밀렸을 수 있음)
        if (enemy != null)
            impactPos = enemy.transform.position;

        if (showDamageOnImpact && DamageNumberSpawner.Instance != null)
            DamageNumberSpawner.Instance.Show(impactPos, harvestDamage, impactDamageAsCritical);

        if (harvestTextOnImpact)
            ShowHarvestText(impactPos + new Vector3(0f, harvestTextExtraHeight, 0f));

        if (shakeOnImpact)
            CameraShake.Shake(impactShake);

        // 처형 파생 효과(충격파 등)가 붙는 지점.
        // HarvestManager는 어떤 카드가 무엇을 하는지 몰라야 하므로 "일어났다"만 알린다.
        if (ImpactLanded != null)
            ImpactLanded(new HarvestImpact(impactPos, enemy));

        // ── 남은 시간 대기 ──
        float remain = scaledDuration - impactDelay;
        if (remain > 0f)
            yield return new WaitForSeconds(remain);

        if (playerMove != null)
        {
            playerMove.isExecuting = false;
        }

        isHarvesting = false;
    }

    private void ShowHarvestText(Vector3 worldPos)
    {
        if (DamageNumberSpawner.Instance != null)
            DamageNumberSpawner.Instance.ShowText(worldPos, "HARVEST!");
    }

    // 순간이동 시작→도착 경로에 섬광/잔상 이펙트를 생성한다.
    private void SpawnDashTrail(Vector3 startPos, Vector3 endPos)
    {
        // 캐릭터 기준점(발) 보정: 경로 전체를 세로로 올림
        Vector3 heightOffset = new Vector3(0f, dashTrailHeightOffset, 0f);
        startPos += heightOffset;
        endPos += heightOffset;

        Vector3 delta = endPos - startPos;
        float distance = delta.magnitude;
        if (distance < 0.001f) return;

        // LineBeam 모드: 두 점을 잇는 직선 빔 (프리팹 불필요)
        if (dashTrailMode == DashTrailMode.LineBeam)
        {
            SpawnLineBeam(startPos, endPos);
            return;
        }

        if (dashTrailEffect == null) return;

        // 이펙트가 진행 방향(시작→도착)을 향하도록 Z축 회전
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);

        if (dashTrailMode == DashTrailMode.SingleStretched)
        {
            Vector3 mid = (startPos + endPos) * 0.5f;
            GameObject fx = Instantiate(dashTrailEffect, mid, rot);
            ApplyTrailAnimSpeed(fx);

            if (stretchToDistance)
            {
                // 프리팹 기본 X스케일에 거리를 곱해 경로 길이만큼 늘림
                Vector3 s = fx.transform.localScale;
                s.x *= distance;
                fx.transform.localScale = s;
            }

            if (dashTrailLifetime > 0f) Destroy(fx, dashTrailLifetime);
        }
        else // RepeatedAlongPath
        {
            int count = Mathf.Max(1, trailCount);
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : (float)i / (count - 1);
                Vector3 pos = Vector3.Lerp(startPos, endPos, t);

                GameObject fx = Instantiate(dashTrailEffect, pos, rot);
                ApplyTrailAnimSpeed(fx);
                if (dashTrailLifetime > 0f) Destroy(fx, dashTrailLifetime);
            }
        }
    }

    // 생성된 이펙트의 Animator 재생 속도를 설정한다. (처형 속도와 이펙트 애니메이션을 맞추기 위함)
    private void ApplyTrailAnimSpeed(GameObject fx)
    {
        float animSpeed = dashTrailAnimSpeed;
        if (matchHarvestSpeed)
            animSpeed *= Mathf.Max(0.01f, harvestSpeed);

        Animator[] animators = fx.GetComponentsInChildren<Animator>(true);
        foreach (Animator a in animators)
            a.speed = animSpeed;
    }

    // 시작→끝을 잇는 직선 빔(LineRenderer)을 생성한다.
    private void SpawnLineBeam(Vector3 startPos, Vector3 endPos)
    {
        GameObject beam = new GameObject("HarvestLineBeam");
        LineRenderer lr = beam.AddComponent<LineRenderer>();

        lr.useWorldSpace = true;

        // 폭 곡선은 '위치점'에서만 샘플링되므로, 선을 여러 점으로 잘게 나눠야
        // 뾰족한(가운데 굵은) 모양이 제대로 그려진다.
        const int segments = 16;
        lr.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            lr.SetPosition(i, Vector3.Lerp(startPos, endPos, t));
        }

        lr.widthMultiplier = lineBeamWidth;
        lr.widthCurve = BuildBeamWidthCurve(lineBeamShape);
        lr.numCapVertices = 4;
        lr.alignment = LineAlignment.View;      // 2D 카메라를 향하도록
        lr.textureMode = LineTextureMode.Stretch;

        // 머티리얼 (지정 없으면 기본 스프라이트 머티리얼 생성해 재사용)
        Material mat = lineBeamMaterial;
        if (mat == null)
        {
            if (_defaultLineMat == null)
                _defaultLineMat = new Material(Shader.Find("Sprites/Default"));
            mat = _defaultLineMat;
        }
        lr.material = mat;

        lr.startColor = lineBeamColor;
        lr.endColor = lineBeamColor;

        // 정렬: 스킬 레이어 위로 (플레이어·적보다 앞)
        lr.sortingLayerName = "Skill";
        lr.sortingOrder = 10;

        if (dashTrailLifetime > 0f)
        {
            if (lineBeamFadeOut)
                StartCoroutine(FadeLineBeam(lr, beam, dashTrailLifetime));
            else
                Destroy(beam, dashTrailLifetime);
        }
    }

    // 빔 모양에 맞는 폭 곡선(0~1)을 만든다. widthMultiplier와 곱해져 실제 두께가 됨.
    private AnimationCurve BuildBeamWidthCurve(LineBeamShape shape)
    {
        switch (shape)
        {
            case LineBeamShape.PointedEnds:
                return new AnimationCurve(
                    new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));
            case LineBeamShape.PointedTail:
                return new AnimationCurve(
                    new Keyframe(0f, 0f), new Keyframe(1f, 1f));
            case LineBeamShape.PointedHead:
                return new AnimationCurve(
                    new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            default: // Rectangle
                return new AnimationCurve(
                    new Keyframe(0f, 1f), new Keyframe(1f, 1f));
        }
    }

    // 빔을 수명 동안 서서히 사라지게 한 뒤 제거.
    private IEnumerator FadeLineBeam(LineRenderer lr, GameObject beam, float duration)
    {
        float t = 0f;
        Color baseColor = lineBeamColor;

        while (t < duration)
        {
            if (lr == null || beam == null) yield break;

            t += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, t / duration);

            Color c = baseColor;
            c.a = baseColor.a * a;
            lr.startColor = c;
            lr.endColor = c;

            yield return null;
        }

        if (beam != null) Destroy(beam);
    }
}