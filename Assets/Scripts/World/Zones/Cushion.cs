using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class Cushion : Zone
    {
        [Header("Cushion")]
        [Tooltip("Only a wizard dropping in faster than this, in boxes per second, puffs the dust. " +
                 "Walking onto it or standing on it stays quiet; a tumble it stops always puffs.")]
        [Min(0f)] public float puffSpeed = 2f;

        [Header("Look")]
        [Tooltip("A child object switched on for Dust Seconds each time the cushion catches the " +
                 "wizard, then off again. Its animation or particles start over each time, so a " +
                 "dust puff that plays once fits here. It starts as a Star Sparkles copy; swap in " +
                 "your own dust. Leave it empty for none.")]
        public GameObject dustLook;

        [Tooltip("How long the Dust Look shows after each catch, in seconds.")]
        [Min(0f)] public float dustSeconds = 0.6f;

        protected override void Reset()
        {
            base.Reset();
            affectsRagdolled = true;
        }

        void Awake() => ShowLook(dustLook, false);

        protected override void OnPlayerEntered(PlayerCharacter wizard) => Catch(wizard, true);

        protected override void OnPlayerInside(PlayerCharacter wizard, float fixedDeltaTime) =>
            Catch(wizard, false);

        void Catch(PlayerCharacter wizard, bool isArriving)
        {
            if (!CanAffect(wizard))
                return;

            PlayerLogic logic = wizard.Logic;
            bool isDroppingIn = isArriving && -logic.movement.Velocity.y > puffSpeed;

            logic.RestartFall();

            if (logic.TryEndTumble() || isDroppingIn)
                PuffDust();
        }

        void PuffDust()
        {
            if (dustLook == null)
                return;

            CancelInvoke(nameof(SettleDust));
            dustLook.SetActive(false);
            dustLook.SetActive(true);
            Invoke(nameof(SettleDust), dustSeconds);
        }

        void SettleDust() => ShowLook(dustLook, false);
    }
}
