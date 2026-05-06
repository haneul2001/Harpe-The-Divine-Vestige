using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public PlayerMove playerMove;
    public PlayerCombat playerCombat;

    [SerializeField]
    private List<PlayerSkill> skills; 

    public enum PlayerState
    {
        Idle,
        Move,
        Attack,
        Parrying,
        Dash,
        SuperAmor,
        Charging
    }
    void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
        playerCombat = GetComponent<PlayerCombat>();

    }

    public void StartParry(float duration)
    {
        playerCombat.StartParry();
    }

}
