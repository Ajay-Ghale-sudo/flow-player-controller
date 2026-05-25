namespace Flow;

public partial class Controller
{
	protected override void OnValidate()
	{
		CreateInternalElements();
	}

	protected override void OnAwake()
	{
		CreateInternalElements();

		var state = GetComponent<State>();
		if ( state.IsValid() ) { SetNextState( state ); }
	}

	protected override void OnUpdate()
	{

	}

	protected override void OnFixedUpdate()
	{
		if ( Scene.IsEditor ) { UpdateInternalElements(); }
	}

	protected override void OnDestroy()
	{
		DestroyInternalElements();
	}
}
