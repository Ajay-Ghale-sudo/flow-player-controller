namespace Flow.States;

public partial class Runner : Crouch.IEvents
{
	[RequireComponent] public Crouch Crouch { get; set; }

	public bool WantCrouch => !IsProxy && Input.Down( "duck" );

	public void ThinkCrouch( float speedMultiplier = 1f )
	{
		var prevCrouchProgress = Crouch.Progress;
		Crouch.Progress += Time.Delta / (WantCrouch ? CrouchTime : -CrouchTime / 2f) * speedMultiplier;
		Crouch.Progress = Crouch.Progress.Clamp( 0f, 1f );

		if ( Crouch.IsCrouched && !WantCrouch )
		{
			if ( Controller.Trace( Controller.Position, Controller.Position + WorldTransform.Up * (Controller.Height - Crouch.CrouchedHeight) ).Hit )
			{
				Crouch.Progress = 1f;
			}
		}

		if ( Crouch.Progress < 1f && prevCrouchProgress >= 1f )
		{
			Crouch.IsCrouched = false;
		}

		if ( Crouch.Progress <= 0f && prevCrouchProgress > 0f )
		{
			Crouch.IsCrouched = false;
		}

		if ( Crouch.Progress >= 1f && prevCrouchProgress < 1f )
		{
			Crouch.IsCrouched = true;
		}
	}

	public void OnCrouch( Crouch module )
	{

	}
	
	public void OnUnCrouch(Crouch module)
	{
		if ( Controller.IsOnGround ) { return; }
		var offset = CrouchedAerialOffset * Controller.Height;
		var trace = Controller.Trace( Controller.Position, Controller.Position - WorldTransform.Up * offset );
		var move = trace.EndPosition - Controller.Position;
		Controller.Position += move;
		WorldPosition += move;
	}
}
