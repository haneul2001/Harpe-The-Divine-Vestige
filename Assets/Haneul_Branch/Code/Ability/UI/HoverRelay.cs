using UnityEngine;
using UnityEngine.EventSystems;

// 마우스가 들어오고 나가는 것만 콜백으로 넘겨 주는 부품.
// 칸마다 전용 MonoBehaviour를 만들지 않고 툴팁만 띄우고 싶을 때 쓴다 (ClickRelay와 같은 결).
public class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public System.Action onEnter;
    public System.Action onExit;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (onEnter != null) onEnter();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (onExit != null) onExit();
    }
}
