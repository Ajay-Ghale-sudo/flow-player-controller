namespace Flow.States;

public partial class Runner
{
	[Title( "Flow Runner Climb State" ), Group( "Flow/States/Runner" ), Icon( "keyboard_double_arrow_up" )]
	public partial class Climb : Controller.State, Look3d.IEvents
	{
		[RequireComponent] public FrameOfReferenceIntegrator FrameOfReferenceIntegrator { get; set; }
		public override Controller.Integrator Integrator => FrameOfReferenceIntegrator;

		[RequireComponent] public Runner Runner { get; set; }
		[RequireComponent] public Look3d Look3d { get; set; }
		[RequireComponent] public Crouch Crouch { get; set; }

		[Property]
        public float VerticalSpeedStart { get; set; } = 200f;

        [Property]
        public float VerticalSpeedEnd { get; set; } = 50f;

        [Property]
        public float VerticalAcceleration { get; set; } = 500f;

        [Property]
        public float VerticalDeceleration { get; set; } = 300f;

        [Property]
        public float HorizontalSpeed { get; set; } = 250f;

        [Property]
        public float HorizontalAcceleration { get; set; } = 200f;

        [Property]
        public float HorizontalDeceleration { get; set; } = 500f;

        [Property]
        public float MaxHeight { get; set; } = 100f;

        [Property]
        public float ZoneMaxHeight { get; set; } = 150f;

        [Property]
        public float EndBoostHeight { get; set; } = 28f;

        [Property]
        public float BaseDetachPenalty { get; set; } = 128f;

        [Property]
        public float DetachSpeed { get; set; } = 70f;

        [Property]
        public float MaxNonUpwardsDuration { get; set; } = 1f;

        [Property]
        public bool JumpSlantedWallBoost { get; set; } = false;

        [Property]
        public TagSet UnclimbableTags { get; set; } = [];

        [Property]
        public bool UseLadders { get; set; } = true;

        [Property]
        public TagSet LadderTags { get; set; } = ["ladder"];

        [Header("Mini")]
        [Property, Group("Zones")]
        public float ZoneMiniHeight { get; set; } = 19f;

        [Property, Group("Zones")]
        public float ZoneMiniJumpHorizontalSpeed { get; set; } = 188f;

        [Property, Group("Zones")]
        public float ZoneMiniJumpHeight { get; set; } = 29f;

        [Header("Green")]
        [Property, Group("Zones")]
        public float ZoneGreenHeight { get; set; } = 28f;

        [Property, Group("Zones")]
        public float ZoneGreenJumpHorizontalSpeed { get; set; } = 228f;

        [Property, Group("Zones")]
        public float ZoneGreenMaxJumpHeight { get; set; } = 100f;

        public Vector3 ZoneBaseline { get; set; } = Vector3.Zero;
        public float ZoneCurrentHeight => (WorldPosition - ZoneBaseline).Dot(WorldTransform.Up);
        public Vector3? AttachPoint { get; set; } = null;
        public float AttachPointCurrentHeight => (WorldPosition - AttachPoint.Value).Dot(WorldTransform.Up);

        public GameObject Object { get; set; } = null;
        public Vector3 Normal { get; set; } = Vector3.Zero;
        protected Vector3 NormalHorizontal => Normal.SubtractDirection(WorldTransform.Up).Normal;

        protected Vector3 UpDir => WorldTransform.Up.SubtractDirection(Normal).Normal;
        protected Vector3 RightDir => Look3d.HorizontalLook.Right.SubtractDirection(Normal).Normal;

        public float NonUpwardsTimer { get; set; } = 0f;
        public float WishDirectionDetachTimer { get; set; } = 0f;

        public TimeSince StartTime { get; set; } = float.PositiveInfinity;
        public TimeSince EndTime { get; set; } = float.PositiveInfinity;
        public TimeSince EndBoostTime { get; set; } = float.PositiveInfinity;

		protected List<Vector3> _attachVelocitiesUtil = [];

		public SceneTraceResult Trace() => Controller.Trace( Controller.Position, Controller.Position + Look3d.HorizontalLook.Forward * 4f);

