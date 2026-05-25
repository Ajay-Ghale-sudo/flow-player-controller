namespace Flow.States;

public partial class Runner : Camera3d.IEvents
{
	/// <summary>
	/// Multiplier on the exponential decay rate when the player is moving
	/// uphill, scaled by slope steepness. Higher = cap bleeds away much faster
	/// on inclines. Effective uphill decay rate is
	/// <c>(base + unearned * deficit) * (1 + steepness * this)</c>.
	/// On downhills the same term *reduces* decay, clamped at 0.
	/// </summary>
	[Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
	public float MomentumSlopeDecayScale { get; set; } = 70f;

	/// <summary>
	/// Rate (u/s²) at which MomentumCap actively *grows* while descending a
	/// slope with forward input. Scaled by slope steepness. Downhill runs are
	/// the primary way to push cap past the flat-ground soft ceiling.
	/// </summary>
	[Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
	public float MomentumSlopeGainRate { get; set; } = 120f;

	/// <summary>
	/// Baseline exponential decay rate (per second) applied to earned
	/// momentum whenever <c>MomentumCap &gt; SprintSpeed</c>. Represents the
	/// "constant downward pressure" — even running perfectly at cap, this
	/// rate fights the build system, and together they produce the flat-ground
	/// equilibrium ceiling at <c>SprintSpeed + MomentumBuildRate / this</c>.
	/// </summary>
	[Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
	public float MomentumDecayBase { get; set; } = 1.75f;
	/// <summary>
	/// Additional decay rate added proportionally to how far *below* the cap
	/// the player is currently moving. Fully applied when stopped (deficit=1),
	/// zero when running at cap (deficit=0). This is what causes momentum to
	/// crash quickly when the player releases input or skids down.
	/// </summary>
	[Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
	public float MomentumDecayUnearned { get; set; } = 3f;
	[Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
	public float MomentumFOVScale { get; set; } = 1.0f;

	/// <summary>
	/// Smoothed FOV offset in degrees, read by the camera system. Updated every frame.
	/// </summary>
	public float MomentumFovOffset { get; private set; }

	/// <summary>
	/// The instantaneous (unsmoothed) target FOV offset. Useful for debugging
	/// or systems that want to snap rather than smooth.
	/// </summary>
	public float MomentumFovTarget { get; private set; }


	public void ThinkMomentum()
	{
		if ( !Controller.UseMomentum ) { return; }

		var horizontal = Controller.Velocity.WithZ( 0 ).Length;
		var slope = CalculateMovementSlopeDot();

		if ( horizontal > Controller.MomentumCap ) Controller.MomentumCap = horizontal;

		var cap = Controller.MomentumCap;
		var over = cap - SprintSpeed;

		if ( over > 0f )
		{
			var deficit = 1f - (horizontal / MathF.Max( cap, 1f )).Clamp( 0f, 1f );
			var slopeFactor = MathF.Max( 1f + (-slope) * MomentumSlopeDecayScale, 0f );

			var k = (MomentumDecayBase + MomentumDecayUnearned * deficit) * slopeFactor;
			var factor = MathF.Exp( -k * Time.Delta );
			cap = SprintSpeed + over * factor;
		}

		if ( SprintProgress > 0.95f && horizontal >= SprintSpeed - 10f && Input.AnalogMove.x > 0 && slope >= -0.1f ) { cap += MomentumBuildRate * Time.Delta; }
		if ( slope > 0f && Controller.IsOnGround && Input.AnalogMove.x > 0 ) { cap += MomentumSlopeGainRate * slope * Time.Delta; }

		Controller.MomentumCap = MathF.Max( cap, SprintSpeed );
	}
	
	public void ThinkMomentumFov()
	{
		var capExcess = Controller.MomentumCap - SprintSpeed;
		var frac = (capExcess / MathF.Max( MomentumReference, 1f )).Clamp( 0f, 1f );
		var target = frac * MomentumFovBoost;

		MomentumFovTarget = target;

		var rate = target > MomentumFovOffset ? MomentumFovRampIn : MomentumFovRampOut;
		MomentumFovOffset = MomentumFovOffset.LerpTo( target, rate * Time.Delta );

		//if ( Wallrun != null && Wallrun.IsCurrent)
	}

	public float CalculateMovementSlopeDot()
	{
		if ( !Controller.IsOnGround ) return 0f;

		var horizVel = Controller.Velocity.WithZ( 0 );
		if ( horizVel.IsNearZeroLength ) return 0f;

		var moveDir = horizVel.Normal;
		var alongGround = moveDir.SubtractDirection( Controller.GroundNormal ).Normal;
		return alongGround.Dot( -WorldTransform.Up );
	}

	void Camera3d.IEvents.ModifyCameraFov( Camera3d module, ref float fieldOfView )
	{
		fieldOfView += MomentumFovOffset;
	}
}
