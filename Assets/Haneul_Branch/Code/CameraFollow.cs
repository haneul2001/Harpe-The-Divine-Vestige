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

    // Update is called once per frame
    void LateUpdate()
{
    if (player == null) return;
    if (CameraPoint_xMin == null || CameraPoint_xMax == null ||
        CameraPoint_yMin == null || CameraPoint_yMax == null) return;

    float clampX = Mathf.Clamp(
        player.position.x,
        CameraPoint_xMin.position.x,
        CameraPoint_xMax.position.x);

    float clampY = Mathf.Clamp(
        player.position.y,
        CameraPoint_yMin.position.y,
        CameraPoint_yMax.position.y);

    // Debug.Log(
    //     $"PlayerY={player.position.y} " +
    //     $"Min={CameraPoint_yMin.position.y} " +
    //     $"Max={CameraPoint_yMax.position.y} " +
    //     $"Clamp={clampY}");

    transform.position = new Vector3(
        clampX, clampY, -40f);
}
}
