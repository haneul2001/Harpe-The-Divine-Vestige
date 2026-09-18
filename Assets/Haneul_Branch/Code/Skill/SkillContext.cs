using UnityEngine;

public class SkillContext
{
    public Player Player { get; }
    public PlayerCombat Combat { get; }
    public PlayerMove Move { get; }
    public PlayerStatus Status { get; }
    public Animator Animator { get; }
    public Rigidbody2D Rigidbody { get; }
    public SpriteRenderer Sprite { get; }
    public MonoBehaviour CoroutineRunner { get; }
    public Transform Transform { get; }

    public SkillContext(Player player)
    {
        Player = player;
        Combat = player.playerCombat;
        Move = player.playerMove;
        Status = player.playerStatus;
        Animator = player.GetComponentInChildren<Animator>();
        Rigidbody = player.GetComponent<Rigidbody2D>();
        Sprite = player.GetComponentInChildren<SpriteRenderer>();
        CoroutineRunner = player;
        Transform = player.transform;
    }
}
