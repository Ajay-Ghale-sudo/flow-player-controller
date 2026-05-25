namespace Flow.States;

public struct MomentumProfile
{
	[Property, Range( 0f, 500f ), Step( 10f )]	public float BuildRate;
	[Property, Range( 0f, 10f ), Step( 0.25f )] public float DecayRate;
	[Property, Range( 0f, 10f ), Step( 0.25f )] public float CrashMultiplier;
	[Property, Range( 0f, 500f ), Step( 10f )] public float SlopeGain;
	[Property, Range( 0f, 200f ), Step( 5f )] public float SlopeDecayPenalty;

	public static MomentumProfile Default => new()
	{
		BuildRate = 30f,
		DecayRate = 1.75f,
		CrashMultiplier = 3f,
		SlopeGain = 120f,
		SlopeDecayPenalty = 70f
	};

	// Flat-ground equilibrium offset above sprintspeed. Just for debug.
	public float EquilibriumOffset => DecayRate > 0f ? BuildRate / DecayRate : 0f;
}
