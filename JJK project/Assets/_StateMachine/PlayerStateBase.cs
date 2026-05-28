/// <summary>
/// Abstract base for all player states.
/// States do their work in OnUpdate and set flags on the blackboard.
/// They do NOT decide transitions — PlayerController owns that.
/// </summary>
public abstract class PlayerStateBase
{
    protected PlayerBlackboard Blackboard;

    public void Init(PlayerBlackboard blackboard) => Blackboard = blackboard;

    /// <summary>Called once when this state becomes active.</summary>
    public virtual void OnEnter() { }

    /// <summary>Called every frame while this state is active.</summary>
    public virtual void OnUpdate() { }

    /// <summary>Called once when this state is deactivated.</summary>
    public virtual void OnExit() { }
}
