using UnityEngine;

// 처형 가능 표시의 모양 한 벌.
//
// 표시(Harvest_image)는 몬스터 프리팹 13개에 각각 들어 있다. 그림을 바꿀 때마다 프리팹을 전부
// 고치지 않도록, HarvestImageController가 켜질 때 Resources/UI/HarvestMarkerStyle 을 읽어 입힌다.
// 이 에셋이 없으면 프리팹에 있던 원래 표시가 그대로 나온다.
[CreateAssetMenu(fileName = "HarvestMarkerStyle", menuName = "Harpe/UI/Harvest Marker Style")]
public class HarvestMarkerStyle : ScriptableObject
{
    public const string ResourcePath = "UI/HarvestMarkerStyle";

    [Tooltip("표시 그림 (해골)")]
    public Sprite icon;
    [Tooltip("표시 크기 (월드 단위, 몬스터 배율과 무관)")]
    public float iconWorldSize = 0.55f;
    [Tooltip("프리팹에 놓인 원래 표시 위치에서 위아래로 옮기는 거리 (월드 단위, 음수면 아래)")]
    public float iconOffsetY = 0f;

    [Tooltip("표시 뒤에서 계속 재생할 이펙트 (루프 파티클)")]
    public GameObject backEffect;
    [Tooltip("이펙트 재질을 이걸로 바꿔 끼운다 (색만 바꾼 복제본용). 비우면 원래 재질")]
    public Material backEffectMaterial;
    [Tooltip("이펙트 크기 배율")]
    public float backEffectScale = 1f;
    [Tooltip("표시 중심 기준 이펙트 위치 (월드 단위)")]
    public Vector2 backEffectOffset = Vector2.zero;
}
