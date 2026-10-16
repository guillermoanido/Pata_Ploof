using FallingWizard.Core;
using FallingWizard.Localization;
using FallingWizard.Player;
using FallingWizard.UI;
using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Collider2D))]
    public class RestSite : PlayerTrigger
    {
        const float SamePlace = 0.01f;

        const float MarkerRadius = 0.25f;

        static readonly Color MarkerColour = new Color(0.98f, 0.86f, 0.42f);

        [Header("Rest")]
        [Tooltip("Where the wizard reappears after a fall, relative to this object. Lift it clear " +
                 "of the floor so they do not come back inside it.")]
        public Vector2 respawnOffset = new Vector2(0f, 0.5f);

        [Tooltip("What the screen is headed with. This is the English. It is only used when the " +
                 "key below is empty, or when no translation has that key yet.")]
        public string title = "A place to rest";

        [Tooltip("Which translated line the heading uses. Leave the standard key here for an " +
                 "ordinary rest site. CLEAR IT for a one-off rest site with words written for " +
                 "this spot, and the English typed above is shown exactly as typed, in every " +
                 "language - which is what you want for something nobody is going to translate.")]
        public string titleKey = Loc.Keys.RestTitle;

        [Tooltip("The line under the heading. Say where this leads. The English, as above.")]
        public string blurb = "Further down, or back the way you came.";

        [Tooltip("Which translated line the blurb uses. Clear it to use the English above verbatim.")]
        public string blurbKey = Loc.Keys.RestBlurb;

        [Tooltip("Optional prefab spawned the first time this is reached.")]
        public GameObject reachedEffect;

        [Header("Look")]
        [Tooltip("A child object with your lit art, switched on only while this is the rest " +
                 "site the wizard will come back to, and off the rest of the time. Your own art " +
                 "on this object is never touched. Never put the rest site itself here, or it " +
                 "switches itself off. Leave it empty and nothing changes when it is reached.")]
        public GameObject litLook;

        public Vector2 RespawnPoint => (Vector2)transform.position + respawnOffset;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        void Awake() => ShowLitLook(IsLive);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = MarkerColour;
            Gizmos.DrawWireSphere(RespawnPoint, MarkerRadius);
        }

        protected override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (Screens.ModalOpen)
                return;

            bool first = !IsLive;

            Progress.MarkCheckpoint(RespawnPoint);

            foreach (RestSite other in FindObjectsByType<RestSite>(FindObjectsSortMode.None))
                other.ShowLitLook(other == this);

            if (first && reachedEffect != null)
                Instantiate(reachedEffect, transform.position, Quaternion.identity);

            Offer(wizard);
        }

        void Offer(PlayerCharacter wizard)
        {
            ChoiceScreen screen = ChoiceScreen.Open(Loc.Text(titleKey, title),
                                                    Loc.Text(blurbKey, blurb));

            screen.Status(Loc.Format(Loc.Keys.RestStatus, Progress.CarriedWisps, Progress.Wisps));

            screen.Choice(Loc.Get(Loc.Keys.RestPressOn), () =>
            {
                wizard.Logic.RestoreHealth();
                wizard.Logic.spellbook.ResetForRun();
                screen.Close();
            });

            screen.Choice(Loc.Format(Loc.Keys.RestTurnBack, Progress.CarriedWisps), () =>
                screen.CloseThen(() =>
                {
                    Progress.EndRun();
                    SkillScreen.Open(Game.LoadFirstLevel);
                }));
        }

        bool IsLive =>
            Progress.CheckpointIsHere &&
            (Progress.CheckpointPoint - RespawnPoint).sqrMagnitude < SamePlace * SamePlace;

        void ShowLitLook(bool lit)
        {
            if (litLook != null)
                litLook.SetActive(lit);
        }
    }
}
