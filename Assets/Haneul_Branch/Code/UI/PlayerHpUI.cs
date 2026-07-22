using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHpUI : MonoBehaviour
{
    [SerializeField] private PlayerStatus playerStatus;
    [SerializeField] private Image fillImage;

    [SerializeField] private Text hpText;

    void Awake()
    {
        // 참조 미할당 시 "Player" 태그로 자동 연결 → 씬마다 수동 연결 불필요
        if (playerStatus == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerStatus = p.GetComponentInParent<PlayerStatus>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (playerStatus == null) return;

        if (fillImage != null)
            fillImage.fillAmount = (float)playerStatus.CurrentHp / playerStatus.MaxHp;

        if (hpText != null)
            hpText.text = $"{playerStatus.CurrentHp}/{playerStatus.MaxHp}";
    }
}
