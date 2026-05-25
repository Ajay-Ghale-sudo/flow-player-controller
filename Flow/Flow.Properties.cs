namespace Flow.States;

public partial class Runner
{
    /// <summary>
    /// Hard ceiling on horizontal velocity. Acts as a safety net against runaway
    /// velocity from physics glitches, chained injections, or long downhill runs.
    /// Not intended to be reached during normal play.
    /// </summary>
    [Property]
    public float MaxSpeed { get; set; } = 5000f;

    // ─── Ground ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Force (u/s²) applied when the player is under their target speed and
    /// pressing a movement key. Higher = snappier response off the line.
    /// Used as the base value; actual acceleration is modulated by
    /// <see cref="CalculateGroundAcceleration"/> depending on current speed
    /// and slope.
    /// </summary>
    [Property, Group( "Ground" ), Order( 200 )]
    public float Acceleration { get; set; } = 1250;

    /// <summary>
    /// Baseline ground deceleration (u/s²) when the player is at or below
    /// <see cref="WalkSpeed"/>. At higher speeds this lerps toward
    /// <see cref="HighSpeedDeceleration"/> to produce a "skidding" feel.
    /// Higher = more abrupt stops.
    /// </summary>
    [Property, Group( "Ground" ), Order( 200 )]
    public float Deceleration { get; set; } = 750f;

    /// <summary>
    /// Target speed (u/s) when the player is walking (not sprinting). Also
    /// serves as the threshold below which deceleration is fully "snappy".
    /// </summary>
    [Property, Group( "Ground" ), Order( 200 )]
    public float WalkSpeed { get; set; } = 200;

    // ─── Sprint ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Target speed (u/s) during a sustained sprint on flat ground with no
    /// earned momentum. This is the "baseline" the momentum system decays
    /// back toward, and the anchor for all momentum math.
    /// </summary>
    [Property, Group( "Sprint" ), Order( 300 )]
    public float SprintSpeed { get; set; } = 300f;

    /// <summary>
    /// Time in seconds to accelerate from <see cref="WalkSpeed"/> to
    /// <see cref="SprintSpeed"/> on flat ground. Used to derive the
    /// high-speed acceleration rate in <see cref="CalculateGroundAcceleration"/>.
    /// </summary>
    [Property, Group( "Sprint" ), Order( 300 )]
    public float SprintTime { get; set; } = 0.87f;

    /// <summary>
    /// If true, the player sprints by default and must hold a key to walk.
    /// If false, the player walks by default and sprint is toggled/held.
    /// </summary>
    [Property, Group( "Sprint" ), Order( 300 )]
    public bool SprintByDefault { get; set; } = true;

    // ─── Crouch ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Target horizontal speed (u/s) while crouched.
    /// </summary>
    [Property, Group( "Sprint" ), Order( 400 )]
    public float CrouchSpeed { get; set; } = 92f;

    /// <summary>
    /// Time in seconds for the crouch transition (hitbox shrink / stand-up
    /// animation timing). Lower = snappier crouch, higher = more commitment.
    /// </summary>
    [Property, Group( "Sprint" ), Order( 400 )]
    public float CrouchTime { get; set; } = 0.25f;

    /// <summary>
    /// Fraction [0..1] of the crouch height offset applied to the camera/hitbox
    /// while airborne and crouched. 0 = no change mid-air, 1 = full crouch
    /// offset applied. Lets you bias crouch-jump geometry independently from
    /// ground crouch.
    /// </summary>
    [Property, Group( "Sprint" ), Order( 400 ), Range( 0f, 1f )]
    public float CrouchedAerialOffset { get; set; } = 0.17f;

    // ─── Jump ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Desired jump apex height (units) at full strength, used to derive the
    /// initial jump velocity via <c>v = sqrt(2 * g * h)</c>. Actual height
    /// is modulated by fatigue (see <see cref="JumpFatigueHeightFrac"/>).
    /// </summary>
    [Property, Group( "Jump" ), Order( 500 )]
    public float JumpHeight { get; set; } = 55f;

