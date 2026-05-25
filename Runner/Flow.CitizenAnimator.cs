using System;
using Sandbox.Audio;

namespace Flow.States;

public partial class Runner
{
	[Title("Runner Citizen Animator"), Group("Flow/States/Runner"), Icon("accessibility")]
	public class CitizenAnimator : Controller.Module, Controller.IEvents, IEvents, ExecuteInEditor
	{
		[RequireComponent] public Runner Runner { get; set; }
		[RequireComponent] public Look3d Look3d { get; set; }
		[RequireComponent] public Camera3d Camera3d { get; set; }
		[RequireComponent] public Crouch Crouch { get; set; }

		[Property, Group( "Aim" ), Range( 0f, 1f )]
		public float AimStrengthEyes { get; set; } = 1f;

		[Property, Group( "Aim" ), Range( 0f, 1f )]
		public float AimStrengthHead { get; set; } = 1f;
		
		[Property, Group( "Aim" ), Range( 0f, 1f )]
		public float AimStrengthBody { get; set; } = 1f;

		[Property]
		public float RotationAngleLimit { get; set; } = 45f;

		[Property]
		public float RotationSpeed { get; set; } = 1f;

		[Property]
		public bool EnableFootstepSounds { get; set; } = true;

		[Property, Group("Footsteps"), ShowIf( nameof( EnableFootstepSounds ), true )]
		public MixerHandle FootstepMixer { get; set; }

		[Property, Group( "Footsteps" ), Range( 0f, 3f )]
		public float FootstepVolume { get; set; } = 1f;

		protected List<GameObject> InternalGameObjects = [];

		public GameObject RendererGameObject { get; private set; }
		public SkinnedModelRenderer Renderer
		{
			get => _renderer;
			set
			{
				if ( _renderer != value )
				{
					DisableAnimationEvents();
					_renderer = value;
					EnableAnimationEvents();
				}
			}
		}

		private SkinnedModelRenderer _renderer = null;
		const string CitizenGameObjectName = "Citizen";

		protected override void OnEnabled()
		{
			Log.Info( "Citizen Animator enabled." );
			OnCreateInternals();
			UpdateInternals();
		}

		protected override void OnDisabled()
		{
			Log.Info( "CitizenAnimator disabled" );
			DeleteInternals();
		}

        protected void OnCreateInternals() {
            InternalGameObjects.Clear();

            if (!RendererGameObject.IsValid()) { RendererGameObject = null; }
            InternalGameObjects.Add(RendererGameObject ??= GameObject.Children.FirstOrDefault(x => x.Name == CitizenGameObjectName) ??
                ((Func<GameObject>)(() => {
                    var go = new GameObject(true, CitizenGameObjectName);
                    go.SetParent(GameObject);
                    return go;
                }))()
            );

			Renderer = RendererGameObject.GetOrAddComponent<SkinnedModelRenderer>();
		}

		protected void UpdateInternals()
		{
			Renderer.Model = Model.Load( "models/citizen/citizen.vmdl" );
		}

		protected void DeleteInternals()
		{
			InternalGameObjects.ForEach( x => x.Destroy() );
		}

		protected override void OnUpdate()
		{
			if ( Scene.IsEditor ) { return; }

			if ( Renderer.IsValid() )
			{
				UpdateVisibility();
				UpdateAnimation( Renderer );
			}
		}

		protected void EnableAnimationEvents()
		{
			if ( Renderer.IsValid() )
			{
				Renderer.OnFootstepEvent += OnFootstepEvent;
			}
		}

		protected void DisableAnimationEvents()
		{
			if ( Renderer.IsValid() )
			{
				Renderer.OnFootstepEvent -= OnFootstepEvent;
			}
		}

		public void UpdateVisibility()
		{
			var distance = Camera3d.CameraWorldPosition.Distance
			(
				Controller.Collider.WorldTransform.PointToWorld( Controller.Collider.LocalBounds.ClosestPoint
				(
					Controller.Collider.WorldTransform.PointToLocal( Camera3d.CameraWorldPosition )
					) )
			);
			RendererGameObject?.Tags.Set( "viewer", distance < 30f );
		}

