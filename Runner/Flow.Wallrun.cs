namespace Flow.States;

public partial class Runner
{
	[Title( "Runner Wallrun State" ), Group( "Flow/States/Runner" ), Icon( "merge_type" )]
	public partial class Wallrun : Controller.State, Look3d.IEvents, Controller.Integrator.IEvents
	{
		[RequireComponent] public FrameOfReferenceIntegrator FrameOfReferenceIntegrator { get; set; }
		public override Controller.Integrator Integrator => FrameOfReferenceIntegrator;

		[RequireComponent] public Runner Runner { get; set; }
		[RequireComponent] public Look3d Look3d { get; set; }
		[RequireComponent] public Camera3d Camera3d { get; set; }
		[RequireComponent] public Crouch Crouch { get; set; }
		[RequireComponent] public Slide Slide { get; set; }

		[Property]
		public float MinEntrySpeed { get; set; } = 225f;
		[Property]
		public float MaxWallAngle { get; set; } = 97.5f;
		[Property]
		public float MinWallAngle { get; set; } = 82.5f;
		[Property]
		public float DetectDistance { get; set; } = 1f;
		[Property]
		public float MaxDuration { get; set; } = 3.5f;
		[Property]
		public float StickForce { get; set; } = 400f;
		[Property]
		public float GravityScale { get; set; } = 0.15f;
		[Property]
		public float UpwardBoost { get; set; } = 50f;
		[Property]
		public float JumpUpSpeed { get; set; } = 75;
		[Property]
		public float JumpAwaySpeed { get; set; } = 125f;
		[Property]
		public float CameraRoll { get; set; } = 12f;
		[Property]
		public float SpeedAlongWall { get; set; } = 320f;
		[Property]
		public float SpeedAlongWallMomentumBonus { get; set; } = 0.4f; 
		[Property]
		public float DecayStartTime { get; set; } = 2.75f;
		[Property]
		public float Cooldown { get; set; } = 0.25f;
		[Property]
		public float ForwardCheckDistance { get; set; } = 24f;
		[Property]
		public float MaxPerFrameWallAngleChange { get; set; } = 30f;

		public GameObject WallObject { get; set; } = null;
		private Vector3 LastWallNormal { get; set; } = Vector3.Zero;
		public Vector3 WallLocalPoint { get; set; } = Vector3.Zero;
		public Vector3 WallLocalNormal { get; set; } = Vector3.Zero;
		public int Side { get; set; } = 0; // -1 for left, 1 for right
		public TimeSince TimeInWallRun { get; set; } = float.PositiveInfinity;
		public TimeSince StartedHoldingJump { get; set; } = float.PositiveInfinity;
		private const float MinReentryAngle = 44.9f; // new wall normal must differ from last by at least this many degrees

		public Vector3 WallWorldPoint
		{
			get => WallObject.WorldTransform.PointToWorld( WallLocalPoint );
			set => WallLocalPoint = WallObject.WorldTransform.PointToLocal( value );
		}

		public Vector3 WallWorldNormal
		{
			get => WallObject.WorldTransform.NormalToWorld( WallLocalNormal );
			set => WallLocalNormal = WallObject.WorldTransform.NormalToLocal( value );
		}
		public float RiseToRunRatio { get; set; } = 0.165f;
		public float CurrentSpeedAlongWall => SpeedAlongWall + ( Controller.MomentumCap - Runner.SprintSpeed ) * SpeedAlongWallMomentumBonus;
		private float PeakRiseSpeed => CurrentSpeedAlongWall  * RiseToRunRatio;

