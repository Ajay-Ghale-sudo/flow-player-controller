namespace Flow.States;

public partial class Runner
{
	[Title( "Flow Runner Ragdoll State" ), Group( "Flow/States/Runner" ), Icon( "airline_seat_individual_suite" )]
	public partial class Ragdoll : Controller.State, Camera3d.IEvents
	{
		[RequireComponent] public FrameOfReferenceIntegrator FrameOfReferenceIntegrator { get; set; }
		public override Controller.Integrator Integrator => FrameOfReferenceIntegrator;

		[RequireComponent] public Runner Runner { get; set; }
		[RequireComponent] public Crouch Crouch { get; set; }
		[RequireComponent] public CitizenAnimator Animator { get; set; }
		[RequireComponent] public Camera3d Camera3d { get; set; }

		/// <summary>
		/// Bone name on the citizen model whose world transform the camera
		/// follows during ragdoll. "head" is the standard s&amp;box citizen bone.
		/// </summary>
		[Property] public string HeadBoneName { get; set; } = "head";

		/// <summary>
		/// Multiplier on each ragdoll body's linear damping (air drag). Values
		/// below 1 = less drag = more sliding/momentum. Cartoony default.
		/// </summary>
		[Property, Group( "Feel" ), Range( 0f, 2f )]
		public float LinearDampingScale { get; set; } = 0.25f;

		/// <summary>
		/// Multiplier on each ragdoll body's angular damping. Values below 1 =
		/// limbs spin freely once moving = more flailing.
		/// </summary>
		[Property, Group( "Feel" ), Range( 0f, 2f )]
		public float AngularDampingScale { get; set; } = 0.08f;

		/// <summary>
		/// Multiplier on each ragdoll body's gravity scale. >1 lands faster
		/// and harder for a more arcadey impact.
		/// </summary>
		[Property, Group( "Feel" ), Range( 0f, 3f )]
		public float GravityScale { get; set; } = 1.25f;

		/// <summary>
		/// Friction override applied to every collider on every ragdoll body.
		/// 0 = perfectly slick, 1 = normal surface friction. Low values let
		/// the body keep sliding after impact instead of immediately stopping,
		/// which also extends the at-rest detection window.
		/// </summary>
		[Property, Group( "Feel" ), Range( 0f, 1f )]
		public float BodyFriction { get; set; } = 0.05f;

		/// <summary>
		/// Initial angular velocity (rad/s per u/s of player speed) applied
		/// around an axis perpendicular to motion. Gives the body a forward
		/// roll/tumble instead of sliding flat. 0 = no induced spin.
		/// </summary>
		[Property, Group( "Feel" ), Range( 0f, 0.5f )]
		public float TumbleStrength { get; set; } = 0.18f;

		/// <summary>
		/// Distance (units) the trace from the ragdoll's mass center looks
		/// downward to consider the body "settled" on a surface. Stand-up is
		/// blocked until both this and the rest check pass.
		/// </summary>
		[Property] public float GroundedTraceDistance { get; set; } = 40f;

		/// <summary>
		/// Per-body speed threshold (u/s). The ragdoll is considered at rest
		/// once every simulated body is below this. Higher = recovers earlier.
		/// </summary>
		[Property] public float StopSpeed { get; set; } = 30f;

		/// <summary>
		/// Time (seconds) spent lerping the crouch back to standing once the
		/// ragdoll has settled. The whole stand-up animation is this long.
		/// </summary>
		[Property] public float GetUpTime { get; set; } = 2f;

		/// <summary>
		/// Minimum time (seconds) the ragdoll must remain active before the
		/// get-up sequence can begin, even if it comes to rest immediately.
		/// </summary>
		[Property] public float MinDownTime { get; set; } = 0.4f;

		/// <summary>
		/// Hard ceiling (seconds) on total ragdoll duration. Failsafe for
		/// bodies that never settle (e.g. resting on a moving rigidbody).
		/// </summary>
		[Property] public float MaxDuration { get; set; } = 8f;

