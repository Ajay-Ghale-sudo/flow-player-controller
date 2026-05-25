namespace Flow.States;

public partial class Runner
{
	[Title("Runner Mantle State"), Group("Flow/States/Runner"), Icon("merge_type")]
	public partial class Mantle : Controller.State, Look3d.IEvents
	{
		[RequireComponent] public FrameOfReferenceIntegrator FrameOfReferenceIntegrator { get; set; }
		public override Controller.Integrator Integrator => FrameOfReferenceIntegrator;

		[RequireComponent] public Runner Runner { get; set; }
		[RequireComponent] public Look3d Look3d { get; set; }
		[RequireComponent] public Camera3d Camera3d { get; set; }
		[RequireComponent] public Crouch Crouch { get; set; }
		[RequireComponent] public Slide Slide { get; set; }

		[Property]
		public float Height { get; set; } = 80f;
		[Property]
		public float PeekHeight { get; set; } = 4f;
		[Property]
		public float UpTime { get; set; } = 0.45f;
		[Property]
		public float DownTime { get; set; } = 0.1f;
		[Property]
		public float JumpHeight { get; set; } = 60f;
		[Property]
		public float JumpHorizontalSpeed { get; set; } = 200f;
		[Property]
		public bool DisallowYawWrapAround { get; set; } = true;

		public GameObject Object { get; set; } = null;

		public Vector3 MantleLocalPosition { get; set; } = Vector3.Zero;
		public Vector3 MantleWorldPosition
		{
			get => Object.WorldTransform.PointToWorld( MantleLocalPosition );
			set => MantleLocalPosition = Object.WorldTransform.PointToLocal( value );
		}
		public Vector3 MantleLocalNormal { get; set; } = Vector3.Zero;
		public Vector3 MantleWorldNormal { get => WorldTransform.NormalToWorld( MantleLocalNormal );
			set => MantleLocalNormal = Object.WorldTransform.NormalToLocal( value );
		}
		public Vector3 MantleLocalDirection => -MantleLocalNormal;
		public Vector3 MantleWorldDirection => -MantleWorldNormal;

		public float GrabVerticalDistance { get; set; } = 0f;
		public float PullVerticalDistance => GrabVerticalDistance + HoldHeight;

		public float HoldHeight => Camera3d.BaseCameraHeight - PeekHeight;
		public float PullUpInset => Controller.Collider.Radius * 1.05f;

		public bool WantDown => Runner.WantCrouch || (Vector3.Zero.WithX( MathF.Min( Input.AnalogMove.x, 0f ) ) * Look3d.HorizontalLook).Dot( MantleWorldPosition ) > 0.5f;
		public bool GoingDown { get; set; } = false;
		public float GoingDownProgress { get; set; } = 0f;


		public bool WantUp => (Vector3.Zero.WithX( Input.AnalogMove.x ) * Look3d.HorizontalLook).Dot( MantleWorldDirection ) > 0.3f;
		public bool CanGoUp { get; set; } = false;
		public bool GoingUp { get; set; } = false;
		public float GoingUpProgress { get; set; } = 0f;

		public TimeSince StartTime { get; set; } = float.PositiveInfinity;
		public TimeSince DropTime { get; set; } = float.PositiveInfinity;
		public TimeSince QuickTime { get; set; } = float.PositiveInfinity;

		protected List<Vector3> _attachVelocitiesUtil = [];

		public SceneTraceResult Trace() => Controller.Trace( Controller.Position, Controller.Position + Look3d.HorizontalLook.Forward * 4f );

		const float MANTLE_HEADROOM_CHECK_GAP = 4f;