		public override bool TryEnter()
		{
			if ( Controller.CurrentState != Runner && Controller.CurrentState.GetType() != typeof( Wallrun ) ) { return false; }
			if ( GetComponent<Interactor>()?.IsAdvancedMovementBlocked ?? false ) { return false; }
			if (!Input.Down("jump")){ return false; }
			if ( Controller.IsOnGround ) { return false; }
			if ( Crouch.IsCrouched ) { return false; }
			if ( Runner.WantCrouch ) { return false; }
			const float MIN_HOLD_TIME = 0.175f; // hold input if fresh off ground
			const float AIRBORNE_TIME = 0.25f; // after this long in air, just holding it is enough
			if ( Runner.JumpTimer < AIRBORNE_TIME && Runner.JumpTimer < MIN_HOLD_TIME) { return false; }
			if ( Controller.Velocity.z < -450 ) { return false; } // if falling too fast, can't wallrun. prolly change later

			var horizontalVel = Controller.Velocity.WithZ( 0 );
			if ( horizontalVel.Length < MinEntrySpeed ) { return false; } // going too slow
 
			var forward = Look3d.HorizontalLook.Forward;
			var trace = Controller.Trace( Controller.Position, Controller.Position + forward * (DetectDistance + 4f) );

			if ( !trace.Hit ) return false;

			// Wall must be nearly vertical
			var angleFromVertical = MathF.Acos( MathF.Abs( trace.Normal.Dot( WorldTransform.Up ) ) ) * (180f / MathF.PI);
			if ( angleFromVertical.NotBetween( MinWallAngle, MaxWallAngle ) ) return false;
			var headOn = forward.Dot( -trace.Normal );
			if ( headOn.NotBetween( 0.1f, 0.6f ) ) { return false; }

			var tangent = WorldTransform.Up.Cross( trace.Normal ).Normal;
			var velAlong = horizontalVel.Dot( tangent );
			var side = velAlong >= 0f ? 1 : -1;

			if ( LastWallNormal != Vector3.Zero )
			{
				var angleDiff = MathF.Acos( LastWallNormal.Dot( trace.Normal ).Clamp( -1f, 1f ) ) * (180f / MathF.PI);
				if ( angleDiff < MinReentryAngle ) return false;
			}

			WallObject = trace.GameObject;
			WallWorldNormal = trace.Normal;
			WallWorldPoint = trace.EndPosition;
			Side = side;

			// one last check
			var toWall = -WallWorldNormal;
			var headHeight = Controller.Position + new Vector3( 0f, 0f, 72f );
			var headCheck = Controller.Trace( headHeight, headHeight + toWall * DetectDistance );
			var legCheck = Controller.TraceRay( Controller.Position, Controller.Position + toWall * DetectDistance );
			var aboveCheck = Controller.TraceRay( headHeight, headHeight + WorldTransform.Up * DetectDistance );

			if ( !headCheck.Hit || headCheck.GameObject != trace.GameObject || !legCheck.Hit || legCheck.GameObject != trace.GameObject || aboveCheck.Hit )
			{
				return false;
			} // can't wallrun, either legs wont touch or head wont touch.

			return true;
		}

		public override void Enter()
		{
			Runner.DropLurchTimer();

			TimeInWallRun = 0f;
			Controller.Velocity = Controller.Velocity.WithZ(0);
		}

		public override void Move()
		{

			FrameOfReferenceIntegrator.BypassPhysics = true;
			FrameOfReferenceIntegrator.BypassLinkedMovement = false; // respect wall movement

			if ( !WallObject.IsValid() )
			{
				Exit();
				return;
			}

			if ( TimeInWallRun > MaxDuration )
			{
				DetachAwayFromWall();
				return;
			}

			var toWall = -WallWorldNormal;  // into the wall
			var recheckLegs = Controller.Trace( Controller.Position, Controller.Position + toWall * (DetectDistance + 4f) );
			var headHeight = Controller.Position + new Vector3( 0f, 0f, 72f );
			var recheckHead = Controller.Trace( headHeight, headHeight + toWall * (DetectDistance + 4f) );
			//DebugOverlay.DrawRay( recheckHead, duration: 4f, color: Color.Yellow );
			//DebugOverlay.DrawRay( recheckLegs, duration: 4f, color: Color.Orange );


			if ( !recheckLegs.Hit || recheckLegs.GameObject != WallObject || !recheckHead.Hit || recheckHead.GameObject != WallObject )// lost the wall
			{
				DetachAwayFromWall();
				return;
			}

			var maxAngleChangeDot = MathF.Cos( MaxPerFrameWallAngleChange * (MathF.PI / 180f) );
			var normalDot = recheckLegs.Normal.Dot( WallWorldNormal );

			if ( normalDot < maxAngleChangeDot ) // per-frame wall bend exceeds rideable limit
			{
				DetachAwayFromWall();
				return;
			}

			WallWorldNormal = recheckLegs.Normal; // keep honest on curved/moving walls
			WallWorldPoint = recheckLegs.EndPosition;

			var normal = WallWorldNormal;
			var up = WorldTransform.Up;
			var tangent = up.Cross( normal ).Normal;
			var runDirection = tangent * Side; // +/- 1

			// Obstacle ahead: detach if the surface ahead bends sharper than the rideable limit
			var forwardCheck = Controller.Trace( Controller.Position, Controller.Position + runDirection * ForwardCheckDistance );
			if ( forwardCheck.Hit && forwardCheck.Normal.Dot( normal ) < maxAngleChangeDot )
			{
				DetachAwayFromWall();
				return;
			}

			// exit conds
			if ( Controller.IsOnGround )
			{
				Exit();
				return;
			}

			// Input is moving away from the wall.
			var intent = new Vector3( Input.AnalogMove.x, Input.AnalogMove.y, 0F ) * Look3d.HorizontalLook;
			if ( intent.Dot( WallWorldNormal ) > 0.4f )
			{
				DetachAwayFromWall();
				return;
			}

			if ( Runner.WantAnyJump )
			{
				TryWallJump();
				return; 
			} // player jumping off, should have a min timer before they can do this

			var vel = Controller.Velocity;

			var (velAlongPlane, _) = vel.SplitByNormal( normal );
			var (velTangent, velVertical) = velAlongPlane.SplitByNormal( up );

			// Target tan speed, preserve dir and approach speedalongwall
			var tangentSpeed = velTangent.Dot( runDirection );
			tangentSpeed = tangentSpeed.ApproachWithStopSpeed( CurrentSpeedAlongWall, 20, 2 );
			velTangent = runDirection * tangentSpeed;

			// So, this all ignores up/down velocity. Hopefully can find a way of making it use vel later, but it's a PITA for now.
			var t = (float)TimeInWallRun;
			float riseEnvelope;
			if ( t < 0.2f )
			{
				riseEnvelope = t / 0.2f;            // ramp up
			}
			else if ( t < DecayStartTime )
			{
				riseEnvelope = 1f;                   // plateau
			}
			else
			{
				var fall = ((t - DecayStartTime) / (MaxDuration - DecayStartTime)).Clamp( 0f, 1f );
				riseEnvelope = 1f - fall;            // ramp down to 0
			}

			// Rise speed is directly proportional to tangent speed — this gives your rise:run ratio exactly.
			var riseSpeed = tangentSpeed * RiseToRunRatio * riseEnvelope;

			// In the decay phase, let the player actually slide DOWN rather than just stop rising.
			float verticalSpeed;
			if ( t < DecayStartTime )
			{
				verticalSpeed = riseSpeed;           // pure rise, no gravity
			}
			else
			{
				// Smoothly transition from rise to slide.
				var slideFraction = ((t - DecayStartTime) / (MaxDuration - DecayStartTime)).Clamp( 0f, 1f );
				var slideSpeed = -CurrentSpeedAlongWall * RiseToRunRatio * slideFraction; // negative = downward
				verticalSpeed = riseSpeed + slideSpeed;
			}

			velVertical = up * verticalSpeed; 

			// Recombine (wall contact maintained via positional snap below, not a velocity component)
			Controller.Velocity = velTangent + velVertical;

			// Manually advance position (bypassing integrator)
			Controller.Position += Controller.Velocity * Time.Delta;

			// Snap to wall: prevent clipping and maintain contact at DetectDistance
			var toWallSnap = -normal; // use updated normal
			var snapTrace = Controller.Trace( Controller.Position, Controller.Position + toWallSnap * (DetectDistance + 4f) );
			if ( snapTrace.StartedSolid )
			{
				Controller.Position += normal * Controller.Margin;
			}
			else if ( snapTrace.Hit && snapTrace.GameObject == WallObject )
			{
				var gap = snapTrace.Distance;
				if ( gap < Controller.Margin )
					Controller.Position += normal * (Controller.Margin - gap); // push away from wall
				else if ( gap > DetectDistance )
					Controller.Position += toWallSnap * (gap - DetectDistance).Min( 2f ); // pull toward wall
			}

			var wallHorizSpeed = Controller.Velocity.WithZ( 0 ).Length;
			if ( wallHorizSpeed > Controller.MomentumCap ) { Controller.MomentumCap = wallHorizSpeed; }
			if ( WallObject.GetComponent<Rigidbody>() != null )
			{
				var rb = WallObject.GetComponent<Rigidbody>();
				Controller.Position += rb.GetVelocity() * Time.Delta;
			}

		}

