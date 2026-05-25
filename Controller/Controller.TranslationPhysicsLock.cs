namespace Flow;

public partial class Controller
{
	public struct TranslationPhysicsLock
	{
		public bool X{ get; set; }
		public bool Y { get; set; }
		public bool Z { get; set; }

		public TranslationPhysicsLock()
		{
			X = false;
			Y = false;
			Z = false;
		}

		public static implicit operator PhysicsLock( TranslationPhysicsLock translationPhysicsLock ) => new()
		{
			X = translationPhysicsLock.X,
			Y = translationPhysicsLock.Y,
			Z = translationPhysicsLock.Z,
			Pitch = true,
			Yaw = true,
			Roll = true
		};
	}
}
