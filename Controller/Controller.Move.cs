namespace Flow;

public partial class Controller
{
	[FixedUpdateOnly] public Vector3 Position { get; set; } = Vector3.Zero;
	[Property, Feature( "Debug" ), ReadOnly, FixedUpdateOnly] public Vector3 Velocity { get; set; } = Vector3.Zero;


	void IScenePhysicsEvents.PrePhysicsStep()
	{
if ( Scene.IsEditor ) return;
		UpdateInternalElements();

		Position = WorldPosition;

		Collider.Enabled = CurrentIntegrator?.ColliderEnabled ?? true;
		Body.MotionEnabled = !IsProxy;

		if ( IsProxy ) return;

		CheckNextState();

		CurrentIntegrator?.PreMove();
		GetComponents<State>().ForEach( x => x.Process() );
		CurrentState?.Move();
		CurrentIntegrator?.PostMove();

		CurrentIntegrator?.PrePhysicsStep();
	}

	void IScenePhysicsEvents.PostPhysicsStep()
	{
		CurrentIntegrator?.PostPhysicsStep();
	}

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		CurrentIntegrator?.OnCollisionStart( collision );
	}

	void ICollisionListener.OnCollisionUpdate( Collision collision )
	{
		CurrentIntegrator?.OnCollisionUpdate( collision );
	}

	void ICollisionListener.OnCollisionStop( CollisionStop collision )
	{
		CurrentIntegrator?.OnCollisionStop( collision );
	}
}
