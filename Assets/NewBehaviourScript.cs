using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    Vector2 position;
    public Transform player;
    public Transform CameraPoint1;
    public Transform CameraPoint2;

    void Start()
    {
        
    }

    // Update is called once per frame
    void LateUpdate() 
    {
        float clampX = Mathf.Clamp(
            player.position.x,
            CameraPoint1.position.x,
            CameraPoint2.position.x);

        transform.position = new Vector3(
            clampX, player.position.y, -40f);
    }
}
