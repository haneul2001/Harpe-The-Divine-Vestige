using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHpUI : MonoBehaviour
{
    [SerializeField] private PlayerStatus playerStatus;
    [SerializeField] private Image fillImage;

    [SerializeField] private Text hpText;

    // Update is called once per frame
    void Update()
    {
        fillImage.fillAmount =
        (float)playerStatus.CurrentHp/playerStatus.MaxHp;
    
        hpText.text =
            $"{playerStatus.CurrentHp}/{playerStatus.MaxHp}";
    }
}