		protected bool Check()
		{
			var isOnLadder = CheckLadders();
			if ( Controller.IsOnGround && !isOnLadder ) { return false;	}

			if ( Crouch.IsCrouched || Runner.WantCrouch ) { return false; }

			var trace = Trace();
			if ( !trace.Hit ) { return false; }
			if ( UnclimbableTags.HasAny( trace.Tags ) ) { return false; }

			var angle = MathX.RadianToDegree( MathF.Acos( trace.Normal.Dot( WorldTransform.Up ).Clamp( -1f, 1f ) ) );

			if ( angle < Controller.GroundAngle + 1f || angle > 134f ) { return false; }

			var horizontalNormal = trace.Normal.SubtractDirection( WorldTransform.Up ).Normal;

			if ( Look3d.HorizontalLook.Forward.Dot( -horizontalNormal ) < 0.7 ) { return false;	}

			if ( trace.Normal.Dot( WorldTransform.Up ) < 0f ) {	trace.Normal = horizontalNormal; }

			var move = trace.Direction * trace.Distance.Sub( Controller.Margin ).Max( 0f ).Mul( 0.5f );
			WorldPosition += move;
			Controller.Position += move;

			Normal = trace.Normal;
			Object = trace.GameObject;
			return true;
		}

		public override bool TryEnter()
		{
			if ( Controller.CurrentState != Runner ) { return false; }
			if ( GetComponent<Interactor>()?.IsAdvancedMovementBlocked ?? false ) { return false; }

			var isOnLadder = CheckLadders();
			if ( Controller.Velocity.Dot( WorldTransform.Up ) > 50f && !isOnLadder ) { return false; }

			// Higher than maximum climb height of the climb zones
			if ( ZoneCurrentHeight > ZoneMaxHeight && !isOnLadder ) { return false; }

			// New attach point can't be higher than previous one, unless the wall angles differ by more than 25.842deg
			// (< 0.9 dot product, lol)
			var trace = Trace();
			var horizontalNormal = trace.Normal.SubtractDirection( WorldTransform.Up ).Normal;
			if ( AttachPoint.HasValue && !isOnLadder )
			{
				if ( horizontalNormal.Dot( NormalHorizontal ) > 0.9 )
				{
					if ( AttachPointCurrentHeight > MaxHeight )
					{
						Log.Info( $"[Climb.TryEnter] blocked: attach point height {AttachPointCurrentHeight:F1} > {MaxHeight}" );
						return false;
					}
				}
			}

			if ( Input.AnalogMove.x <= 0f && !isOnLadder ) {
				if ( (GetComponent<Mantle>()?.DropTime ?? float.PositiveInfinity) > 0.1f )
				{
					var lookingIntoWall = trace.Hit && Look3d.HorizontalLook.Forward.Dot( -horizontalNormal ) >= 0.7f;
					if ( !lookingIntoWall && !_attachVelocitiesUtil.Any( x => x.Dot( -horizontalNormal ) > 10f ) )
					{
						return false;
					}
				}
				else if ( Input.AnalogMove.x < -0.5f )
				{
					return false;
				}
			}

			if ( !Check() ) { return false; }

			return true;
		}
		
		public override void Enter()
		{
			Log.Info( $"Entering climb state. {Time.Now}" );
            AttachPoint = WorldPosition;
            NonUpwardsTimer = 0f;
            StartTime = 0f;
        }

        public override void Process() 
		{
			_attachVelocitiesUtil.Add( FrameOfReferenceIntegrator.PreIntegrateVelocity );
			if ( _attachVelocitiesUtil.Count > 15 ) 
			{
                _attachVelocitiesUtil.RemoveAt(0);
            }

			if ( Controller.IsOnGround || ZoneCurrentHeight < 0f ) { ZoneBaseline = WorldPosition; }

            AlignZoneBaseline();
			// DebugOverlay.Sphere( new Sphere( ClimbZoneBaseline, 1f ) );

			if ( Controller.IsOnGround && !CheckLadders() ) { AttachPoint = null; }
        }

