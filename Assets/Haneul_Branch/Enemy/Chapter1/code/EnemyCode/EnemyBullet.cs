using UnityEngine;

// 적 원거리 공격 투사체. 직선으로 날아가 플레이어에 데미지, 벽/수명에 소멸.
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBullet : MonoBehaviour
{
    [Tooltip("자동 소멸 시간(초)")]
    [SerializeField] private float lifeTime = 5f;
    [Tooltip("이 레이어(벽)에 닿으면 소멸")]
    [SerializeField] private LayerMask wallLayer;

    private int damage;
    private Enemy owner;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // 발사 초기화: 방향, 속도, 데미지, 발사한 적(패링 반사 대상)
    public void Init(Vector2 direction, float speed, int damage, Enemy owner)
    {
        this.damage = damage;
        this.owner = owner;

        Vector2 dir = direction.normalized;
        rb.velocity = dir * speed;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 이펙트를 자식으로 붙여 같이 날아가게 한다. 원래 스프라이트는 자리만 차지하므로 끈다.
        var own = GetComponent<SpriteRenderer>();
        if (own != null) own.enabled = false;
        PixelVfx.Play("BatShot", transform.position, angle, transform);

        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어 피격
        PlayerStatus ps = other.GetComponentInParent<PlayerStatus>();
        if (ps != null)
        {
            ps.TakeDamage(damage, owner);
            Destroy(gameObject);
            return;
        }

        // 벽에 닿으면 소멸
        if (((1 << other.gameObject.layer) & wallLayer) != 0)
        {
            Destroy(gameObject);
        }
    }
}
