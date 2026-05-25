namespace Flow;

[Title( "Flow Controller" ), Group( "Flow" ), Icon( "directions_walk" )]
public partial class Controller : Component, Component.ExecuteInEditor, IScenePhysicsEvents, Component.ICollisionListener
{
	[Property, Group( "Body" ), Range( 1f, 256f, false, true ), Step( 1f )]
	public float Height { get; set; } = 72f;
	[Property, Group( "Body" ), Range( 1f, 128f, false, true ), Step( 1f )]
	public float Radius { get; set; } = 16f;
	[Property, Group( "Body" ), Range( 0f, 4f, false, true ), Step( 0.25f )]
	public float Margin { get; set; } = 0.5f;
	[Property, Group( "Body" ), Range( 1f, 10000f, false, true ), Step( 1f )]
	public float Mass { get; set; } = 500f;

	[Property, Group( "Body" )]
	public TranslationPhysicsLock Locking { get; set; } = new();

	[Property, Group( "Ground" ), Range( 0f, 80f ), Step( 2f )]
	public float GroundAngle { get; set; } = 46f;
	[Property, Group( "Group" )]
	public float StepHeight { get; set; } = 22f;

	[Property, Group( "Gravity" ), Order( 700 )]
	public bool UseVectorGravity = false;
	[Property, Group( "Gravity" ), ShowIf( nameof( UseVectorGravity ), false ), Title( "Gravity Direction" )]
	public Vector3 UnnormalizedGravityDirection { get; set; } = Vector3.Down;
	public Vector3 GravityDirection => UnnormalizedGravityDirection.Normal;
	[Property, Group( "Gravity" ), Order( 700 ), ShowIf( nameof( UseVectorGravity ), false ), Range( 0f, 5000f, false, true ), Step( 50f )]
	public float GravityStrength { get; set; } = 750f;
	[Property, Group( "Gravity" ), Order( 700 ), ShowIf( nameof( UseVectorGravity ), true )]
	public Vector3 VectorGravity { get; set; } = new Vector3( 0f, 0f, -750f );

	public Vector3 DefaultGravity => UseVectorGravity ? VectorGravity : (GravityDirection * GravityStrength);

	public Vector3 Gravity
	{
		get
		{
			var gravity = DefaultGravity;
			Post( x => x.ModifyGravity( this, ref gravity ) );
			return gravity;
		}
	}
}