		public void UpdateAnimation( SkinnedModelRenderer renderer )
		{
			if ( !renderer.IsValid() ) return;

			UpdateAnimatorVelocity( renderer );
			UpdateAnimatorState( renderer );
			UpdateAnimatorLookDirection( renderer );
			UpdateRotationSpeed( renderer );
			RotateRenderBody( renderer );
		}

		protected TimeSince _timeSinceStep = 0f;

		protected void OnFootstepEvent( SceneModel.FootstepEvent ev )
		{
			if ( Controller.IsOnGround && EnableFootstepSounds && !(_timeSinceStep < 0.2f) )
			{
				_timeSinceStep = 0f;
				PlayFootstepSound( ev.Transform.Position, ev.Volume, ev.FootId );
			}
		}

		public void PlayFootstepSound( Vector3 worldPosition, float volume, int foot )
		{
			volume *= Controller.Velocity.Length.Remap( 0f, 400f );
			if ( volume <= 0.1f || !Controller.GroundSurface.IsValid() ) { return; }

			SoundEvent soundEvent = ((foot == 0) ? Controller.GroundSurface.SoundCollection.FootLeft : Controller.GroundSurface.SoundCollection.FootRight);
			if ( soundEvent == null )
			{
				// Debug stuff
				return;
			}
			SoundHandle soundHandle = GameObject.PlaySound( soundEvent );
			if ( !soundHandle.IsValid() ) { return; }

			soundHandle.FollowParent = false;
			soundHandle.TargetMixer = FootstepMixer.GetOrDefault();
			soundHandle.Volume *= volume * FootstepVolume;
			// More debug stuff
		}

		protected Vector3.SmoothDamped smoothedMove = new( 0f, 0f, 0.1f );
		protected Vector3.SmoothDamped smoothedWish = new( 0f, 0f, 0.2f );
		protected Vector3.SmoothDamped smoothedSkid = new( 0f, 0f, 0.2f );

		protected void UpdateAnimatorVelocity( SkinnedModelRenderer renderer )
		{
			var isClimb = Controller.CurrentState is Climb;
			var isSlide = Controller.CurrentState is Slide;
			var isMantle = Controller.CurrentState is Mantle;

			Rotation worldRotation = renderer.WorldRotation;
			Vector3 velocity = Controller.Velocity;
			Vector3 wishVelocity = Runner.WishVelocity;

			Vector3 target = velocity;
			smoothedMove.Target = velocity;
			smoothedMove.Update( Time.Delta );
			target = smoothedMove.Current;

			Vector3 something = (target - velocity) * 0.5f;
			something = Vector3.Lerp( something, velocity, wishVelocity.Length.Remap( 200f, 0f ) );

			smoothedSkid.Target = something;
			smoothedSkid.Update( Time.Delta );

			something = smoothedSkid.Current;
			something = GetLocalVelocity( worldRotation, something );

			if ( isClimb )
			{
				wishVelocity = Look3d.HorizontalLook.Forward * Controller.Velocity.Dot( WorldTransform.Up );
			}

			if ( isSlide || isMantle )
			{
				wishVelocity = Vector3.Zero;
			}

			renderer.Set( "skid_x", something.x / 400f );
			renderer.Set( "skid_y", something.y / 400f );

			Vector3 target2 = wishVelocity;
			smoothedWish.Target = wishVelocity;
			smoothedWish.Update( Time.Delta );
			target2 = smoothedWish.Current;
			target2 = ApplyDeadZone( target2, 10f );

			target2 = GetLocalVelocity( worldRotation, target2 );
			renderer.Set( "move_direction", GetAngle( target2 ) );
			renderer.Set( "move_speed", target2.Length );
			renderer.Set( "move_groundspeed", target2.WithZ( 0f ).Length );
			renderer.Set( "move_x", target2.x );
			renderer.Set( "move_y", target2.y );
			renderer.Set( "move_z", target2.z );
			Vector3 localVelocity = GetLocalVelocity( worldRotation, wishVelocity );
			renderer.Set( "wish_direction", GetAngle( localVelocity ) );
			renderer.Set( "wish_speed", wishVelocity.Length );
			renderer.Set( "wish_groundspeed", wishVelocity.WithZ( 0f ).Length );
			renderer.Set( "wish_x", localVelocity.x );
			renderer.Set( "wish_y", localVelocity.y );
			renderer.Set( "wish_z", localVelocity.z );
		}

