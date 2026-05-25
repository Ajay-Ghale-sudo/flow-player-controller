namespace Flow.States;

public partial class Runner : Look3d.IEvents
{
	protected override void OnUpdate()
	{

	}

	protected float LookSwaySprintingTime { get; set; } = 0f;
	protected float LookSwaySprintingSmoothed { get; set; } = 0f;
	protected float LookSwayJumpingSmoothed { get; set; } = 0f;
	protected float LookSwayLandingSmoothed { get; set; } = 0f;
	void Look3d.IEvents.ModifyLookSway( Look3d module, ref Angles sway )
	{
		float Sin( float x ) => MathF.Sin( x );
		float Cos( float x ) => MathF.Cos( x );
		Angles V( float x, float y, float z = 0f ) => new( -y, x, z );
		const float PI = MathF.PI;

		{
			const float BREATH_ANIMATION_FREQUENCY = 1f;
			const float BREATH_ANIMATION_SCALE = 1f;

			var t = Time.Now * BREATH_ANIMATION_FREQUENCY;
			t *= 1.5f;
			sway += V( Sin( t * 0.3f ) / 2f * 0.33f, -Cos( t ) / 2f ) * 0.4f * BREATH_ANIMATION_SCALE;
		}

		float AccelerateEasing( float x )
		{
			var xe = MathF.Pow( x, 0.6f );
			// return 0.5f * (Cos( 2f * PI * xe - PI ) + 1 + (Cos( PI * xe - PI ) + 1) * 0.5f);
			return (1f - MathF.Pow( 2f * xe - 1f, 2f )) * 1.2f + x * 0.5f;
		}

		// Sprint
		{
			const float SPRINT_ANIMATION_FREQUENCY = 1f;
			const float SPRINT_ANIMATION_SCALE = 1f;
			var mom = Controller.UseMomentum ? Controller.Momentum.Clamp( 0f, 2f ) : 0f;

			var target = (Controller.IsOnGround && IsCurrent) ? AccelerateEasing( SprintProgress ) : 0f;
			LookSwaySprintingSmoothed = LookSwaySprintingSmoothed.LerpTo( target, 10f * Time.Delta );
			var frac = LookSwaySprintingSmoothed;
			var freq = (15f + frac * 10f) * (1f + mom * MomentumSwayFrequencyScale);
			var amp = 1f + mom * MomentumSwayAmplitudeScale;
			LookSwaySprintingTime += Time.Delta * freq * SPRINT_ANIMATION_FREQUENCY;
			LookSwaySprintingTime %= 4f * MathF.PI;
			var t = LookSwaySprintingTime;
			var vertRaw = -Cos( t );
			var vert = vertRaw < 0f ? vertRaw * (1f + mom * MomentumSwayDownstrokeBias) : vertRaw; // downstroke amped, upstroke normal
			var v = V( Sin( t / 2f + MathF.PI / 4f ) * 0.4f * amp, -Cos( t ) * 0.2f * amp );
			sway += v * LookSwaySprintingSmoothed * SPRINT_ANIMATION_SCALE;
		}

		float ScaryElasticishEasing( float t )
		{
			var xe = 1f - MathF.Pow( 1f - MathF.Pow( t, 0.5f ), 0.9f );
			var frac = (Cos( PI + xe * PI / 2f * 5f ) + Cos( xe * PI ) / 2f + 0.5f) * (1f - xe);
			return frac;
		}

		// Jumping
		{
			const float JUMP_ANIMATION_LENGTH = 1f;
			const float JUMP_ANIMATION_SCALE = 1f;

			var t = (JumpTimer / JUMP_ANIMATION_LENGTH).Clamp( 0f, 1f );
			var frac = ScaryElasticishEasing( t );
			LookSwayJumpingSmoothed = LookSwayJumpingSmoothed.LerpTo( frac, 50f * Time.Delta );
			var v = V( 0.4f * Sin( Time.Now * 0.1f ), -3f, 0.66f );
			sway += v * LookSwayJumpingSmoothed * JUMP_ANIMATION_SCALE;
		}

		// Landing
		{
			const float LAND_ANIMATION_LENGTH = 1f;
			const float LAND_ANIMATION_SCALE = 1f;

			var t = (LandTime / LAND_ANIMATION_LENGTH).Clamp( 0f, 1f );
			var frac = ScaryElasticishEasing( t );
			// 165 low-jump, 300 high-jump
			// high -> 2pitch, 0.5roll
			// low -> 1pitch, 0.25roll
			var strength = MathF.Pow( LandSpeed.Clamp( 0f, 2000f ) / 150f, 1.5f ) * frac;
			LookSwayLandingSmoothed = LookSwayLandingSmoothed.LerpTo( strength, 25f * Time.Delta );
			var side = Sin( Time.Now * 0.33f );
			side = MathF.CopySign( MathF.Pow( MathF.Abs( side ), 0.2f ), side );
			var v = V( 0f, -1f, 0.25f * side );
			sway += v * LookSwayLandingSmoothed * LAND_ANIMATION_SCALE;
		}
	}
}
