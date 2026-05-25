namespace Flow;

public partial class Controller
{
	public abstract class Module : Component, IEvents
	{
		[RequireComponent] public Controller Controller { get; set; }
	}
}
