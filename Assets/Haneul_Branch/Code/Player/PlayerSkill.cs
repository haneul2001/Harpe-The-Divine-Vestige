using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PlayerSkill : ScriptableObject
{
    public string skillName;
    public abstract void Activate(Player player);



}