		public override bool TryEnter()
		{
			if ( Controller.CurrentState != Runner && Controller.CurrentState.GetType() != typeof( Climb ) && Controller.CurrentState.GetType() != typeof( Wallrun ) ) { return false; }
			if ( GetComponent<Interactor>()?.IsAdvancedMovementBlocked ?? false ) { return false; }
			if ( Controller.IsOnGround ) { return false; }
			if ( Crouch.IsCrouched ) { return false; }
			if ( Runner.WantCrouch ) { return false; }
			if ( DropTime < 0.5f ) { return false; }

			var trace = Trace();
			if ( !trace.Hit ) { return false; }

			var angle = MathX.RadianToDegree( MathF.Acos( trace.Normal.Dot( WorldTransform.Up ).Clamp( -1f, 1f ) ) );
			if ( angle < Controller.GroundAngle + 1f || angle > 134f ) { return false; }

			var horizontalNormal = trace.Normal.SubtractDirection( WorldTransform.Up ).Normal;
			var headOn = Look3d.HorizontalLook.Forward.Dot( -horizontalNormal );
			if ( headOn < 0.725f ) { return false; }

			if ( Input.AnalogMove.ClampLength( 1f ).x <= 0.7f )
			{
				if ( (GetComponent<Climb>()?.EndTime ?? float.PositiveInfinity) > 0.1f )
				{
					if ( !_attachVelocitiesUtil.Where( x => x.Dot( -horizontalNormal ) > 10f ).Any() )
					{
						return false;
					}
				}

				if ( !Input.AnalogMove.IsNearZeroLength )
				{
					return false;
				}
			}

			var headroom = Controller.Trace( Controller.Position, Controller.Position + WorldTransform.Up * Height ).Distance;
			if ( headroom < 1f ) { return false; }

			SceneTraceResult? forwardTraceOrNull = null;
			float checkHeight;
			for ( checkHeight = MANTLE_HEADROOM_CHECK_GAP; checkHeight <= headroom; checkHeight += MANTLE_HEADROOM_CHECK_GAP )
			{
				var start = Controller.Position + WorldTransform.Up * checkHeight;
				var end = start - horizontalNormal * (PullUpInset + Controller.Margin);
				var tr = Controller.Trace( start, end );
				if ( !tr.Hit )
				{
					forwardTraceOrNull = tr;
					break;
				}
			}
			

			if ( checkHeight <= Controller.StepHeight ) { return false; }
			if ( forwardTraceOrNull is not SceneTraceResult forwardTrace ) { return false; }
			var downTrace = Controller.Trace( forwardTrace.EndPosition, forwardTrace.EndPosition + WorldTransform.Down * MANTLE_HEADROOM_CHECK_GAP * 1.5f );
			if ( !Controller.IsValidGround( downTrace ) ) { return false; }

			GoingUp = false;
			GoingUpProgress = 0f;
			GoingDown = false;
			GoingDownProgress = 0f;

			var upDistance = checkHeight - HoldHeight;

			var clampedUpDistance = upDistance;
			if ( upDistance < 0f )
			{
				var holdTrace = Controller.Trace( Controller.Position, Controller.Position + WorldTransform.Down * (-upDistance) );
				if ( holdTrace.Hit )
				{
					GoingUp = true;
					GoingUpProgress = holdTrace.Fraction.Min( 0.5f );
					clampedUpDistance = clampedUpDistance.Max( -holdTrace.Distance );
					QuickTime = 0f;
				}
			}
			Object = downTrace.GameObject;
			MantleWorldNormal = horizontalNormal;
			GrabVerticalDistance = upDistance;

			return true;
		}

		public override void Enter()
		{
			StartTime = 0f;
			MantleWorldPosition = Controller.Position;
		}

		public override void Process()
		{
			_attachVelocitiesUtil.Add( FrameOfReferenceIntegrator.PreIntegrateVelocity );
			if ( _attachVelocitiesUtil.Count > 15 ) { _attachVelocitiesUtil.RemoveAt( 0 ); }
		}

