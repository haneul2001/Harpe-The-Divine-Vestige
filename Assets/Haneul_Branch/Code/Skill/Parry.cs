using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Parry", menuName = "PlayerSkill/Parry")]
public class Parry : PlayerSkill
{
    public override void Activate(Player player)
    {
        player.StartParry(1.0f); // Example duration, adjust as needed
    }
}
