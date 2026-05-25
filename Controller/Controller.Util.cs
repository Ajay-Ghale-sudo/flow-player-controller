namespace Flow;

public partial class Controller
{
	public void Push( Vector3 v )
	{
		GroundObject = null;
		Velocity += v;
	}

	public void ApplyGravity( float mult = 1f )
	{
		Velocity += Gravity * Time.Delta * mult;
	}

	public void ApplyHalfGravity() => ApplyGravity( 0.5f );
}