		public override void Move()
		{
			FrameOfReferenceIntegrator.BypassPhysics = true;
			FrameOfReferenceIntegrator.BypassLinkedMovement = true;

			if ( !Object.IsValid() )
			{
				Exit();
				return;
			}
			// Apply force to mantle object but prevent double grav.
			if ( Object.GetComponent<Collider>()?.Rigidbody is var rigidbody && rigidbody.IsValid() )
			{
				if ( rigidbody.GetVelocity().Dot( Controller.Gravity.Normal ) < 20f )
				{
					var massCenterLocal = rigidbody.OverrideMassCenter ? rigidbody.MassCenterOverride : rigidbody.MassCenter;
					var massCenterWorld = rigidbody.WorldTransform.PointToWorld( massCenterLocal );
					var pushPoint = MantleWorldPosition + WorldTransform.Up * (PullVerticalDistance - PeekHeight * 0.8f) + MantleWorldDirection * PullUpInset * 1.2f;
					pushPoint = pushPoint.LerpTo( massCenterWorld, 0.25f );
					rigidbody.ApplyForceAt( pushPoint, Controller.Gravity * Controller.Mass );
				}
			}
			if ( MantleWorldPosition.Distance( WorldPosition ) > Height )
			{
				Exit();
				return;
			}

			var lowerCheckTrace = Controller.Trace( MantleWorldPosition, MantleWorldPosition + WorldTransform.Down * GrabVerticalDistance );
			var legsStuck = Controller.TraceCustom( MantleWorldPosition, MantleWorldPosition + WorldTransform.Down * GrabVerticalDistance, x =>
			{
				x.Capsule( new Capsule( Controller.Capsule.CenterA, Controller.Capsule.CenterB, Controller.Capsule.Radius - 4f ) );
			} ).Hit;

			CanGoUp = false;
			var upTrace = Controller.Trace( MantleWorldPosition, MantleWorldPosition + WorldTransform.Up * PullVerticalDistance );
			if ( !upTrace.Hit )
			{
				var forwardTrace = Controller.Trace( upTrace.EndPosition, upTrace.EndPosition + MantleWorldDirection * (PullUpInset + Controller.Margin) );
				if ( !forwardTrace.Hit )
				{
					var downTrace = Controller.Trace( forwardTrace.EndPosition, forwardTrace.EndPosition + WorldTransform.Down * (MANTLE_HEADROOM_CHECK_GAP + Controller.Margin * 2f) );
					if ( Controller.IsValidGround( downTrace ) )
					{
						CanGoUp = true;
					}
				}
			}

			if ( !(GoingUp || GoingDown) )
			{
				if ( CanGoUp )
				{
					if ( WantUp && GrabVerticalDistance < 1f || legsStuck )
					{
						GoingUp = true;
						GoingUpProgress = 1f - PullVerticalDistance / HoldHeight;
						GoingUpProgress = GoingUpProgress.Clamp( 0f, 0.25f );
						QuickTime = 0f;
					}
				}
				else if ( legsStuck )
				{
					Exit();
					Log.Info( "Door stuck" );
					return;
				}
			}
			if ( !(GoingUp || GoingDown) )
			{
				if ( WantDown )
				{
					GoingDown = true;
					GoingDownProgress = 0f;
				}
			}

			if ( !(GoingUp || GoingDown) )
			{
				var prevVerticalDistance = GrabVerticalDistance;
				GrabVerticalDistance = GrabVerticalDistance.ApproachWithStopSpeed( 0f, 10f, 8f );
				var add = GrabVerticalDistance - prevVerticalDistance;
				MantleWorldPosition += WorldTransform.Down * add;
			}

			Controller.GroundObject = Object;
			Controller.GroundNormal = WorldTransform.Up;
			Controller.Velocity = Vector3.Zero;

			Controller.Position = MantleWorldPosition;

			if ( !(GoingUp || GoingDown) )
			{
				if ( TryJump() ) { return; }
			}

			if ( GoingUp )
			{
				if ( !CanGoUp )
				{
					Exit();
					return;
				}

				GoingUpProgress += Time.Delta / UpTime;
				GoingUpProgress = GoingUpProgress.Clamp( 0f, 1f );

				const float MANTLE_STAGE_CUTOFF = 0.5f;
				var midPosition = WorldTransform.Up * (PullVerticalDistance * 0.7f + Controller.Margin);
				var endPosition = WorldTransform.Up * (PullVerticalDistance + Controller.Margin) + MantleWorldDirection * PullUpInset;
				if ( GoingUpProgress < MANTLE_STAGE_CUTOFF )
				{
					// Up stage
					var frac = GoingUpProgress.Remap( 0f, MANTLE_STAGE_CUTOFF );
					frac = MathF.Pow( frac, 0.7f );
					frac = CosInterpolate( frac );
					Controller.Position = MantleWorldPosition + midPosition * frac;
				}
				else
				{
					// Forward stage
					var frac = GoingUpProgress.Remap( MANTLE_STAGE_CUTOFF, 1f );
					frac = CosInterpolate( frac );
					Controller.Position = MantleWorldPosition + midPosition.LerpTo( endPosition, frac );
				}

				if ( GoingUpProgress >= 1f )
				{
					Exit( true );
					Boost();
					return;
				}

			}

			if ( GoingDown )
			{
				GoingDownProgress += Time.Delta / DownTime;
				GoingDownProgress = GoingDownProgress.Clamp( 0f, 1f );
				if ( GoingDownProgress >= 1f )
				{
					Exit();
					Controller.Velocity += WorldTransform.Down * 70f;
					return;
				}
				return;
			}
		}

