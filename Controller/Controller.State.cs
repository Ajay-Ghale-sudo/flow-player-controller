namespace Flow;

public partial class Controller
{
	[Property, Feature( "Debug" ), Title( "State" ), Sync, ReadOnly]
	public State CurrentState { get; set; } = null;
	[Property, Feature( "Debug" ), ReadOnly]
	protected State NextState { get; set; } = null;

	public Integrator CurrentIntegrator => CurrentState?.Integrator;

	public void SetNextState( State state )
	{
		if ( !state.IsValid() ) return;
		if ( NextState.IsValid() ) return;
		NextState = state;
	}
	
	protected void CheckNextState()
	{
		if ( !NextState.IsValid() )
		{
			foreach ( var state in GetComponents<State>() )
			{
				if ( state == CurrentState ) { continue; }
				if ( state.TryEnter() )
				{
					NextState = state;
					break;
				}
			}
		}
		if (!NextState.IsValid()) { return; }
		CurrentState?.Exit();
		CurrentState = NextState;
		NextState = null;
		CurrentState?.Enter();
	}

	public abstract class State : Component, IEvents
	{
		[RequireComponent] public Controller Controller { get; set; }

		public bool IsCurrent => Controller.CurrentState == this;
		public void SetNextState( State state ) => Controller.SetNextState( state );

		public abstract Integrator Integrator { get; }

		public virtual void Process() { }
		public abstract void Move();
		public virtual bool TryEnter() => false;
		public virtual void Enter() { }
		public virtual void Exit() { }
	}
}
