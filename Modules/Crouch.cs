namespace Flow.Modules;

[Title( "Flow Crouch Module" ), Group( "Flow/Modules" ), Icon( "move_down" )]
public class Crouch : Controller.Module, Controller.IEvents, Camera3d.IEvents
{
	[Property, Group( "Dimensions" ), Range( 1f, 256f, false, true ), Step( 1f ), Sync( SyncFlags.FromHost )]
	public float CrouchedHeight { get; set; } = 36f;
	[Property, Group( "Dimensions" ), Range( 1f, 256f, false, true ), Step( 1f ), Sync( SyncFlags.FromHost )]
	public float CrouchedCameraHeight { get; set; } = 32f;

	[Property, Range( 0f, 1f ), Sync]
	public float Progress { get => _crouchProgress; set => _crouchProgress = value.Clamp( 0f, 1f ); }
	private float _crouchProgress = 0f;
	public float ProgressSmooth => Easing.Smoothstep( PreviousProgress.LerpTo( Progress, Scene.GetPhysicsInterpolationFraction() ) );

	[Property, Sync, Change( nameof( OnIsCrouchChanged ) )]
	public bool IsCrouched { get; set; } = false;

	protected float PreviousProgress { get; set; } = 0f;

	protected override void OnFixedUpdate()
	{
		PreviousProgress = Progress;
	}

	void Controller.IEvents.ModifyDimensions( Controller controller, ref float height, ref float radius )
	{
		if ( IsCrouched ) height = CrouchedHeight;
	}

	void Camera3d.IEvents.ModifyCameraHeight( Camera3d module, ref float cameraHeight )
	{
		if ( Scene.IsEditor ) { return; }
		cameraHeight = cameraHeight.LerpTo( CrouchedCameraHeight, ProgressSmooth );
	}

	public void OnIsCrouchChanged()
	{
		Controller.UpdateInternalElements();

	}

	public interface IEvents : ISceneEvent<IEvents>
	{
		virtual void OnIsCrouchChanged( Crouch module, bool value ) { }
		virtual void OnCrouch( Crouch module ) { }
		virtual void OnUnCrouch( Crouch module ) { }
	}

	public void Post( Action<IEvents> action ) => IEvents.PostToGameObject( GameObject, action );
}
