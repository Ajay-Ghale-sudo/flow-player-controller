namespace Flow;

public partial class Controller
{
	public abstract class Integrator : Component
	{
		[RequireComponent] public Controller Controller { get; set; }

		public virtual bool ColliderEnabled => true;

		public virtual void PreMove() { }
		public virtual void PostMove() { }
		public abstract void PrePhysicsStep();
		public abstract void PostPhysicsStep();

		public virtual void OnCollisionStart( Collision collision ) { }
		public virtual void OnCollisionUpdate( Collision collision ) { }
		public virtual void OnCollisionStop( CollisionStop collision ) { }

		public interface IEvents : ISceneEvent<IEvents>
		{
			void OnLand( GameObject gameObject, float landSpeed ) { }
		}

		protected void Post( Action<IEvents> action ) { IEvents.PostToGameObject( GameObject, action ); }

	}
}
