namespace Flow.States;

using Sandbox;

[Title("Flow Runner Default State"), Group("Flow/States/Runner"), Icon("skateboarding")]
public partial class Runner : Controller.State
{
	[RequireComponent] public FrameOfReferenceIntegrator FrameOfReferenceIntegrator { get; set; }
	public override Controller.Integrator Integrator => FrameOfReferenceIntegrator;

	[RequireComponent] public Look3d Look3d { get; set; }

	private float _smoothedYawVel;

	public override void Process()
	{
		if ( Controller.IsOnGround ) CoyoteTimeTimer = 0f;
	}

	public override void Move()
	{
		ThinkSprint();
		ThinkCrouch();
		ThinkLurch();

		TryJump( JumpHeight );

		if ( Controller.IsOnGround )
		{
			Decelerate();
			Accelerate();
		}
		else AirAccelerate();

		ThinkMomentum();
		ThinkMomentumFov();
	}

// maybe this should go somewhere else
	void Look3d.IEvents.ModifyEyeAngles( Look3d module, ref Angles eyeAngles, in Angles input, in Angles oldEyeAngles )
	{
		if ( !IsCurrent ) { return; }
		var m = Controller.Momentum;
		if ( m <= 0.01f ) { return; }

		var drag = MomentumCameraDrag * m;
		eyeAngles.yaw = MathX.Lerp( eyeAngles.yaw, oldEyeAngles.yaw, drag );
		eyeAngles.pitch = MathX.Lerp( eyeAngles.pitch, oldEyeAngles.pitch, drag * 0.6f );

		var yawDelta = Angles.NormalizeAngle( eyeAngles.yaw - oldEyeAngles.yaw );
		var alpha = 1f - MathF.Exp( -8f * Time.Delta );
		_smoothedYawVel = MathX.Lerp( _smoothedYawVel, yawDelta / MathF.Max( Time.Delta, 1e-4f ), alpha );

		var turnRateNorm = (_smoothedYawVel / 180f).Clamp( -1f, 1f );
		var targetRoll = -turnRateNorm * MomentumCameraLean * m;
		eyeAngles.roll = eyeAngles.roll.ApproachWithStopSpeed( targetRoll, 0.1f, module.RollDecay );
		
	}
}
