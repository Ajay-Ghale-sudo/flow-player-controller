namespace Flow.Modules;

[Title( "Flow Look Module" ), Group( "Flow/Modules" ), Icon( "visibility" )]
public class Look3d : Controller.Module
{
	[Property, Range( 0f, 10f, false ), Step( 0.01f )] public float Sensitivity { get; set; } = 1f;
	[Property, Range( -89f, 89f ), Step( 1f )] public float PitchMin { get; set; } = -89f;
	[Property, Range( -89f, 89f ), Step( 1f ), Validate( nameof( ValidatePitchMinMax ), "Pitch min is greater than pitch max.", LogLevel.Warn )]
	public float PitchMax { get; set; } = 89f;

	protected bool ValidatePitchMinMax( float value ) => PitchMin <= value;

	[Property, Range( 0.1f, 100f, false )] public float RollDecay { get; set; } = 10f;
	[Property] public bool ReverseMouseYawUpsideDown { get; set; } = true;

	public Rotation Look { get; set; } = Rotation.Identity;
	public Rotation HorizontalLook => EyeAnglesToLook( LookToEyeAngles( Look ).WithPitch( 0f ).WithRoll( 0f ) );
	public Rotation LookSway { get; set; } = Rotation.Identity;
	public Angles LookSwayAngles => LookSway.Angles();
	public Rotation FinalLook => Look * LookSway;
	public Angles EyeAngles { get => LookToEyeAngles( Look ); set => Look = EyeAnglesToLook( value ); }
	public Angles FinalEyeAngles => LookToEyeAngles( FinalLook );
	protected bool LookingUpsideDown => Look.Up.Dot( WorldTransform.Up ) < 0f;

	protected Angles SanitizeAngles( in Rotation rotation )
	{
		var angles = new Angles( rotation.Pitch(), rotation.Yaw(), rotation.Roll() );
		if ( (WorldRotation * rotation).Up.Dot( WorldTransform.Up ) <= 0f )
		{
			angles.yaw = (angles.yaw + 360f) % 360f - 180f;
			angles.roll = (180f - angles.roll + 270f) % 180f - 90f;
			angles.pitch = (360f - angles.pitch) % 360f - 180f;
		}

		return angles;
	}

	protected Angles LookToEyeAngles( in Rotation look ) => SanitizeAngles( WorldRotation.Inverse * look );
	protected Angles EyeAnglesToLook( in Angles angles ) => WorldRotation * Rotation.From( angles );

	public static Vector3 WishDirectionUnrotated => Input.AnalogMove.ClampLength( 1f );
	public Vector3 WishDirectionFree => (WishDirectionUnrotated * Look).ClampLength( 1f );
	public Vector3 WishDirectionHorizontal => (WishDirectionUnrotated * HorizontalLook).ClampLength( 1f );


	protected override void OnUpdate()
	{
		if ( Scene.IsEditor || IsProxy ) return;

		var swayAngles = Angles.Zero;
		Post( x => x.ModifyLookSway( this, ref swayAngles ) );
		var sway = Rotation.From( swayAngles );
		Post( x => x.ModifyLookSway( this, ref sway ) );
		LookSway = sway;


		Angles input = Input.AnalogLook;

		if ( LookingUpsideDown && ReverseMouseYawUpsideDown ) input.yaw = -input.yaw;

		var oldEyeAngles = EyeAngles;
		var eyeAngles = oldEyeAngles with
		{
			pitch = (EyeAngles.pitch + input.pitch).Clamp( PitchMin, PitchMax ),
			yaw = oldEyeAngles.yaw + input.yaw,
			roll = oldEyeAngles.roll.ApproachWithStopSpeed( 0f, 0.1f, RollDecay )
		};

		Post( x => x.ModifyEyeAngles( this, ref eyeAngles, input, oldEyeAngles ) );

		EyeAngles = eyeAngles;
		Post( x => x.Look( this, Look, FinalLook ) );
	}

	public interface IEvents : ISceneEvent<IEvents>
	{
		virtual void ModifyEyeAngles(Look3d module, ref Angles eyeAngles, in Angles input, in Angles oldEyeAngles) { }
        virtual void ModifyLookSway(Look3d module, ref Rotation sway) { }
        virtual void ModifyLookSway(Look3d module, ref Angles sway) { }
        virtual void Look(Look3d module, in Rotation look, in Rotation finalLook) { }
	}

	public void Post( Action<IEvents> action ) => IEvents.PostToGameObject( GameObject, action );
}
