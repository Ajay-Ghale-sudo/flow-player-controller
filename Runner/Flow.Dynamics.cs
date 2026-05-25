namespace Flow.States;

public partial class Runner
{
	public Vector3 WishVelocity => CalculateGroundMaxSpeed() * Look3d.WishDirectionHorizontal;

	public float CalculateGroundMaxSpeed()
	{
		float speed;
		if ( Crouch.IsCrouched ) { speed = CrouchSpeed; }
		else if ( !SprintActive ) { speed = WalkSpeed; }
		else if ( Input.AnalogMove.x <= 0 ) { speed = WalkSpeed; }
		else { speed = Controller.MomentumCap; }

		Post( x => x.ModifyMaxSpeed( this, ref speed ) );
		return speed;
	}

	public float CalculateSlopeFactor()
	{
		if ( !Controller.IsOnGround ) { return 1f; }

		var wishDir = Look3d.WishDirectionHorizontal;
		if ( wishDir.IsNearZeroLength ) { return 1f; }

		var groundNormal = Controller.GroundNormal;
		var alongGround = wishDir.SubtractDirection( groundNormal ).Normal;

		var slopeDot = alongGround.Dot( -WorldTransform.Up );
		return 1f + slopeDot * MomentumSlopeFactor;
	}

	public float CalculateGroundAcceleration()
	{
		var speed = Controller.Velocity.Length;
		var acceleration = Acceleration;
		if ( !Crouch.IsCrouched )
		{
			if ( speed < WalkSpeed - 1f )
			{
				var lowSpeed = WalkSpeed * 0.7f;
				if ( speed > lowSpeed )
				{
					var highAcceleration = Acceleration * 0.18f; // Both magic numbers are relative
					acceleration = highAcceleration;
				}
			}
			else
			{
				acceleration = (SprintSpeed - WalkSpeed) / SprintTime;
			}
		}

		//acceleration *= MomentumTurnMultiplier;
		acceleration *= CalculateSlopeFactor();

		return acceleration;
	}

	public void Decelerate( Vector3? wishDirection = null, float? deceleration = null, float? maxSpeed = null )
	{
		wishDirection ??= Look3d.WishDirectionHorizontal;
		deceleration ??= Deceleration;
		maxSpeed ??= CalculateGroundMaxSpeed();

		if ( deceleration is null)
		{
			var speed = Controller.Velocity.WithZ( 0 ).Length;
			var t = MathX.Remap( speed, WalkSpeed, SprintSpeed, 0f, 1f ).Clamp( 0f, 1f );
			var dec = MathX.Lerp( Deceleration, HighSpeedDeceleration, t );

			if ( wishDirection.Value.IsNearZeroLength ) { dec *= NoInputDecelerationScale; }

			deceleration = dec;
		}

		var speedAlongWishDirection = Controller.Velocity.Dot( wishDirection.Value );
		var allowedVelocity = speedAlongWishDirection.Clamp( 0f, maxSpeed.Value ) * wishDirection.Value;
		var velocityToDecelerate = Controller.Velocity - allowedVelocity;
		velocityToDecelerate = velocityToDecelerate.Normal * MathF.Max(velocityToDecelerate.Length - deceleration.Value * Time.Delta, 0f);
		Controller.Velocity = velocityToDecelerate + allowedVelocity;
	}

	public void Accelerate( Vector3? wishDirection = null, float? acceleration = null, float? maxSpeed = null )
	{
		wishDirection ??= Look3d.WishDirectionHorizontal;
		acceleration ??= CalculateGroundAcceleration();
		maxSpeed ??= CalculateGroundMaxSpeed();

		var speedAlongWishDirection = Controller.Velocity.Dot( wishDirection.Value );
		var maxAddedSpeed = maxSpeed.Value - speedAlongWishDirection;

		if ( maxAddedSpeed <= 0 ) { return; }

		var speedLimit = MathF.Max( Controller.Velocity.Length, maxSpeed.Value );
		Controller.Velocity += wishDirection.Value * MathF.Min( acceleration.Value * Time.Delta, maxAddedSpeed );

		var newSpeed = Controller.Velocity.Length;
		if ( newSpeed > speedLimit )
		{
			var factor = speedLimit / newSpeed;
			Controller.Velocity *= factor;
		}
	}
	
	public void AirAccelerate(Vector3? wishDirection = null, float? airAcceleration = null, float? maxSpeed = null)
	{
		wishDirection ??= Look3d.WishDirectionHorizontal;
		airAcceleration ??= AirAcceleration;
		maxSpeed ??= AirLimit;

		// TODO: Switch to "relative velocity", see https://apexmovement.tech/wiki/articles/Wiki%20help%3EHow%20the%20Apex%20engine%20handles%20Movement#Moving_Platforms

		var horizontalVelocity = Controller.Velocity.SubtractDirection( WorldTransform.Up );
		var currentSpeedAlongWishDirection = horizontalVelocity.Dot( wishDirection.Value );
		var addedMaxSpeed = maxSpeed.Value - currentSpeedAlongWishDirection;

		if ( addedMaxSpeed <= 0f ) { return; }

		Controller.Velocity += MathF.Min( airAcceleration.Value * Time.Delta, addedMaxSpeed ) * wishDirection.Value;
	}
}
