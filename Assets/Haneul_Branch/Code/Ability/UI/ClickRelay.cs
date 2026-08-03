using UnityEngine;
using UnityEngine.EventSystems;

// 클릭을 콜백으로 넘겨 주는 최소 컴포넌트.
//
// Button을 쓰지 않는 이유: Button은 targetGraphic·전이(transition)·내비게이션을
// 요구하는데, 여기선 "눌렸다"는 사실만 있으면 된다.
// 자식 그래픽에서 올라온 클릭도 부모의 이 핸들러가 받는다.
public class ClickRelay : MonoBehaviour, IPointerClickHandler
{
    public System.Action onClick;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (onClick != null) onClick();
    }
}