		protected void UpdateAnimatorState( SkinnedModelRenderer renderer )
		{
			var isClimb = Controller.CurrentState is Climb;
			renderer.Set( "b_climbing", isClimb );
			renderer.Set( "b_grounded", Controller.IsOnGround );
			renderer.Set( "duck", Crouch.ProgressSmooth );
		}

		protected void UpdateAnimatorLookDirection( SkinnedModelRenderer renderer )
		{
			renderer.SetLookDirection( "aim_eyes", Look3d.HorizontalLook.Forward, AimStrengthEyes );
			renderer.SetLookDirection( "aim_head", Look3d.HorizontalLook.Forward, AimStrengthHead );
			renderer.SetLookDirection( "aim_body", Look3d.HorizontalLook.Forward, AimStrengthBody );
		}

		private float _animRotationSpeed = 0f;

		private void AddRotationSpeed( Rotation oldRotation, Rotation newRotation )
		{
			float yaw = oldRotation.Angles().yaw;
			float num = MathX.DeltaDegrees( newRotation.Angles().yaw, yaw );
			_animRotationSpeed = (_animRotationSpeed + num).Clamp( -90f, 90f );
		}

		private TimeSince _timeSinceRotationSpeedUpdate;

		private void UpdateRotationSpeed( SkinnedModelRenderer renderer )
		{
			if ( !(_timeSinceRotationSpeedUpdate < 0.1f) )
			{
				renderer.Set( "move_rotationspeed", _animRotationSpeed * 5f );
				_timeSinceRotationSpeedUpdate = 0f;
				_animRotationSpeed = 0f;
			}
		}

		protected void RotateRenderBody( SkinnedModelRenderer renderer )
		{
			Rotation rotation = Look3d.HorizontalLook;
			Vector3 vector = Runner.WishVelocity;
			float num = renderer.WorldRotation.Distance( rotation );
			Rotation worldRotation = renderer.WorldRotation;
			if ( num > RotationAngleLimit )
			{
				float frac = 0.999f - RotationAngleLimit / num;
				Rotation worldRotation2 = Rotation.Lerp( renderer.WorldRotation, rotation, frac );
				renderer.WorldRotation = worldRotation2;
			}

			if ( vector.Length > 10f )
			{
				renderer.WorldRotation = Rotation.Slerp( renderer.WorldRotation, rotation, Time.Delta * 2f * RotationSpeed * vector.Length.Remap( 0f, 100f ) );
			}

			AddRotationSpeed( worldRotation, renderer.WorldRotation );

		}

		protected static float GetAngle( Vector3 localVelocity ) => MathF.Atan2( localVelocity.y, localVelocity.x ).RadianToDegree().NormalizeDegrees();

		protected static Vector3 ApplyDeadZone( Vector3 velocity, float minimum )
		{
			if ( !velocity.IsNearlyZero( minimum ) )
			{
				return velocity;
			}
			return 0f;
		}

		protected static Vector3 GetLocalVelocity( in Rotation rotation, in Vector3 worldVelocity ) => (rotation.Inverse * worldVelocity) * new Vector3( 1f, -1f, 1f );

		void IEvents.OnJump() { Renderer?.Set("b_jump", value: true); }
	}

}