		public bool TryWallJump()
		{

			var away = WallWorldNormal;
			var up = WorldTransform.Up;
			var forward = Look3d.HorizontalLook.Forward * Input.AnalogMove.x;
			var launch = away * JumpAwaySpeed + up * JumpUpSpeed + forward * (JumpAwaySpeed * 0.3f);


			Runner.Jump( JumpAwaySpeed, launch.Normal );
			Controller.InjectMomentum( speedAdd: 40f, capRaiseFraction: 1.5f );
			SetNextState( Runner );
			Runner.DropLurchTimer();
			return true;

		}

		private void DetachAwayFromWall()
		{
			// small boost away
			Controller.Velocity += WallWorldNormal * 250f;
			
			Exit();
		}

		public override void Exit()
		{
			if ( !IsCurrent ) return;

			SetNextState( Runner );
			Log.Info( $"Exiting wallrun state. {Time.Now}" );

			if ( FrameOfReferenceIntegrator.IsValid() )
			{
				FrameOfReferenceIntegrator.BypassPhysics = false;
				FrameOfReferenceIntegrator.BypassLinkedMovement = false;
				FrameOfReferenceIntegrator.FrameOfReference = null;
			}
			
			LastWallNormal = WallObject.IsValid() ? WallWorldNormal : LastWallNormal;
			WallObject = null;
			Side = 0;
		}

		void Look3d.IEvents.ModifyEyeAngles( Look3d module, ref Angles eyeAngles, in Angles input, in Angles oldEyeAngles )
		{
			if ( !IsCurrent || !WallObject.IsValid() || Side == 0 ) { return; }
			var entryBlend = ((float)TimeInWallRun / 0.2f).Clamp( 0f, 1f );
			var targetRoll = Side * CameraRoll;
			targetRoll *= entryBlend;

			eyeAngles.roll = eyeAngles.roll.ApproachWithStopSpeed( targetRoll, 0.05f, module.RollDecay * 2f );
		}

		void Controller.Integrator.IEvents.OnLand( GameObject gameObject, float landSpeed )
		{
			LastWallNormal = Vector3.Zero; // grounding refreshes wallrun history
		}
	}
}
