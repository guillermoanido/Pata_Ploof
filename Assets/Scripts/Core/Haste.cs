using UnityEngine;

namespace FallingWizard.Core
{
    public static class Haste
    {
        public const float SlowestWorldScale = 0.05f;

        public static float WorldScale { get; private set; } = 1f;

        public static float DeltaTime => Time.deltaTime * WorldScale;

        public static float FixedDeltaTime => Time.fixedDeltaTime * WorldScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => End();

        public static void Begin(float scale) =>
            WorldScale = Mathf.Clamp(scale, SlowestWorldScale, 1f);

        public static void End() => WorldScale = 1f;
    }
}
