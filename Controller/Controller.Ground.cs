namespace Flow;

public partial class Controller
{
	[Property, Feature( "Debug" ), ReadOnly, FixedUpdateOnly]
	public GameObject GroundObject
	{
		get => _groundObject.IsValid() ? _groundObject : null;
		set
		{
			if ( !PreventGroundingUntil ) { return; }
			_groundObject = value;
		}
	}
	private GameObject _groundObject = null;
	public bool IsOnGround => GroundObject.IsValid();
	public Surface GroundSurface => GroundObject?.GetComponent<Collider>()?.Surface;

	public TimeUntil PreventGroundingUntil { get; set; } = 0f;

	[FixedUpdateOnly]
	public Vector3 GroundNormal { get; set; } = Vector3.Up;

	public bool IsValidGround( in SceneTraceResult collision )
	{
		if ( !collision.Hit || collision.StartedSolid ) return false;
		
		var isProperAngle = MathF.Acos( collision.Normal.Normal.Dot( WorldTransform.Up ).Clamp( -1f, 1f ) ).RadianToDegree() <= GroundAngle;

		var localTransform = new Transform( collision.EndPosition, WorldRotation );
		var localZ = localTransform.PointToLocal( collision.HitPosition ).z;
		var isCollidingWithFeet = localZ < Radius;
		return isProperAngle && isCollidingWithFeet;
	}
	
	public void PreventGrounding(float seconds = 0f)
	{
		GroundObject = null;
		PreventGroundingUntil = seconds;
	}
}