		/// <summary>
		/// If true, the controller capsule tracks the ragdoll's mass center
		/// each frame. Independent of camera follow — the camera tracks the
		/// head bone via Camera3d.IEvents regardless.
		/// </summary>
		[Property] public bool FollowRagdoll { get; set; } = true;

		protected ModelPhysics _physics;
		protected GameObject _ragdollObject;
		protected GameObject _previousParent;
		protected Transform _previousLocalTransform;
		protected SkinnedModelRenderer _suppressedAnimatorRenderer;

		// Camera-relative-to-head pose captured at entry. The camera maintains
		// this offset throughout the ragdoll so there's no snap on entry.
		protected Vector3 _cameraLocalToHeadPosition;
		protected Rotation _cameraLocalToHeadRotation = Rotation.Identity;
		protected bool _cameraOffsetCaptured;

		// Most recent valid ground point the ragdoll's mass center traced
		// down onto during the sim. Used as a teleport rescue if the player
		// ends up wedged in geometry with no room to even crouch.
		protected Vector3? _lastSafeGroundPosition;

		protected TimeSince EnterTime { get; set; } = float.PositiveInfinity;
		protected bool IsGettingUp { get; set; } = false;
		protected float GetUpProgress { get; set; } = 0f;

		public override bool TryEnter()
		{
			if ( IsProxy ) return false;
			if ( Input.Pressed( "ragdoll" ) ) { Log.Info( "Ragdolling!" ); }
			return Input.Pressed( "ragdoll" );
		}

		public override void Enter()
		{
			EnterTime = 0f;
			IsGettingUp = false;
			GetUpProgress = 0f;

			// Seed the safe-position fallback with the player's pre-ragdoll
			// location. If the body never finds a better surface during the
			// sim, the teleport rescue still has somewhere sensible to send.
			_lastSafeGroundPosition = Controller.Position;

			Runner.SprintActive = false;
			Runner.DropLurchTimer();

			var renderer = Animator?.Renderer;
			if ( !renderer.IsValid() )
			{
				Log.Warning( "[Ragdoll] No citizen renderer available; aborting." );
				SetNextState( Runner );
				return;
			}

			_ragdollObject = renderer.GameObject;
			_previousParent = _ragdollObject.Parent;
			_previousLocalTransform = _ragdollObject.LocalTransform;

			// Defensive: a prior ragdoll may have left the renderer or its bone
			// child GameObjects in stale local transforms. Force the renderer
			// back to its expected pose relative to the player BEFORE we capture
			// camera offsets or start physics, so subsequent ragdolls always
			// spawn at the player's current position.
			if ( _previousParent.IsValid() )
			{
				_ragdollObject.LocalTransform = _previousLocalTransform;
			}
			CleanupBoneGameObjects();

			// Capture the camera's pose relative to the head bone NOW, while the
			// bones are still in their pre-ragdoll animated pose. Used during
			// the sim to maintain the entry offset (no snap on activation).
			CaptureCameraHeadOffset( renderer );

			// Stop the citizen animator from setting renderer.WorldRotation on
			// the now-loose ragdoll root — it would pin the body in place /
			// fight the simulation.
			_suppressedAnimatorRenderer = Animator.Renderer;
			Animator.Renderer = null;

			// Detach the renderer so ModelPhysics drives it freely instead of
			// being yanked around by the controller capsule each frame.
			_ragdollObject.SetParent( null, true );

			var inheritedVelocity = Controller.Velocity;

			// Always start from a fresh ModelPhysics — re-using a stale one
			// would skip body re-creation.
			var existing = _ragdollObject.GetComponent<ModelPhysics>();
			if ( existing.IsValid() ) { existing.Destroy(); }

			_physics = _ragdollObject.AddComponent<ModelPhysics>();
			_physics.Renderer = renderer;
			_physics.Model = renderer.Model;
			_physics.MotionEnabled = true;
			_physics.Enabled = true;

			// Copy current animated bone pose AND per-bone velocity into the
			// new physics bodies — this is what makes a ragdoll inherit a
			// running animation's limb motion instead of snapping to bind pose.
			_physics.CopyBonesFrom( renderer, false );

			// Then add the controller's translational velocity on top so the
			// body keeps moving in the direction the player was going.
			AddVelocityToBodies( inheritedVelocity );

			ApplyCartoonTuning();
			ApplyTumble( inheritedVelocity );

			var bodyCount = _physics.Bodies?.Count ?? 0;
			Log.Info( $"[Ragdoll] ModelPhysics started with {bodyCount} bodies." );

			// Park the capsule: no physics motion, no platform-following, no
			// collisions with the simulated bodies, no residual velocity.
			FrameOfReferenceIntegrator.BypassPhysics = true;
			FrameOfReferenceIntegrator.BypassLinkedMovement = true;
			Controller.Collider.Enabled = false;
			Controller.Body.MotionEnabled = false;
			Controller.Velocity = Vector3.Zero;
			Controller.GroundObject = null;
		}

