using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    Vector2 position;
    public Transform player;
    public Transform CameraPoint_xMin;
    public Transform CameraPoint_xMax;
    public Transform CameraPoint_yMin;
    public Transform CameraPoint_yMax;

    void Awake()
    {
        // 참조 미할당 시 "Player" 태그로 자동 연결 → 씬마다 수동 연결 불필요
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void Start()
    {
        if (player == null)
            Debug.LogWarning("[CameraFollow] player 미할당 & 'Player' 태그 오브젝트 없음");
    }

    // 방(Room) 단위로 카메라 경계를 갈아끼운다. RoomManager가 방 이동 시 호출.
    public void SetBounds(Transform xMin, Transform xMax, Transform yMin, Transform yMax)
    {
        CameraPoint_xMin = xMin;
        CameraPoint_xMax = xMax;
        CameraPoint_yMin = yMin;
        CameraPoint_yMax = yMax;
    }

    // 경계를 바꾼 직후 다음 프레임을 기다리지 않고 즉시 위치를 맞춘다 (방 전환 시 한 프레임 어긋남 방지).
    public void SnapToTarget()
    {
        LateUpdate();
    }

    // 방 전환 슬라이드처럼 외부가 카메라를 직접 몰아야 할 때 추적을 잠시 멈춘다.
    // 켜져 있는 동안은 OverridePosition을 기준 위치로 쓴다.
    // transform.position을 그대로 읽으면 이미 얹힌 흔들림 오프셋이 매 프레임 누적돼 카메라가 흘러간다.
    public bool Suspended { get; set; }
    public Vector3 OverridePosition { get; set; }

    // 지금 경계 기준으로 카메라가 있어야 할 자리. 슬라이드의 도착점을 미리 구할 때 쓴다.
    public bool TryGetTargetPosition(out Vector3 result)
    {
        result = transform.position;

        if (player == null) return false;
        if (CameraPoint_xMin == null || CameraPoint_xMax == null ||
            CameraPoint_yMin == null || CameraPoint_yMax == null) return false;

        float clampX = Mathf.Clamp(
            player.position.x,
            CameraPoint_xMin.position.x,
            CameraPoint_xMax.position.x);

        float clampY = Mathf.Clamp(
            player.position.y,
            CameraPoint_yMin.position.y,
            CameraPoint_yMax.position.y);

        result = new Vector3(clampX, clampY, -40f);
        return true;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        Vector3 pos;

        // 슬라이드 중에는 위치를 연출이 정한다. 흔들림만 그 위에 얹어 준다.
        if (Suspended) pos = OverridePosition;
        else if (!TryGetTargetPosition(out pos)) return;

        // 카메라 흔들림은 위치를 정한 뒤 마지막에 얹는다.
        // CameraShake는 Update에서 값을 갱신하므로 여기(LateUpdate)선 항상 최신값이다.
        if (CameraShake.Instance != null)
        {
            pos += CameraShake.Instance.Offset;
            transform.rotation = Quaternion.Euler(0f, 0f, CameraShake.Instance.Roll);
        }

        transform.position = pos;
    }
}
