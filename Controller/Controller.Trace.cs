namespace Flow;

public partial class Controller
{
	[Property, Feature( "Debug" ), Order( 100000 )] 
	public bool DebugOverlayTrace { get; set; } = false;
	[Property, Feature( "Debug" ), Order( 10000 ), ShowIf( nameof( DebugOverlayTrace ), true )]
	public float DebugOverlayTraceDuration { get; set; } = 0.1f;

	public struct MoveAndSlideParameters
	{
		public MoveAndSlideParameters() { }
		public bool FlatGround { get; set; } = true;
		public bool FlatWalls { get; set; } = true;
		public bool StopAtGround { get; set; } = false;
	}

	public struct MoveAndSlideResult( in Vector3 position, in Vector3 motion )
	{
		public List<SceneTraceResult> Collisions { get; set; } = [];
		public List<Vector3> Normals { get; set; } = [];
		public Vector3 StartPosition { get; set; } = position;
		public Vector3 EndPosition { get; set; } = position;
		public Vector3 Motion { get; set; } = motion;
		public float DistanceTraveled { get; set; } = 0f;

		public readonly float Distance => StartPosition.Distance( EndPosition );
		public readonly bool Hit => Collisions.Count != 0;
		public readonly Vector3 Travel => EndPosition - StartPosition;
		public readonly bool IsStuck => Collisions.Any( x => x.StartedSolid );
	}

	public MoveAndSlideResult MoveAndSlideWithStep( in Vector3 from, in Vector3 motion )
	{
		var noStep = MoveAndSlide( from, motion, new MoveAndSlideParameters() );

		var headRoomTrace = Trace( from, from + WorldTransform.Up * StepHeight );
		float headroom = headRoomTrace.Distance;
		var stepUpFrom = from + WorldTransform.Up * headroom;
		var step = MoveAndSlide( stepUpFrom, motion, new MoveAndSlideParameters() );

		if ( step.IsStuck ) return noStep;

		var stepDownTo = step.EndPosition + WorldTransform.Down * (StepHeight * 2f + Margin);
		var downTrace = Trace( step.EndPosition, stepDownTo );

		if ( !downTrace.Hit ) return noStep;

		downTrace.Normal = GetRealNormal( downTrace );
		if ( !IsValidGround( downTrace ) || downTrace.Distance < Margin / 2f ) { return noStep; }

		step.EndPosition = downTrace.EndPosition + WorldTransform.Up * Margin;
		step.Collisions.Add( downTrace );

		var verticalOffset = (step.EndPosition - from).Dot( WorldTransform.Up );
		if ( verticalOffset > -Margin * 2f )
		{
			var noStepTravel = (noStep.EndPosition - from).SubtractDirection( WorldTransform.Up ).Length;
			var stepTravel = (step.EndPosition - from).SubtractDirection( WorldTransform.Up ).Length;
			if ( noStepTravel >= stepTravel ) return noStep;
		}

		WorldPosition += verticalOffset * WorldTransform.Up;

		return step;
		
	}

	public Vector3 GetRealNormal( in SceneTraceResult trace )
	{
		const float EPS = 0.02f; // epsilon to avoid precision issues
		var checkPos = trace.HitPosition - trace.Normal * EPS * 0.1f;
		var physicsTrace = Scene.PhysicsWorld.Trace.Ray( checkPos + WorldTransform.Up * EPS, checkPos + WorldTransform.Down * EPS ).Run();

		if ( !physicsTrace.Hit || physicsTrace.StartedSolid || physicsTrace.Normal.IsNearZeroLength ) { return trace.Normal; }

		return physicsTrace.Normal.Normal;
	}

