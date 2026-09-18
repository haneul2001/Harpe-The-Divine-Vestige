public class EnemyStateMachine
{
    public IEnemyState CurrentState { get; private set; }

    public void Initialize(IEnemyState startState) //처음 시작을 현재 상태로 설정하는 메서드
    {
        if (startState == null)
        {
            return;
        }

        CurrentState = startState;
        CurrentState.Enter();//스테이트의 Enter() 메서드 호출하여 상태가 시작될 때 필요한 초기화 작업 수행
    }

    public void ChangeState(IEnemyState newState)
    {   // empty state로 전환하는 것을 방지
        if (newState == null)
        {
            return;
        } 
        // 상태 전환 시 현재 상태의 Exit() 메서드를 호출하여 상태를 나가는 작업 수행
        CurrentState?.Exit(); //사용하던 상태를 나감
        
        CurrentState = newState; //새로운 상태로 전환

        CurrentState.Enter();//바꿀 상태로 들어감
    }

    public void Update()
    {
        CurrentState?.Update(); //현재 상태가 null이 아니면 계속 업데이트 메서드 실행
    }
}