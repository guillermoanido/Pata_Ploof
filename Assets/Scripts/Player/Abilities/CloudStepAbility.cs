using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Cloud Step", fileName = "Cloud Step")]
    public class CloudStepAbility : Ability
    {
        const float ShortestCloud = 0.5f;

        [Header("Cloud")]
        [Tooltip("Spawned under the wizard's feet, and required - with this empty the spell will " +
                 "not cast. The Cloud Step prefab, or your own: its root is the TOP of the cloud, " +
                 "where the feet go, and its collider has to be on a layer the wizard's ground " +
                 "check looks at, such as Ground, or they fall straight through it.")]
        public GameObject cloudPrefab;

        [Tooltip("Seconds the cloud lasts once the wizard steps or falls off it. It also melts " +
                 "when Active Duration runs out, whether or not anyone is standing on it.")]
        [Min(0f)] public float secondsAfterLeaving = 0.25f;

        public override bool CanCast(PlayerLogic wizard) =>
            cloudPrefab != null &&
            wizard.State == PlayerState.Normal &&
            !wizard.movement.IsGrounded;

        public override string WhyNot(PlayerLogic wizard)
        {
            if (cloudPrefab == null)
                return "this spell has no Cloud Prefab - drag the Cloud Step prefab into its " +
                       "Cloud Prefab slot";

            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State}";

            return wizard.movement.IsGrounded ? "you are already standing on something" : null;
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            Puff puff = wizard.spellbook.StateOf<Puff>(this);

            RemoveCloud(puff);

            puff.cloud = Instantiate(cloudPrefab, wizard.movement.Footing, Quaternion.identity);
            puff.cloud.name = displayName;

            wizard.movement.RiseAtLeast(0f);
            return true;
        }

        public override void OnLit(PlayerLogic wizard, float fixedDeltaTime)
        {
            Puff puff = wizard.spellbook.StateOf<Puff>(this);

            if (IsStandingOn(wizard, puff.cloud))
            {
                puff.hasBeenStoodOn = true;
                puff.offFor = 0f;
                return;
            }

            if (!puff.hasBeenStoodOn)
                return;

            puff.offFor += fixedDeltaTime;

            if (puff.offFor >= secondsAfterLeaving)
                wizard.spellbook.Extinguish(this);
        }

        public override void OnEnded(PlayerLogic wizard) =>
            RemoveCloud(wizard.spellbook.StateOf<Puff>(this));

        protected override void Validate() => activeDuration = Mathf.Max(ShortestCloud, activeDuration);

        protected override void CleanUp(PlayerLogic wizard) =>
            RemoveCloud(wizard.spellbook.StateOf<Puff>(this));

        static bool IsStandingOn(PlayerLogic wizard, GameObject cloud)
        {
            Collider2D floor = wizard.movement.StandingOn;

            return cloud != null && floor != null && floor.transform.IsChildOf(cloud.transform);
        }

        static void RemoveCloud(Puff puff)
        {
            if (puff.cloud != null)
                Destroy(puff.cloud);

            puff.cloud = null;
            puff.hasBeenStoodOn = false;
            puff.offFor = 0f;
        }

        class Puff
        {
            public GameObject cloud;
            public bool hasBeenStoodOn;
            public float offFor;
        }
    }
}