		public override void Move()
		{
			FrameOfReferenceIntegrator.BypassPhysics = true;
			FrameOfReferenceIntegrator.BypassLinkedMovement = true;
			Controller.Collider.Enabled = false;
			Controller.Velocity = Vector3.Zero;

			if ( FollowRagdoll && !IsGettingUp )
			{
				var center = GetRagdollCenter();
				if ( center.HasValue )
				{
					Controller.Position = center.Value;
					WorldPosition = center.Value;
				}
			}

			if ( !IsGettingUp )
			{
				Crouch.Progress = 1f;
				Crouch.IsCrouched = true;

				TrackLastSafePosition();

				var atRest = IsRagdollAtRest();
				var grounded = IsRagdollGrounded();
				if ( (atRest && grounded && EnterTime > MinDownTime) || EnterTime > MaxDuration )
				{
					BeginGetUp();
				}
				return;
			}

			// Stand-up gating with two-tier failsafe:
			//   1. Can stand at full height → run the normal lerp.
			//   2. Blocked at full height but can sit at crouch → exit crouched
			//      immediately; Runner.ThinkCrouch will auto-hold the crouch
			//      while the ceiling persists and pop up when it clears.
			//   3. Can't even fit the crouched capsule (wedged in geometry) →
			//      teleport to the last safe ground point the ragdoll touched,
			//      then exit crouched.
			if ( !CanStandUp() )
			{
				ExitAsCrouched( withTeleport: !CanFitCrouched() );
				return;
			}

			GetUpProgress += Time.Delta / MathF.Max( GetUpTime, 0.0001f );
			GetUpProgress = GetUpProgress.Clamp( 0f, 1f );

			Crouch.Progress = 1f - GetUpProgress;
			if ( GetUpProgress >= 1f )
			{
				Crouch.Progress = 0f;
				Crouch.IsCrouched = false;
				SetNextState( Runner );
			}
		}

		protected bool CanStandUp()
		{
			var standClearance = Controller.Height - Crouch.CrouchedHeight;
			if ( standClearance <= 0f ) return true;
			var tr = Controller.Trace( Controller.Position, Controller.Position + WorldTransform.Up * standClearance );
			return !tr.Hit;
		}

		protected bool CanFitCrouched()
		{
			// Test the crouched capsule at the current position by sweeping
			// it a hair upward. StartedSolid = inside geometry = can't fit.
			var tr = Controller.Trace( Controller.Position, Controller.Position + WorldTransform.Up * 1f );
			return !tr.StartedSolid;
		}

		protected void ExitAsCrouched( bool withTeleport )
		{
			if ( withTeleport && _lastSafeGroundPosition.HasValue )
			{
				Controller.Position = _lastSafeGroundPosition.Value;
				WorldPosition = _lastSafeGroundPosition.Value;
				Log.Info( $"[Ragdoll] Get-up failsafe: teleported to {_lastSafeGroundPosition.Value}." );
			}

			Crouch.Progress = 1f;
			Crouch.IsCrouched = true;
			SetNextState( Runner );
		}