        public override void Move() {
			if ( Controller.Velocity.Dot( -Normal ) > 0f ) 
			{
				Controller.Velocity = Controller.Velocity.SubtractDirection( Normal );
            }

            Runner.ThinkSprint();
            Runner.ThinkCrouch();

			if ( !Check() ) 
			{
                Detach();
                return;
            }

            var isOnLadder = CheckLadders();

            FrameOfReferenceIntegrator.FrameOfReference = Object;

			if ( Runner.WantAnyJump ) {
                var inMiniZone = ZoneCurrentHeight <= ZoneMiniHeight;
                var jumpHeight = ZoneMiniJumpHeight;
                var horizontalSpeed = ZoneMiniJumpHorizontalSpeed;
				if ( !inMiniZone )
				{
					jumpHeight = ( ZoneCurrentHeight - ZoneMiniHeight ).Remap(
                        ZoneGreenHeight * 0.5f,
                        ZoneGreenHeight * 1.25f,
                        ZoneGreenMaxJumpHeight,
                        0f
                    );

                    horizontalSpeed = ZoneGreenJumpHorizontalSpeed;
					if ( ZoneCurrentHeight <= ZoneMiniHeight + ZoneGreenHeight * 1.25f ) 
					{
						Controller.Velocity = Controller.Velocity.SubtractDirection( WorldTransform.Up );
                    }
                }

				Detach( !inMiniZone );
				Runner.Jump( jumpHeight );

                var climbJumpNormal = JumpSlantedWallBoost ? Normal : NormalHorizontal;
				Controller.Velocity += climbJumpNormal * (horizontalSpeed - DetachSpeed);

                return;
            }

			if ( Look3d.WishDirectionHorizontal.Normal.Dot( -NormalHorizontal ) < -0.7 && (!isOnLadder || Controller.IsOnGround) ) 
			{
                WishDirectionDetachTimer += Time.Delta;
				if ( WishDirectionDetachTimer > 0.05f )  
				{
                    Detach();
                    return;
                }
            } else { WishDirectionDetachTimer = 0f; }

			var climbProgress = MathF.Max( ZoneCurrentHeight / ZoneMaxHeight, AttachPointCurrentHeight / MaxHeight );
			if ( climbProgress >= 1f && !isOnLadder ) 
			{
                Detach();
                EndBoost();
                return;
            }

			if ( Controller.Velocity.Dot( WorldTransform.Up ) < 20f && !isOnLadder ) {
                NonUpwardsTimer += Time.Delta;
                if (NonUpwardsTimer > MaxNonUpwardsDuration) 
				{
                    Detach();
                    return;
                }
            }

            var climbVertical = Input.AnalogMove.x;
			if ( !isOnLadder ) { climbVertical = MathF.Max( climbVertical, -0.001f );
            } else if (climbVertical > 0f) { Controller.PreventGrounding( 0.1f ); }

            if (Controller.Velocity.Dot(UpDir) < -MaxHeight * 2f && MathF.Abs(Input.AnalogMove.y) > 0.7f) { climbVertical = MathF.Max( 0.7f, climbVertical ); }

            var climbVerticalWishDirection = climbVertical * UpDir;
            climbVerticalWishDirection = climbVerticalWishDirection.Normal;
			var gravityAmount = Controller.Gravity.Dot( -UpDir );
			var climbVerticalSpeed = climbProgress.Remap( 0f, 1f, VerticalSpeedStart, VerticalSpeedEnd );
            var climbVerticalDeceleration = VerticalDeceleration;

			if ( !isOnLadder ) 
			{ climbVerticalSpeed += gravityAmount * Time.Delta;	}
			else { climbVerticalSpeed = VerticalSpeedStart; }

			var wishDirectionOnlyY = (new Vector3( 0f, Input.AnalogMove.y, 0f ) * Look3d.HorizontalLook).ClampLength( 1f );
			var climbHorizontal = wishDirectionOnlyY.SubtractDirection( NormalHorizontal ).Dot( RightDir ) * 2f;
			climbHorizontal = climbHorizontal.Clamp( -1f, 1f );
            var climbHorizontalWishDirection = climbHorizontal * RightDir;

            var climbHorizontalSpeed = HorizontalSpeed;
            if (Input.AnalogMove.x > 0f && !isOnLadder) { climbHorizontalSpeed = climbProgress.Remap( 0f, 1f, HorizontalSpeed, VerticalSpeedEnd + 50f ); }

			if ( climbVertical == 0f ) 
			{
				var factor = MathF.Abs( wishDirectionOnlyY.SubtractDirection( NormalHorizontal ).Dot( RightDir ) ).Remap( 1f, 0.8f );
				factor = MathF.Max( factor, 1f - NonUpwardsTimer );
				factor = MathF.Min( factor, MathF.Abs( Input.AnalogMove.y ) );
				climbVerticalDeceleration += gravityAmount * factor;
            }

			if ( isOnLadder )
			{
                climbVerticalDeceleration = 9999f;
                climbHorizontalSpeed *= 0.5f;
            }

            var (horizontalVelocity, verticalVelocity) = Controller.Velocity.SplitByNormal(UpDir);
            Controller.Velocity = horizontalVelocity;
            Runner.Decelerate(climbHorizontalWishDirection, !isOnLadder ? HorizontalDeceleration : 9999f, climbHorizontalSpeed);
            Runner.Accelerate(climbHorizontalWishDirection, !isOnLadder ? HorizontalAcceleration : 9999f, climbHorizontalSpeed);
            horizontalVelocity = Controller.Velocity;

            Controller.Velocity = verticalVelocity;
			if ( !isOnLadder ) { Controller.Velocity -= WorldTransform.Up * gravityAmount * Time.Delta; }

            var climbVerticalAcceleration = VerticalAcceleration;
			if ( isOnLadder ) { climbVerticalAcceleration = 9999f; }

            Runner.Decelerate(climbVerticalWishDirection, climbVerticalDeceleration, climbVerticalSpeed);
            Runner.Accelerate(climbVerticalWishDirection, climbVerticalAcceleration + (isOnLadder ? 0f : gravityAmount), climbVerticalSpeed);
            verticalVelocity = Controller.Velocity + WorldTransform.Up * gravityAmount * Time.Delta;

            Controller.Velocity = horizontalVelocity + verticalVelocity;
			if ( isOnLadder ) {
                Controller.Position += WorldTransform.Down * gravityAmount * Time.Delta * 0.01f;
            }
        }