    /// <summary>
    /// Jumps landed within this window (seconds) since the previous jump are
    /// fully fatigued (reduced height). Below this, no fatigue is applied.
    /// </summary>
    [Property, Group( "Jump" ), Order( 500 )]
    public float MinJumpFatigueTime { get; set; } = 0.15f;

    /// <summary>
    /// Jumps landed after this long (seconds) since the previous jump are
    /// fully recovered (full height). Between min and max, height is lerped.
    /// </summary>
    [Property, Group( "Jump" ), Order( 500 )]
    public float MaxJumpFatigueTime { get; set; } = 0.75f;

    /// <summary>
    /// Fraction [0..1] of normal jump height remaining when fully fatigued.
    /// e.g. 0.3 = a rapidly-repeated jump is only 30% as tall.
    /// </summary>
    [Property, Group( "Jump" ), Order( 500 )]
    public float JumpFatigueHeightFrac { get; set; } = 0.3f;

    /// <summary>
    /// Multiplier on the player's upward velocity component (from running up a
    /// slope) that gets added to the jump. 0 = slope ignored, 1 = full slope
    /// velocity transferred into the jump. Makes jumping off ramps feel more
    /// powerful without requiring dedicated ramp logic.
    /// </summary>
    [Property, Group( "Jump" ), Order( 500 )]
    public float JumpUpSlopeBoost { get; set; } = 0.75f;

    /// <summary>
    /// How long (seconds) a jump input remains queued if pressed before the
    /// player is grounded. Landing within this window consumes the buffered
    /// input and jumps immediately.
    /// </summary>
    [Property, Group( "Jump" ), Order( 500 )]
    public float JumpBufferTime { get; set; } = 0.2f;

    /// <summary>
    /// How long (seconds) after walking off a ledge the player can still jump
    /// as if they were grounded. Standard "coyote time" forgiveness window.
    /// </summary>
    [Property, Group( "Jump" ), Order( 500 )]
    public float CoyoteTime { get; set; } = 0.2f;

    /// <summary>
    /// If true, holding jump while grounded will continuously hop (bhop style).
    /// If false, jump must be re-pressed each time.
    /// </summary>
    [Property, Group( "Jump" ), Order( 500 )]
    public bool AutoBhop { get; set; } = false;

    /// <summary>Direction of mousewheel scroll — used for scroll-to-jump binding.</summary>
    public enum ScrollDirection { Up, Down }

    /// <summary>
    /// Optional mousewheel direction that triggers a jump, in addition to the
    /// regular jump bind. null disables scroll-jump entirely. Commonly used in
    /// movement shooters for tighter bhop timing.
    /// </summary>
    [Property, Group( "Jump" ), Order( 500 )]
    public ScrollDirection? ScrollJump { get; set; } = ScrollDirection.Up;

    // ─── Aerial ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Acceleration (u/s²) applied from player input while airborne. This is
    /// the "air strafe" responsiveness — higher = more mid-air control.
    /// </summary>
    [Property, Group( "Aerial" ), Order( 600 )]
    public float AirAcceleration { get; set; } = 500f;

    /// <summary>
    /// Maximum speed (u/s) the air-accelerate function is allowed to *add to*
    /// along the wish direction. Does NOT cap total air speed — players can
    /// carry in any velocity they want. This only limits how much new speed
    /// can be gained *while airborne* along a given direction, which is what
    /// enables classic Quake/Source-style air control without runaway speed.
    /// </summary>
    [Property, Group( "Aerial" ), Order( 600 )]
    public float AirLimit { get; set; } = 70f;

    // ─── Landing ────────────────────────────────────────────────────────────