	public MoveAndSlideResult MoveAndSlide(in Vector3 from, Vector3 motion, in MoveAndSlideParameters parameters )
	{
		const int MAX_ITERATIONS = 6;

		var result = new MoveAndSlideResult( from, motion );
		var planes = new List<Vector3>();
		

		for ( int iteration = 0; iteration < MAX_ITERATIONS; iteration++ )
		{
			var velocity = motion / Time.Delta;
			if ( velocity.IsNearZeroLength ) { break; }

			var target = result.EndPosition + motion;
			var trace = Trace( result.EndPosition, target + motion.Normal * Margin );
			if ( DebugOverlayTrace && trace.Hit )
			{
				DebugOverlay.ScreenText(new(70f, 120f + iteration * 18f), $"Trace[{iteration}] hit dist={trace.Distance:F2} solid={trace.StartedSolid} normal={trace.Normal} validGround={IsValidGround(trace)}", 14f, TextFlag.LeftBottom, Color.Orange, DebugOverlayTraceDuration);
			}
			result.Normals.Add( trace.Normal );

			if ( trace.StartedSolid )
			{
				result.Collisions.Add( trace );
				break;
			}

			var safeDistance = MathF.Max( trace.Distance - Margin, 0f );
			result.EndPosition += trace.Direction * safeDistance;
			result.DistanceTraveled += safeDistance;

			if ( !trace.Hit ) { break; }

			planes.Add( trace.Normal );
			result.Collisions.Add( trace );

			var realNormal = GetRealNormal( trace );
			var traceWithRealNormal = trace with { Normal = realNormal };

			if ( IsValidGround( trace ) )
			{
				if ( parameters.FlatGround && motion.Dot( WorldTransform.Down ) >= 0f ) { motion = Vector3.Zero; }
			}
			else
			{
				if ( parameters.FlatWalls && !IsValidGround( traceWithRealNormal ) )
				{
					var flatWallNormal = traceWithRealNormal.Normal.SubtractDirection( WorldTransform.Up ).Normal;
					if ( !flatWallNormal.IsNearZeroLength )
					{
						planes.Add( flatWallNormal );
						if ( IsOnGround )
						{
							var crease = flatWallNormal.Cross( WorldTransform.Up );
							motion = motion.ProjectOnNormal( crease );
						}
					}
				}
			}

			if ( parameters.StopAtGround && IsValidGround( traceWithRealNormal ) ) { break; }

			for ( var i = 0; i < planes.Count; i++ )
			{
				var plane = planes[i];
				if ( plane.Dot( motion ) < 0f )
				{
					motion = motion.SubtractDirection( plane );
				}
				else continue;

				for ( var j = i + 1; j < planes.Count; j++ )
				{
					var plane2 = planes[j];
					if ( plane.Dot( plane2 ) > 0.999f ) { continue; }
					if ( plane2.Dot( motion ) >= 0f ) { continue; }
					if ( plane.Dot( plane2 ) <= 0f ) { continue; }

					var crease = plane.Cross( plane2 );
					motion = motion.ProjectOnNormal( crease );
				}
			}
		}

		return result;
	}

	public SceneTraceResult Trace( in Vector3 from, in Vector3 to )
	{
		var noSelfTags = Tags.ToArray();
		Tags.Add( "self" );
		var result = Scene.PhysicsWorld.Trace.Capsule( Capsule, from, to ).WithCollisionRules( noSelfTags ).WithoutTag( "self" ).UseHitPosition().Rotated( WorldRotation ).Run();
		Tags.Remove( "self" );

		var trace = SceneTraceResult.From( Scene, result );
		if ( DebugOverlayTrace ) { DebugOverlay.Trace( trace, DebugOverlayTraceDuration ); }

		trace.Normal = trace.Normal.Normal;

		return trace;
	}

	public SceneTraceResult TraceRay( in Vector3 from, in Vector3 to )
	{
		var noSelfTags = Tags.ToArray();
		Tags.Add( "self" );
		var result = Scene.PhysicsWorld.Trace.Ray( from, to ).WithCollisionRules( noSelfTags ).WithoutTag( "self" ).UseHitPosition().Rotated( WorldRotation ).Run();
		Tags.Remove( "self" );

		var trace = SceneTraceResult.From( Scene, result );
		if ( DebugOverlayTrace ) { DebugOverlay.Trace( trace, DebugOverlayTraceDuration ); }

		trace.Normal = trace.Normal.Normal;

		return trace;
	}

	public SceneTraceResult TraceCustom( in Vector3 from, in Vector3 to, Action<PhysicsTraceBuilder> extra )
	{
		var noSelfTags = Tags.ToArray();
		Tags.Add( "self" );
		var builder = Scene.PhysicsWorld.Trace.Capsule( Capsule, from, to ).WithCollisionRules( noSelfTags ).WithoutTag( "self" ).UseHitPosition().Rotated( WorldRotation );
		extra( builder );
		var result = builder.Run();
		Tags.Remove( "self" );

		var trace = SceneTraceResult.From( Scene, result );
		if ( DebugOverlayTrace ) DebugOverlay.Trace( trace, DebugOverlayTraceDuration );

		trace.Normal = trace.Normal.Normal;

		return trace;
	}
}
