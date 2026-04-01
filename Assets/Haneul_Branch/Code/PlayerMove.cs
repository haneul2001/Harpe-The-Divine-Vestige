using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{

    Rigidbody2D rb;
    Animator anim;
    SpriteRenderer spriter;
    PlayerCombat combat;


    public float speed;
    Vector2 moveVec;
    
    void Awake()
    {
        combat = GetComponent<PlayerCombat>(); //플레이어 전투 관련 스크립트 참조
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriter = GetComponent<SpriteRenderer>();
    }

    void Update()
    {

        moveVec.x = Input.GetAxisRaw("Horizontal"); //입력은 업데이트에서 받음
        moveVec.y = Input.GetAxisRaw("Vertical");
        float yScale = moveVec.y != 0 ? 0.7f : 1f; 
        moveVec.y *= yScale;
        moveVec = moveVec.normalized; //대각선 이동 시 속도 보정
    }

    
    void FixedUpdate()
    {
        Move();
    } 
           
    void Move()
    {
        if (combat.isAttacking) { //공격 중에는 이동하지 않음
            rb.velocity = Vector2.zero;
            anim.SetBool("isRun", false);
            return;
        }

        rb.velocity = moveVec * speed * Time.deltaTime;

        if (!combat.isAttacking && moveVec.x != 0) //스프라이트의 좌우 컨트롤
        {
            spriter.flipX = moveVec.x < 0;
        }

        anim.SetBool ("isRun", moveVec != Vector2.zero);


    }

}