		protected void TrackLastSafePosition()
		{
			var center = GetRagdollCenter();
			if ( !center.HasValue ) return;

			var tr = Scene.Trace
				.Ray( center.Value, center.Value + Vector3.Down * (GroundedTraceDistance * 2f) )
				.IgnoreGameObjectHierarchy( _ragdollObject ?? GameObject )
				.Run();

			// Only record reasonably flat ground (cos(45°) ≈ 0.7) — we don't
			// want to teleport the player onto a steep ramp or a wall normal.
			if ( tr.Hit && tr.Normal.Dot( Vector3.Up ) > 0.7f )
			{
				_lastSafeGroundPosition = tr.EndPosition;
			}
		}

		public override void Exit()
		{
			RestoreRenderer();
			RestoreController();

			_ragdollObject = null;
			_physics = null;
			_suppressedAnimatorRenderer = null;
		}

		protected void BeginGetUp()
		{
			IsGettingUp = true;
			GetUpProgress = 0f;

			// Snap the capsule to where the body settled, traced down to ground
			// so the player doesn't get up floating mid-air on a slope.
			var settle = GetRagdollSettlePosition();
			if ( settle.HasValue )
			{
				Controller.Position = settle.Value;
				WorldPosition = settle.Value;
			}

			RestoreRenderer();
			// Hand control back to the controller capsule; the camera/crouch
			// lerp drives the visual stand-up from here.
			RestoreController();
		}

		protected void RestoreRenderer()
		{
			// Destroy (not just disable) so the SkinnedModelRenderer fully
			// hands its bones back to the animator. Disabling alone has been
			// observed to leave the model frozen in its last physics pose.
			if ( _physics.IsValid() ) { _physics.Destroy(); }
			_physics = null;

			if ( _ragdollObject.IsValid() )
			{
				var parent = _previousParent ?? GameObject;

				// CRITICAL: place the renderer at its final desired world pose
				// while still unparented, THEN reparent with keepWorldPosition.
				// Doing reparent-first-then-fix-local leaves the renderer at
				// (parent * stale_ragdoll_world) for one tick — that junk
				// transform leaks into SceneModel/AnimGraph caches and shows
				// up as the model snapping to out-of-bounds locations and the
				// citizen getting stuck at a tilted stand-up pose.
				var targetWorld = parent.WorldTransform.ToWorld( _previousLocalTransform );

				// Pre-force rotation to match current look so we don't leave
				// the citizen at a sub-45° tilt that CitizenAnimator's
				// auto-rotation won't correct without movement input.
				var look3d = Animator?.Look3d;
				if ( look3d.IsValid() )
				{
					targetWorld = targetWorld.WithRotation( look3d.HorizontalLook );
				}

				_ragdollObject.WorldTransform = targetWorld;
				_ragdollObject.SetParent( parent, true );

				// ModelPhysics leaves the bone child GameObjects behind with
				// their last-physics positions baked into LocalTransform once
				// the Absolute flag is cleared. Strip them now so the next
				// ragdoll's CreateBoneObjects starts from a clean slate.
				CleanupBoneGameObjects();
			}

			if ( Animator.IsValid() && _suppressedAnimatorRenderer.IsValid() )
			{
				Animator.Renderer = _suppressedAnimatorRenderer;
			}
		}

		protected void CleanupBoneGameObjects()
		{
			if ( !_ragdollObject.IsValid() ) return;
			foreach ( var child in _ragdollObject.Children.ToList() )
			{
				if ( child.Flags.Contains( GameObjectFlags.Bone ) )
				{
					child.Destroy();
				}
			}
		}

		protected void RestoreController()
		{
			FrameOfReferenceIntegrator.BypassPhysics = false;
			FrameOfReferenceIntegrator.BypassLinkedMovement = false;
			Controller.Collider.Enabled = true;
			Controller.Body.MotionEnabled = !IsProxy;
		}

		protected Vector3? GetRagdollCenter()
		{
			if ( !_physics.IsValid() ) return null;
			return _physics.MassCenter;
		}

		protected Vector3? GetRagdollSettlePosition()
		{
			var center = GetRagdollCenter();
			if ( !center.HasValue ) return null;

			var trace = Controller.Trace( center.Value, center.Value + WorldTransform.Down * 200f );
			return trace.Hit ? trace.EndPosition : center.Value;
		}

