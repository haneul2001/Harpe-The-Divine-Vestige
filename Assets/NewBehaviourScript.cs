using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    Vector2 position;
    public Transform player;
    public Transform CameraPoint_xMin;
    public Transform CameraPoint_xMax;
    public Transform CameraPoint_yMin;
    public Transform CameraPoint_yMax;

    void Start()
    {
        Debug.Log(player.position);
        Debug.Log(CameraPoint_yMin.position);
        Debug.Log(CameraPoint_yMax.position);
    }

    // Update is called once per frame
    void LateUpdate()
{
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
