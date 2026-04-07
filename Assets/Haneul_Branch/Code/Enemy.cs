using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public int hp = 3;
    public void TakeDamage (int damage)
    {
        hp -= damage;
        if (hp <= 0)
        {
            Die();
        }
    }
    void Die()
    {
        Debug.Log("적이 죽었습니다.");
        Destroy(gameObject);
    }
}
