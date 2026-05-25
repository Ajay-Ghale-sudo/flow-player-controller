namespace Flow.States;

public partial class Runner
{
	public bool WantSprint => !IsProxy && Input.Down( "run" );
	public bool SprintActive { get; set; } = false;
	public bool IsSprinting => SprintProgress > 0f;
	public float SprintProgress => (!SprintActive || !Controller.IsOnGround || !(Input.AnalogMove.x > 0)) ? 0f : Controller.Velocity.Length.Remap( WalkSpeed, SprintSpeed );
	public TimeSince SprintInputBufferTimer = float.PositiveInfinity;
	public TimeUntil SprintIgnoreDirectionTimer = float.PositiveInfinity;

	public void ThinkSprint()
	{
		if ( GetComponent<Interactor>()?.IsSprintBlocked ?? false ) { SprintActive = false; return; }

		if ( WantSprint ) { SprintInputBufferTimer = 0f; }

		var wishDirectionNotPointingForward = Look3d.WishDirectionUnrotated.x < 0f;
		if ( !SprintIgnoreDirectionTimer ) { wishDirectionNotPointingForward = false; }

		if ( Controller.IsOnGround && (wishDirectionNotPointingForward || Crouch.IsCrouched) )
		{
			SprintActive = false;
		}
		else if ( SprintInputBufferTimer < 1f || SprintByDefault )
		{
			SprintActive = true;
			SprintInputBufferTimer = 0f;
		}

		if ( SprintByDefault && WantSprint ) { SprintActive = false; }

	}
	
	public void IgnoreSprintDirection ( float seconds )
	{
		SprintIgnoreDirectionTimer = seconds;
	}
}