    /// <summary>
    /// Downward landing speed (u/s) at which fall-stun begins to apply (0% stun).
    /// Landings softer than this produce no horizontal velocity loss.
    /// </summary>
    [Property, Group( "Landing" ), Order( 600 )]
    public float MinFallStunSpeed { get; set; } = 400f;

    /// <summary>
    /// Downward landing speed (u/s) at which fall-stun is fully applied (100% stun).
    /// Between min and max, stun lerps linearly.
    /// </summary>
    [Property, Group( "Landing" ), Order( 600 )]
    public float MaxFallStunSpeed { get; set; } = 900f;

    /// <summary>
    /// Strength [0..1] of the horizontal velocity reduction at full stun. 1 = 
    /// completely halted on a hard landing, 0 = no horizontal loss at all.
    /// Scales the stun curve without moving the min/max thresholds.
    /// </summary>
    [Property, Group( "Landing" ), Order( 600 ), Range( 0f, 1f ), Step( 0.05f )]
    public float FallStunStrength { get; set; } = 1f;

    // ─── Momentum ───────────────────────────────────────────────────────────

    /// <summary>
    /// Scale reference (u/s) used to normalize the computed <c>Momentum</c>
    /// property for other systems (camera lean, HUD, SFX). <c>Momentum = 1.0</c>
    /// corresponds to this many units over <c>SprintSpeed</c>. Does NOT affect
    /// the cap itself — purely a display/scaling constant.
    /// </summary>
    [Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
    public float MomentumReference { get; set; } = 150f;

    /// <summary>
    /// Multiplier that scales ground acceleration based on slope. On flat
    /// ground this has no effect. On uphills acceleration is reduced (up to
    /// multiplied by <c>1 - this</c>), on downhills it's increased. 0 = slopes
    /// don't affect acceleration at all. Distinct from slope decay — this is
    /// about input responsiveness, not momentum retention.
    /// </summary>
    [Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
    public float MomentumSlopeFactor { get; set; } = 4f;

    /// <summary>
    /// Maximum camera roll (degrees) applied from horizontal acceleration at
    /// high momentum. Produces the "leaning into turns" feel. 0 disables.
    /// </summary>
    [Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
    public float MomentumCameraLean { get; set; } = 25f;

    /// <summary>
    /// Smoothing factor [0..1] for momentum-driven camera lean. Higher = more
    /// lag / more gradual lean, lower = snappier and more twitchy. Roughly the
    /// "weight" of the camera against acceleration changes.
    /// </summary>
    [Property, Group( "Momentum" ), Order( 700 )]
    public float MomentumCameraDrag { get; set; } = 0.35f;

    /// <summary>
    /// Target soft-ceiling offset (u/s) above <see cref="SprintSpeed"/> that
    /// flat-ground sprint converges toward. Combined with <see cref="MomentumBuildRate"/>
    /// and <see cref="MomentumDecayBase"/>, the equilibrium is approximately
    /// <c>MomentumBuildRate / MomentumDecayBase</c>, so setting this field
    /// alone won't change the actual ceiling — it's documentation of intent.
    /// If you change build rate or decay base, update this to match the new
    /// equilibrium for clarity.
    /// </summary>
    /// <remarks>
    /// Currently informational only. If you want a hard clamp at this value,
    /// re-enable <c>cap = MathF.Min(cap, SprintSpeed + MomentumBuildCeiling)</c>
    /// in <c>ThinkMomentum</c>.
    /// </remarks>
    [Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
    public float MomentumBuildCeiling { get; set; } = 50f;

    /// <summary>
    /// Rate (u/s²) at which MomentumCap grows while sustaining a sprint on
    /// flat or gentle terrain with forward input. Provides constant upward
    /// pressure that the decay fights against — the balance between this and
    /// <see cref="MomentumDecayBase"/> determines the flat-ground ceiling.
    /// </summary>
    [Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
    public float MomentumBuildRate { get; set; } = 30f;

	/// <summary>
	/// How much momentum increases the sprint bob frequency. 0 = no effect,
	/// 0.35 = +35% footstep rate at Momentum = 1.
	/// </summary>
	[Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
	public float MomentumSwayFrequencyScale { get; set; } = 0.35f;

	/// <summary>
	/// How much momentum increases the sprint bob amplitude. 0 = no effect,
	/// 0.6 = +60% bob size at Momentum = 1.
	/// </summary>
	[Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
	public float MomentumSwayAmplitudeScale { get; set; } = 0.6f;

	/// <summary>
	/// How much momentum amplifies the downstroke of the sprint bob relative
	/// to the upstroke, producing a "heavy stride" feel at high speeds.
	/// 0 = symmetric, 0.5 = 50% heavier downstroke at Momentum = 1.
	/// </summary>
	[Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
	public float MomentumSwayDownstrokeBias { get; set; } = 0.5f;

	/// <summary>
	/// Maximum FOV increase (degrees) added when horizontal speed reaches
	/// SprintSpeed + MomentumReference. Scaled linearly below that.
	/// </summary>
	[Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
	public float MomentumFovBoost { get; set; } = 12f;

	/// <summary>
	/// Rate at which FOV ramps UP toward the target when gaining speed.
	/// Lower = feels heavier / earned. Higher = snappy / arcadey.
	/// </summary>
	[Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
	public float MomentumFovRampIn { get; set; } = 3.5f;

	/// <summary>
	/// Rate at which FOV ramps DOWN toward the target when losing speed.
	/// Higher = sharp feedback on deceleration, impacts, etc.
	/// </summary>
	[Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
	public float MomentumFovRampOut { get; set; } = 8f;

	/// <summary>
	/// Extra FOV added while wallrunning (on top of momentum-driven boost).
	/// Signals the state visually even at sustained speed.
	/// </summary>
	[Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
	public float WallrunFovBoost { get; set; } = 4f;

	/// <summary>
	/// Extra FOV added while sliding. Should be larger than wallrun since
	/// slides typically feel more intense.
	/// </summary>
	[Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
	public float SlideFovBoost { get; set; } = 6f;

	/// <summary>
	/// Horizontal speed (u/s) over SprintSpeed at which FOV boost reaches maximum.
	/// Should be set HIGHER than typical flat-sprint peak so chained actions
	/// (walljumps, slides, downhill) have visible headroom above normal play.
	/// e.g. if flat peak is ~390 (90 excess) and you want chains to feel bigger,
	/// set this to 150–180.
	/// </summary>
	[Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
	public float MomentumFovSpan { get; set; } = 180f;

	/// <summary>
	/// Power curve applied to the FOV ramp. 1.0 = linear. Values &gt;1 make the
	/// low end feel subtle and the high end explosive (good for rewarding chains).
	/// Values &lt;1 do the opposite (more sensitive at low speeds).
	/// </summary>
	[Property, Group( "Momentum" ), Order( 700 ), Feature( "Momentum" )]
	public float MomentumFovCurve { get; set; } = 1.5f;
	
    /// <summary>
    /// Deceleration rate (u/s²) applied at or above <see cref="SprintSpeed"/>.
    /// Lerped from <see cref="Deceleration"/> to this value based on current
    /// speed. Lower than Deceleration to produce a "skidding" feel when
    /// slowing from high speeds — fast responsiveness at walk, momentum-heavy
    /// skids when sprinting.
    /// </summary>
    [Property, Group( "Ground" ), Order( 200 )]
    public float HighSpeedDeceleration { get; set; } = 150f;

	/// <summary>
	/// Multiplier [0..1] applied to deceleration when the player gives no
	/// movement input. Makes "releasing the stick" produce a longer skid than
	/// "actively stopping". 1 = no difference, 0.5 = half decel → double skid
	/// time, 0 = no deceleration at all on release.
	/// </summary>
	[Property, Group( "Ground" ), Order( 200 )]
	public float NoInputDecelerationScale { get; set; } = 0.5f;
	
}