        protected bool CheckLadders() 
		{
			if ( !UseLadders ) { return false; }
			foreach ( Collider item in Controller.Body.Touching )
			{
				if ( item.Tags.HasAny( LadderTags ) )
				{
                    return true;
                }
            }
            return false;
        }


		public void Detach( bool doublePenalty = false ) 
		{
			SetNextState( Runner );
            EndTime = 0f;

            var penalty = BaseDetachPenalty;
			if ( doublePenalty ) { penalty *= 2f; }

            ZoneBaseline += WorldTransform.Down * penalty;

			if ( CheckLadders() ) 
			{
                AttachPoint = null;
                ZoneBaseline = WorldPosition;
            }

			if ( !Controller.IsOnGround ) { Controller.Velocity += Normal * DetachSpeed; }

            Controller.GroundObject = null;
            FrameOfReferenceIntegrator.FrameOfReference = null;
        }

        public void EndBoost() 
		{
            EndBoostTime = 0f;
            var boost = EndBoostHeight;

			var totalGreenZoneHeight = ZoneMiniHeight + ZoneGreenHeight; // 47?
			var extra = MathF.Min( ZoneCurrentHeight + totalGreenZoneHeight + MaxHeight, 10f );
            boost += extra;

			if ( boost <= 0f ) { return; }

			Controller.Velocity = Controller.Velocity.SubtractDirection( WorldTransform.Up );
			Controller.Velocity += WorldTransform.Up * MathF.Sqrt( Controller.Gravity.Dot( WorldTransform.Down ) * 2f * boost );
        }

		private static float CopySignPow( float value, float exponent ) => float.CopySign( MathF.Pow( MathF.Abs( value ), exponent ), value );


        public void AlignZoneBaseline() { ZoneBaseline = (ZoneBaseline - WorldPosition).ProjectOnNormal( WorldTransform.Up ) + WorldPosition; }

        protected float SwayPitch { get; set; } = 0f;
        protected override void OnUpdate() 
		{
			var start = ((float)StartTime).Remap( 0f, 0.5f, 10f, 0f );
			var end = ((float)EndTime).Remap( 0f, 0.3f, 10f, 0f );
			var endboost = ((float)EndBoostTime).Remap( 0f, 0.5f, -10f, 0f );
			SwayPitch = SwayPitch.ApproachWithStopSpeed( start + end + endboost, 0.1f, 8f );
        }

		void Look3d.IEvents.ModifyLookSway( Look3d module, ref Angles sway ) { sway.pitch += SwayPitch; }
		
	}
}
