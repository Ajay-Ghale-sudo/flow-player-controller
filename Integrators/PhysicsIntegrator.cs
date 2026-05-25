namespace Flow.Integrators;

[Title( "Flow Physical Integrator" ), Icon( "fitness_Center" ), Hide]
public class PhysicalIntegrator : Controller.Integrator
{
	public override void PrePhysicsStep()
	{
		var posDelta = Controller.Position - WorldPosition;
		var bodyVel = posDelta / Time.Delta;
		DebugOverlay.ScreenText(new(120f, 50f), $"PhysicsPreStep posDelta={posDelta} vel={bodyVel}", 14f, TextFlag.LeftBottom, Color.Yellow, 0.1f);
		if ( bodyVel.Length > 40f || posDelta.Length > 1f )
		{
			Log.Info($"Physics PrePhysicsStep posDelta={posDelta} bodyVel={bodyVel} world={WorldPosition} pos={Controller.Position} vel={Controller.Velocity}");
		}
		Controller.Body.Velocity = bodyVel;
		Controller.Body.Sleeping = false;
	}

	public override void PostPhysicsStep()
	{
		Controller.Velocity = Controller.Body.Velocity;
	}

	public override void OnCollisionStart( Collision collision )
	{
		
	}

	public override void OnCollisionStop( CollisionStop collision )
	{
		
	}
}