		public bool TryJump()
		{
			var jumpDirection = Look3d.HorizontalLook.Forward;
			var jumpDirectionDot = jumpDirection.Dot( MantleWorldNormal );
			if ( jumpDirectionDot < 0f )
			{
				jumpDirection = MantleWorldNormal;
			}

			if ( Runner.TryJump( JumpHeight ) )
			{
				SetNextState( Runner );
				Controller.Velocity += jumpDirection * JumpHorizontalSpeed;
				Runner.DropLurchTimer();

				return true;
			}

			return false;
		}

		public void Boost()
		{
			var speed = Runner.SprintSpeed;
			Runner.IgnoreSprintDirection( 0.1f );
			if ( Runner.WantCrouch )
			{
				speed = Runner.CrouchSpeed;
				Crouch.Progress = 1f;
			}
			Controller.Velocity = Look3d.WishDirectionHorizontal * speed;
		}

		public void Exit( bool up = false )
		{
			if ( !IsCurrent ) { return; }
			SetNextState( Runner );

			var newGroundObject = up ? (Object.IsValid() ? Object : null) : null;
			Controller.GroundObject = newGroundObject;
			FrameOfReferenceIntegrator.FrameOfReference = newGroundObject;
			Task.FrameEnd().ContinueWith( async x =>
			{
				if ( !Task.IsValid ) { return; }
				await Task.FrameEnd();
				if ( !FrameOfReferenceIntegrator.IsValid() || !newGroundObject.IsValid() ) { return; }
				FrameOfReferenceIntegrator.FrameOfReference = newGroundObject;
			} );

			if ( up )
			{
				if ( Object.IsValid() )
				{
					Controller.Velocity += Object.GetComponent<Collider>().GetVelocityAtPoint( MantleWorldPosition );
				}
			}
		}

		public override void Exit()
		{
			DropTime = 0f;
			GoingUp = false;
			GoingUpProgress = 0f;
			GoingDown = false;
			GoingDownProgress = 0f;
		}

		protected Angles Sway = new();

		protected override void OnUpdate()
		{
			var target = new Angles();
			target.pitch += ((float)StartTime).Remap( 0f, 0.25f, -10f, 0f );
			target.pitch += CosBump( GoingUpProgress.Mul( 1.5f ).Min( 1f ) ) * 12f;
			target.pitch += ((float)QuickTime).Remap( 0f, 0.2f, 20f, 0f );
			target.roll += ((float)QuickTime).Remap( 0f, 0.3f, 4f, 0f );

			Sway.pitch = Sway.pitch.ApproachWithStopSpeed( target.pitch, 0.1f, 10f );
			Sway.yaw = Sway.yaw.ApproachWithStopSpeed( target.yaw, 0.1f, 10f );
			Sway.roll = Sway.roll.ApproachWithStopSpeed( target.roll, 0.1f, 15f );
		}

		protected static float CosBump( float x )
		{
			return 0.5f - x.Mul( float.Pi * 2f ).Cos() * 0.5f;
		}

		protected static float CosInterpolate( float x )
		{
			return 0.5f - x.Mul( float.Pi ).Cos() * 0.5f;
		}

		void Look3d.IEvents.ModifyEyeAngles( Look3d module, ref Angles eyeAngles, in Angles input, in Angles oldEyeAngles )
		{
			if ( !IsCurrent ) { return; }
			if ( !DisallowYawWrapAround ) { return; }

			var mantleNormalLocal = MantleWorldDirection * WorldRotation.Inverse;
			var mantleYaw = MathF.Atan2( mantleNormalLocal.y, mantleNormalLocal.x ).RadianToDegree();
			var oldYaw = MathX.DeltaDegrees( mantleYaw, oldEyeAngles.yaw );
			var newYaw = oldYaw + input.yaw;
			newYaw = newYaw.Clamp( -175f, 175f );

			eyeAngles = oldEyeAngles with
			{
				pitch = eyeAngles.pitch.Clamp( -89f, 60f ),
				yaw = newYaw + mantleYaw,
				roll = eyeAngles.roll
			};
		}

	}
}