		protected bool IsRagdollAtRest()
		{
			if ( !_physics.IsValid() ) return true;
			foreach ( var body in _physics.Bodies )
			{
				if ( !body.Component.IsValid() ) continue;
				if ( body.Component.Velocity.Length > StopSpeed ) return false;
			}
			return true;
		}

		protected void AddVelocityToBodies( Vector3 velocity )
		{
			if ( !_physics.IsValid() ) return;
			foreach ( var body in _physics.Bodies )
			{
				if ( !body.Component.IsValid() ) continue;
				body.Component.Velocity += velocity;
				body.Component.Sleeping = false;
			}
		}

		protected bool IsRagdollGrounded()
		{
			var center = GetRagdollCenter();
			if ( !center.HasValue ) return false;

			var tr = Scene.Trace
				.Ray( center.Value, center.Value + Vector3.Down * GroundedTraceDistance )
				.IgnoreGameObjectHierarchy( _ragdollObject ?? GameObject )
				.Run();
			return tr.Hit;
		}

		protected Transform? GetHeadBoneTransform()
		{
			var renderer = _suppressedAnimatorRenderer.IsValid() ? _suppressedAnimatorRenderer : Animator?.Renderer;
			if ( !renderer.IsValid() ) return null;
			if ( !renderer.TryGetBoneTransform( HeadBoneName, out var tx ) ) return null;
			return tx;
		}

		protected void CaptureCameraHeadOffset( SkinnedModelRenderer renderer )
		{
			_cameraOffsetCaptured = false;
			if ( !renderer.IsValid() ) return;
			if ( !renderer.TryGetBoneTransform( HeadBoneName, out var head ) ) return;

			// Camera pose expressed in the head bone's local frame at entry.
			_cameraLocalToHeadPosition = head.Rotation.Inverse * (Camera3d.CameraWorldPosition - head.Position);
			_cameraLocalToHeadRotation = head.Rotation.Inverse * Camera3d.CameraWorldRotation;
			_cameraOffsetCaptured = true;
		}

		protected void ApplyCartoonTuning()
		{
			if ( !_physics.IsValid() ) return;
			foreach ( var body in _physics.Bodies )
			{
				var rb = body.Component;
				if ( !rb.IsValid() ) continue;
				rb.LinearDamping *= LinearDampingScale;
				rb.AngularDamping *= AngularDampingScale;
				rb.GravityScale *= GravityScale;

				foreach ( var collider in rb.GameObject.GetComponents<Collider>() )
				{
					collider.Friction = BodyFriction;
				}
			}
		}

		protected void ApplyTumble( Vector3 velocity )
		{
			if ( !_physics.IsValid() ) return;
			if ( TumbleStrength <= 0f ) return;

			var horizontal = velocity.WithZ( 0f );
			if ( horizontal.IsNearZeroLength ) return;

			// Spin around an axis perpendicular to motion (and to up), so the
			// body tumbles forward in the direction it was traveling.
			var axis = Vector3.Cross( horizontal.Normal, Vector3.Up ).Normal;
			var angularVelocity = axis * (horizontal.Length * TumbleStrength);

			foreach ( var body in _physics.Bodies )
			{
				var rb = body.Component;
				if ( !rb.IsValid() ) continue;
				rb.AngularVelocity += angularVelocity;
			}
		}

		void Camera3d.IEvents.ModifyCameraWorldPosition( Camera3d module, ref Vector3 cameraWorldPosition )
		{
			if ( !IsCurrent || IsGettingUp ) return;
			if ( !_cameraOffsetCaptured ) return;
			var head = GetHeadBoneTransform();
			if ( !head.HasValue ) return;
			cameraWorldPosition = head.Value.Position + head.Value.Rotation * _cameraLocalToHeadPosition;
		}

		void Camera3d.IEvents.ModifyCameraWorldRotation( Camera3d module, ref Rotation cameraWorldRotation )
		{
			if ( !IsCurrent || IsGettingUp ) return;
			if ( !_cameraOffsetCaptured ) return;
			var head = GetHeadBoneTransform();
			if ( !head.HasValue ) return;
			cameraWorldRotation = head.Value.Rotation * _cameraLocalToHeadRotation;
		}
	}
}
