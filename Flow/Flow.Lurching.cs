namespace Flow.States;

public partial class Runner
{
	[Property, FeatureEnabled( "Lurch" ), Order( 1000 )]
	public bool Lurches { get; set; } = true;

	[Property, FeatureEnabled( "Lurch" ), Order( 1000 ), DefaultValue( ScrollDirection.Down )]
	public ScrollDirection? ScrollLurch { get; set; } = ScrollDirection.Down;

	[Property, FeatureEnabled( "Lurch" ), Order( 1000 )]
	public float LurchTimeMin { get; set; } = 0.2f;
	[Property, FeatureEnabled( "Lurch" ), Order( 1000 )]
	public float LurchTimeMax { get; set; } = 0.5f;
	[Property, FeatureEnabled( "Lurch" ), Order( 1000 )]
	public float LurchMax { get; set; } = 0.7f;
	[Property, FeatureEnabled( "Lurch" ), Order( 1000 )]
	public float LurchStrength { get; set; } = 0.7f;

	public TimeSince LurchTimer = float.PositiveInfinity;

	public bool WantScrollLurch => !IsProxy && ScrollLurch.HasValue && (ScrollLurch.Value switch
	{
		ScrollDirection.Up => Input.MouseWheel.y > 0,
		ScrollDirection.Down => Input.MouseWheel.y < 0,
		_ => throw new Exception( "how did you screw up scrollwheel its 2 directions" ),
	});

	public void ThinkLurch()
	{
		if ( !ScrollLurch.HasValue || Controller.IsOnGround ) { return; }

		var lurchPower = ((float)LurchTimer).Remap( LurchTimeMin, LurchTimeMax, 1f, 0f );
		if ( lurchPower <= 0f ) { return; }

		if ( !(WantScrollLurch || Input.Pressed( "forward" ) || Input.Pressed( "down" ) || Input.Pressed( "right" ) || Input.Pressed( "left" )) )
		{
			return;
		}

		Lurch( lurchPower );
	}

	public void ResetLurchTimer() { LurchTimer = 0f; }
	public void DropLurchTimer() { LurchTimer = float.PositiveInfinity; }

	public Vector3 GetLurchWishDir()
	{
		var wishDirectionUnrotated = Look3d.WishDirectionUnrotated;
		if ( WantScrollLurch )
		{
			wishDirectionUnrotated = Input.AnalogMove;
			wishDirectionUnrotated.x = MathF.Min( wishDirectionUnrotated.x + 1f, 1f );
			wishDirectionUnrotated = wishDirectionUnrotated.ClampLength( 1f );
		}

		if ( wishDirectionUnrotated.IsNearZeroLength ) { return Vector3.Zero; }

		return (wishDirectionUnrotated * Look3d.HorizontalLook).ClampLength( 1f );
	}

	public void Lurch(float lurchPower)
	{
		var (horizontalVelocity, verticalVelocity) = Controller.Velocity.SplitByNormal( WorldTransform.Up );

		var wishDirection = GetLurchWishDir();
		if ( wishDirection.IsNearZeroLength ) { return; }
		
		// lurch_wishdir = normalize(<button directions> * <camera angles> + <button directions> * <another set of angles that are 0? Moving platform maybe?>)
        // lurch_vec = (lurch_wishdir - normalized_horizontal_velocity) * lurch_scale_factor + normalized_horizontal_velocity
        var lurchVec = (wishDirection - horizontalVelocity.Normal) * lurchPower * LurchStrength + horizontalVelocity.Normal;

        // if (|lurch_vec| > 0.01): // What?
        //     scale = |current_velocity| / |lurch_vec|
        //     lurch_vec *= scale
        if (lurchVec.LengthSquared > 0.00001f) {
            var scale = horizontalVelocity.Length / lurchVec.Length;
            lurchVec *= scale;
        }

        // if player_speed_multiplier < 0:
        //     // Some pointer shuffling, probably overrides use of `sprintspeed` later.

        // delta_vel = lurch_vec - current_velocity
        var deltaVel = lurchVec - horizontalVelocity;

        // max_speed_change = (jump_keyboardgrace_max * sprintspeed * player_speed_multiplier)
        var maxSpeedChange = LurchMax * SprintSpeed;

        // if |delta_vel| > max_vel_change:
        //     // Scale down the lurch
        //     scale_factor = max_vel_change / |delta_vel|
        //     delta_vel *= scale_factor
        deltaVel = deltaVel.ClampLength(maxSpeedChange);

        // // Update x and y velocities, restore z velocity unchanged from the stack
        horizontalVelocity += deltaVel;

        Controller.Velocity = horizontalVelocity + verticalVelocity;
	}
}
