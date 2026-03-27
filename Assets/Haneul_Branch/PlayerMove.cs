using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{

    Rigidbody2D rb;
    Animator anim;
    SpriteRenderer spriter;

    public float speed;
    float inputValue;
    bool isMoving;
    Vector2 moveVec;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriter = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        float moveX =Input.GetAxisRaw("Horizontal"); //-1 , 0, 1
        float moveY = Input.GetAxisRaw("Vertical");

        moveVec = new Vector2(moveX, moveY).normalized;


        if (moveX != 0)
        {
            spriter.flipX = moveX < 0;
        }

        isMoving = moveVec.magnitude != 0; // 벡터가 0이 아니면 트루

        if (moveVec !=Vector2.zero)
        {
            anim.SetBool("isRun", true);
        }
        else anim.SetBool("isRun", false);

    }

    void FixedUpdate()
    {
        Vector2 nextVec = new Vector2(moveVec.x, moveVec.y * 0.7f) * speed;
        rb.velocity = nextVec;

        if (nextVec != Vector2.zero)
        {
            anim.SetBool("isRun", true);
        }
        else anim.SetBool("isRun", false);
        
    } 
            
        
        
    
    
}
