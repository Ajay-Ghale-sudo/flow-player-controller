namespace Flow;

public partial class Controller
{
	[Property, Feature( "Debug" ), Order( 10000 )]
	public bool ShowInternalComponents { get; set; } = false;

	protected List<Component> InternalComponents = [];
	protected List<Component> InternalGameObjects = [];

	public CapsuleCollider Collider { get; private set; }
	public Capsule Capsule => new( Collider.Start, Collider.End, Collider.Radius - Margin );
	public Rigidbody Body { get; private set; }

	protected void CreateInternalElements()
	{
		InternalComponents.Clear();

		InternalComponents.Add( Collider = GetOrAddComponent<CapsuleCollider>() );
		InternalComponents.Add( Body = GetOrAddComponent<Rigidbody>() );

		InternalGameObjects.Clear();

		UpdateInternalElementsInternal();
	}

	protected void DestroyInternalElements()
	{
		InternalComponents.ForEach( x => x.Destroy() );
		InternalGameObjects.ForEach( x => x.Destroy() );
	}

	public void UpdateInternalElements()
	{
		CreateInternalElements();
		UpdateInternalElementsInternal();
	}

	protected void UpdateInternalElementsInternal()
	{
		if ( Body.IsValid() )
		{
			var mass = Mass;
			Post( x => x.ModifyMass( this, ref mass ) );

			Body.Flags = Body.Flags.WithFlag( ComponentFlags.Hidden, !ShowInternalComponents );
			Body.Enabled = true;
			Body.MassOverride = mass;
			Body.Gravity = false;
			Body.Locking = Locking;
			Body.CollisionEventsEnabled = true;
			Body.CollisionUpdateEventsEnabled = true;
		}
		
		if (Collider.IsValid())
		{
			Collider.Flags = Collider.Flags.WithFlag( ComponentFlags.Hidden, !ShowInternalComponents );

			var radius = Radius;
			var height = Height;
			Post( x => x.ModifyDimensions( this, ref height, ref radius ) );

			var radiusCapped = radius;
			var endCapped = height - radius;
			if ( height < radiusCapped * 2f )
			{
				radiusCapped = height / 2f;
				endCapped = radiusCapped;
			}

			Collider.Radius = radiusCapped - MathF.Min( 0f, radiusCapped / 2f );
			Collider.Start = Vector3.Up * radiusCapped;
			Collider.End = Vector3.Up * endCapped;
		}
	}

}
