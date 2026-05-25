namespace Flow.Modules;

[Title( "Flow Camera Module" ), Group( "Flow/Modules" ), Icon( "cameraswitch" )]
public class Camera3d : Controller.Module, Look3d.IEvents
{
	[RequireComponent] public Look3d Look3d { get; set; }

	[Property, Title( "Camera Height" ), Range( 1f, 256f, false ), Step( 1f ), Sync( SyncFlags.FromHost )]
	public float BaseCameraHeight { get; set; } = 64f;

	[Property, Range( 0f, 4f, false )] public float SwayStrength { get; set; } = 1f;
	[Property] public bool UseFovFromPreferences { get; set; } = true;
	[Property, ShowIf( nameof( UseFovFromPreferences ), false )] public float Fov { get; set; } = 75f;
	[Property] public GameObject CameraMimic { get; set; } = null;
	public CameraComponent CameraComponent => Scene?.Camera;
	public GameObject Camera => Scene?.Camera?.GameObject;

	public Vector3 CameraWorldPosition { get; protected set; }
	public Rotation CameraWorldRotation { get; protected set; }
	public Vector3 CameraWorldPositionWithoutOffset { get; protected set; }
	public float CameraHeight { get; protected set; }
	public Vector3 CameraOffset { get; protected set; }

	protected override void OnUpdate()
	{
		if ( Scene.IsEditor || IsProxy ) return;
		if ( !Camera.IsValid() ) return;
		if ( !CameraComponent.IsMainCamera ) return;

		UpdateFov();
		UpdateCameraPosition();
	}

	public void UpdateFov()
	{
		var fieldOfView = UseFovFromPreferences ? Preferences.FieldOfView : Fov;
		Post( x => x.ModifyCameraFov( this, ref fieldOfView ) );
		CameraComponent.FieldOfView = fieldOfView;
	}

	public void UpdateCameraPosition()
	{
		var cameraHeight = BaseCameraHeight;
		Post( x => x.ModifyCameraHeight( this, ref cameraHeight ) );
		CameraHeight = cameraHeight;

		var positionWithHeight = WorldTransform.PointToWorld( new Vector3( 0f, 0f, cameraHeight ) );
		CameraWorldPositionWithoutOffset = positionWithHeight;

		var cameraWorldPosition = positionWithHeight;
		Post( x => x.ModifyCameraWorldPosition( this, ref cameraWorldPosition ) );
		CameraWorldPosition = cameraWorldPosition;
		Camera.WorldPosition = CameraWorldPosition;

		if ( CameraMimic.IsValid() ) { CameraMimic.WorldTransform = Camera.WorldTransform; }

	}

	void Look3d.IEvents.Look( Look3d module, in Rotation look, in Rotation finalLook )
	{
		CameraWorldRotation = look;
		if ( SwayStrength >= 1f ) { CameraWorldRotation = finalLook; }

		if ( SwayStrength > 0f ) { CameraWorldRotation = Look3d.Look.SlerpTo(Look3d.FinalLook, SwayStrength, false); }

		var rot = CameraWorldRotation;
		Post( x => x.ModifyCameraWorldRotation( this, ref rot ) );
		CameraWorldRotation = rot;

		Camera.WorldRotation = CameraWorldRotation;

		if ( CameraMimic.IsValid() ) { CameraMimic.WorldTransform = Camera.WorldTransform; }

		UpdateCameraPosition();
	}

	public interface IEvents : ISceneEvent<IEvents>
	{
		virtual void ModifyCameraHeight( Camera3d module, ref float cameraHeight ) { }
		virtual void ModifyCameraOffset( Camera3d module, ref Vector3 cameraOffset ) { }
		virtual void ModifyCameraWorldPosition( Camera3d module, ref Vector3 cameraWorldPosition ) { }
		virtual void ModifyCameraWorldRotation( Camera3d module, ref Rotation cameraWorldRotation ) { }
		virtual void ModifyCameraFov( Camera3d module, ref float fieldOfView ) { }
	}

	public void Post( Action<IEvents> action ) => IEvents.PostToGameObject( GameObject, action );
}
