namespace Flow;

public partial class Controller
{
	[Property, Group( "Momentum" ), Feature( "Momentum" )]
	public bool UseMomentum { get; set; } = true;
	public float MomentumReference { get; set; } = 150f; // in u/s above sprint. whatever sprintspeed is + 150
	public float MomentumCap { get; set; } = 0f; // actions add speed to the momentum cap
	public float MomentumBaseline = 300f;

	public float Momentum
	{
		get
		{
			var horizontalSpeed = Velocity.SubtractDirection( WorldTransform.Up ).Length;
			var over = horizontalSpeed - MomentumBaseline;
			return MathF.Max( 0f, over / MathF.Max( MomentumReference, 1f ) );
		}
	}

	public void InjectMomentum( float speedAdd, float capRaiseFraction = 1f )
	{
		var horizontal = Velocity.WithZ( 0 );
		var dir = horizontal.IsNearZeroLength ? WorldTransform.Forward : horizontal.Normal;
		Velocity += dir * speedAdd;

		var horizontalSpeed = horizontal.Length + speedAdd;
		MomentumCap = MathF.Max( MomentumCap, horizontalSpeed + speedAdd * capRaiseFraction );
	}

	public void ResetMomentum() => MomentumCap = 0f;
}
