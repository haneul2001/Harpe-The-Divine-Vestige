public interface IEnemyAttack
{
    bool IsRunning { get; }
    bool IsGroundOnly { get; }
    void Execute();
    void ShowRange(bool show);
}
