namespace Flow;

public partial class Controller : Controller.IEvents
{
	public interface IEvents : ISceneEvent<IEvents>
	{
		virtual void ModifyDimensions( Controller controller, ref float height, ref float radius ) { }
		virtual void ModifyMass( Controller controller, ref float mass ) { }
		virtual void ModifyGravity( Controller controller, ref Vector3 gravity ) { }
	}

	public void Post( Action<IEvents> action ) => IEvents.PostToGameObject( GameObject, action );
}
