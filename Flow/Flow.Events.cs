namespace Flow.States;

public partial class Runner
{
	public interface IEvents : ISceneEvent<IEvents>
	{
		void OnJump() { }
		void ModifyMaxSpeed( Runner runner, ref float maxSpeed ) { }
	}

	public void Post( Action<IEvents> action ) => IEvents.PostToGameObject( GameObject, action );
}
