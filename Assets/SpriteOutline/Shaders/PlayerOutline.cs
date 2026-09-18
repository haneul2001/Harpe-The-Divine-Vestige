using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerOutline : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRender;
    private Material mat;
    private readonly Color normalColor = Color.white;
    private readonly Color superArmorColor = Color.yellow;

 void Awake()
{
    GameObject player =
        GameObject.FindGameObjectWithTag("Player");

    if (player == null)
    {
        Debug.LogError("Player를 찾을 수 없음");
        return;
    }

    spriteRender =
        player.GetComponentInChildren<SpriteRenderer>();

    if (spriteRender == null)
    {
        Debug.LogError("SpriteRenderer를 찾을 수 없음");
        return;
    }

    mat = spriteRender.material;

    SetOutlineColor(normalColor);
}

    public void StartAttackOutline()
    {
        SetOutlineColor(superArmorColor);
    }
    public void EndAttackOutline()
    {
        SetOutlineColor(normalColor);
    }
    private void SetOutlineColor(Color color)
    {
        mat.SetColor("_OutlineColorBase", color);
    }

}
