using UnityEngine;

public class Player : MonoBehaviour
{
    public PlayerMove playerMove;
    public PlayerCombat playerCombat;
    public PlayerStatus playerStatus;

    void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
        playerCombat = GetComponent<PlayerCombat>();
        playerStatus = GetComponent<PlayerStatus>();
    }
}
