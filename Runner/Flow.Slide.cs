namespace Flow.States;

public partial class Runner
{
	[Title( "Flow Runner Slide State" ), Group( "Flow/States/Runner" ), Icon( "double_arrow" )]
	public partial class Slide : Controller.State, Look3d.IEvents, Camera3d.IEvents
	{
		[RequireComponent] public Runner Runner { get; set; }
		[RequireComponent] public Crouch Crouch { get; set; }
		[RequireComponent] public Look3d Look3d { get; set; }

		[RequireComponent] public FrameOfReferenceIntegrator FrameOfReferenceIntegrator { get; set; }
		public override Controller.Integrator Integrator => FrameOfReferenceIntegrator;

		[Property]
		public float MinSpeed { get; set; } = 200f;

		[Property]
		public float MinHorizontalSpeed { get; set; } = 90f;

		[Property]
		public float StopSpeed { get; set; } = 125f;

		[Property]
		public float VelocityDecay { get; set; } = 0.3f;

		[Property]
		public float Acceleration { get; set; } = 100f;

		[Property]
		public float Deceleration { get; set; } = 100f;

		[Property]
		public float WantToStopDeceleration { get; set; } = 400f;

		[Property]
		public float JumpHeight { get; set; } = 48f;

		[Property]
		public float BoostCooldown { get; set; } = 2f;

		[Property]
		public float BoostSpeed { get; set; } = 150f;

		[Property]
		public float BoostCap { get; set; } = 400f;

		[Property]
		public float MinBoostedJumpTime { get; set; } = 0.2f;

		[Property]
		public float CameraTilt { get; set; } = 15f;

		[Property]
		public float CameraFovScale { get; set; } = 1.1f;

		[Property]
		public float CameraFovTime { get; set; } = 0.25f;

		public TimeSince LastSlideTime = float.PositiveInfinity;
		public TimeSince LastSlideBoostTime = float.PositiveInfinity;

		public override bool TryEnter()
		{
			if ( Controller.CurrentState != Runner ) { return false; }
			if ( !Controller.IsOnGround ) { return false; }
			if ( Crouch.Progress <= 0f ) { return false; }
			if ( !Runner.WantCrouch ) { return false; }
			if ( Runner.LandTime > Time.Delta * 1.5f )
			{
				var speed = Controller.Velocity.Length;
				if ( speed < MinSpeed - 5f ) { return false; }
				if ( Runner.SprintProgress < 0.01f ) return false;
			}

			return true;
		}

		public override void Enter()
		{
			Crouch.IsCrouched = true;
			if ( LastSlideTime > BoostCooldown )
			{
				Boost();
				LastSlideBoostTime = 0f;
			}

			LastSlideTime = 0f;
		}

		public override void Process() { }

		public override void Move()
		{
			TryJump();

			Runner.ThinkSprint();
			Runner.ThinkCrouch();

			var deceleration = Deceleration;
			if ( Controller.Velocity.Normal.Dot( Look3d.WishDirectionHorizontal ) < -0.7f ) { deceleration = WantToStopDeceleration; }

			Runner.Decelerate( deceleration: deceleration, maxSpeed: Runner.CalculateGroundMaxSpeed() );
			Runner.Accelerate( acceleration: Acceleration, maxSpeed: Runner.CalculateGroundMaxSpeed() );

			if ( !Controller.IsOnGround || Crouch.Progress <= 0f || Controller.Velocity.Length < StopSpeed )
			{
				SetNextState( Runner );
				return;
			}

			Controller.Velocity *= MathF.Pow( 1f - VelocityDecay, Time.Delta );
			Controller.Velocity += Controller.Gravity.SubtractDirection( Controller.GroundNormal ).SubtractDirection( WorldTransform.Up ) * Time.Delta;
		}

		public void TryJump()
		{
			if ( Runner.TryJump( JumpHeight ) )
			{
				if ( LastSlideBoostTime < 0.5f && !IsBoostedJump() )
				{
					var (OnPlane, AlongNormal) = Controller.Velocity.SplitByNormal( WorldRotation.Up );
					Controller.Velocity = OnPlane.Normal * MathF.Max( OnPlane.Length - BoostSpeed, 0f ) + AlongNormal;
				}
			}
		}

		public bool IsBoostedJump()
		{
			var horizontalSpeed = Controller.Velocity.SubtractDirection( WorldTransform.Up ).Length;

			if ( LastSlideTime > MinBoostedJumpTime ) { return true; }
			if ( horizontalSpeed < Runner.WalkSpeed + BoostSpeed ) return true;

			return false;
		}

		public void Boost()
		{
			var v = Controller.Velocity.SubtractDirection( WorldTransform.Up );
			Controller.Velocity += v.Normal * MathF.Max( BoostSpeed - MathF.Max( v.Length - BoostCap, 0f ), 0f );
		}

		protected override void OnUpdate()
		{
			UpdateCameraFov();
			UpdateCameraTilt();
		}

		protected float SlideFovProgress { get; set; } = 0f;
		protected float FovScale { get; set; } = 1f;

		public void UpdateCameraFov()
		{
			SlideFovProgress += Time.Delta / (IsCurrent ? CameraFovTime : -CameraFovTime);
			SlideFovProgress = SlideFovProgress.Clamp( 0f, 1f );
			FovScale = 1f + (CameraFovScale - 1f) * Sandbox.Utility.Easing.QuadraticInOut( SlideFovProgress );
		}

		void Camera3d.IEvents.ModifyCameraFov( Camera3d module, ref float fieldOfView )
		{
			fieldOfView *= FovScale;
		}

		protected float SlideTiltSmoothed { get; set; } = 0f;

		public void UpdateCameraTilt()
		{
			var amount = 0f;
			if ( IsCurrent )
			{
				amount = -Look3d.HorizontalLook.Right.Dot( Controller.Velocity );
				amount *= Look3d.Look.Forward.Dot( Look3d.HorizontalLook.Forward );
				amount = amount.Abs().Remap( StopSpeed, Runner.SprintSpeed + BoostSpeed ).CopySign( amount );
			}

			SlideTiltSmoothed = SlideTiltSmoothed.ApproachWithStopSpeed( amount * CameraTilt, 10f, 10f );
		}

		void Look3d.IEvents.ModifyLookSway(Look3d module, ref Angles sway)
		{
			sway += new Angles( 0f, 0f, SlideTiltSmoothed );
		}
	}
}
