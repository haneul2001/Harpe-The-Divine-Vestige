using UnityEngine;

public class AttackRangeSet : MonoBehaviour
{
    public void SetDirection(Vector2 direction)
    {
        float angle =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        Transform enemy = transform.parent;

        if (enemy != null && enemy.eulerAngles.y > 90f)
        {
            angle = 180f - angle;
        }

        transform.localRotation =
            Quaternion.Euler(0f, 0f, angle);
    }
}