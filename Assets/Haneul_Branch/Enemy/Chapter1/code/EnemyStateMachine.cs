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
        CurrentState.Enter();
    }

    public void ChangeState(IEnemyState newState)
    {
        if (newState == null)
        {
            return;
        }

        CurrentState?.Exit(); //사용하던 상태를 나감

        CurrentState = newState;

        CurrentState.Enter();//바꿀 상태로 들어감
    }

    public void Update()
    {
        CurrentState?.Update(); //현재 상태가 null이 아니면 계속 업데이트 메서드 실행
    }
}