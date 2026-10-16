#if UNITY_EDITOR
using System.Collections.Generic;
using FallingWizard.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FallingWizard.EditorTools
{
    public static class BuildPlayerAnimator
    {
        const string ClipFolder = "Assets/Animations";
        const string ControllerPath = ClipFolder + "/Player.controller";

        const int FramesPerSecond = 12;
        const int DefaultRagdollVariants = 4;

        class Clip
        {
            public string State;
            public bool Loops;

            public string Path => $"{ClipFolder}/{State.Replace(" ", string.Empty)}.anim";
        }

        static readonly Clip[] Clips =
        {
            new Clip { State = "Idle",        Loops = true },
            new Clip { State = "Walk",        Loops = true },
            new Clip { State = "Run",         Loops = true },
            new Clip { State = "Falling",     Loops = true },
            new Clip { State = "Climb Up",    Loops = false },
            new Clip { State = "Climb Down",  Loops = false },
            new Clip { State = "Climb",       Loops = true },
            new Clip { State = "Take Damage", Loops = false },
            new Clip { State = "Hat Change",  Loops = false },
            new Clip { State = "Death",       Loops = false },
        };

        const string RagdollClipPrefix = "Ragdoll";

        [MenuItem("Falling Wizard/Build Player Animator")]
        public static void Build()
        {
            int variants = ReadVariantCount();

            EnsureFolder();

            var motions = new Dictionary<string, AnimationClip>();

            foreach (Clip clip in Clips)
                motions[clip.State] = LoadOrCreateClip(clip);

            for (int i = 0; i < variants; i++)
            {
                var ragdoll = new Clip { State = $"{RagdollClipPrefix} {i}", Loops = false };
                motions[ragdoll.State] = LoadOrCreateClip(ragdoll);
            }

            BuildController(motions, variants);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Player Animator built: {motions.Count} clips and a controller at " +
                      $"{ControllerPath}, with {variants} ragdoll reaction(s). Clips that already " +
                      "existed were left exactly as they were; new ones are empty. Give each " +
                      "health look's Animator this controller, or an override of it.");
        }

        static int ReadVariantCount()
        {
            PlayerAnimator existing = Object.FindFirstObjectByType<PlayerAnimator>(FindObjectsInactive.Include);

            return existing != null ? Mathf.Max(1, existing.ragdollVariants) : DefaultRagdollVariants;
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(ClipFolder))
                AssetDatabase.CreateFolder("Assets", "Animations");
        }

        static AnimationClip LoadOrCreateClip(Clip spec)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(spec.Path);

            if (existing != null)
                return existing;

            var clip = new AnimationClip { frameRate = FramesPerSecond };

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = spec.Loops;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, spec.Path);
            return clip;
        }

        static AnimatorController BuildController(Dictionary<string, AnimationClip> motions, int variants)
        {
            AnimatorController controller = LoadEmptyController();

            controller.AddParameter("Mode", AnimatorControllerParameterType.Int);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("ClimbRate", AnimatorControllerParameterType.Float);
            controller.AddParameter("ClimbUp", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("ClimbDown", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("RagdollVariant", AnimatorControllerParameterType.Int);
            controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HatChanged", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine graph = controller.layers[0].stateMachine;

            AnimatorState idle = AddState(graph, motions, "Idle", new Vector3(300, 0));
            AnimatorState walk = AddState(graph, motions, "Walk", new Vector3(560, -80), "Speed");
            AnimatorState run = AddState(graph, motions, "Run", new Vector3(560, 80), "Speed");
            AnimatorState falling = AddState(graph, motions, "Falling", new Vector3(300, 180));
            AnimatorState climbUp = AddState(graph, motions, "Climb Up", new Vector3(-40, -140));
            AnimatorState climbDown = AddState(graph, motions, "Climb Down", new Vector3(-40, -60));
            AnimatorState climb = AddState(graph, motions, "Climb", new Vector3(180, -100), "ClimbRate");
            AnimatorState hurt = AddState(graph, motions, "Take Damage", new Vector3(300, 300));
            AnimatorState death = AddState(graph, motions, "Death", new Vector3(300, 380));
            AnimatorState hatChange = AddState(graph, motions, "Hat Change", new Vector3(300, 460));

            graph.defaultState = idle;

            Go(idle, walk, ("Moving", true), ("Walking", true));
            Go(idle, run, ("Moving", true), ("Walking", false));
            Go(walk, run, ("Walking", false));
            Go(run, walk, ("Walking", true));
            Go(walk, idle, ("Moving", false));
            Go(run, idle, ("Moving", false));

            Go(idle, falling, ("Grounded", false));
            Go(walk, falling, ("Grounded", false));
            Go(run, falling, ("Grounded", false));

            GoOnLanding(falling, idle, motions, ("Moving", false));
            GoOnLanding(falling, walk, motions, ("Moving", true), ("Walking", true));
            GoOnLanding(falling, run, motions, ("Moving", true), ("Walking", false));

            OnFinishing(climbUp, climb);
            OnFinishing(climbDown, climb);

            AnimatorStateTransition offThePole = climb.AddTransition(idle);
            offThePole.hasExitTime = false;
            offThePole.duration = 0f;
            offThePole.AddCondition(AnimatorConditionMode.NotEqual, PlayerAnimator.ModeStaff, "Mode");

            OnFinishing(hurt, idle);
            OnFinishing(hatChange, idle);

            Interrupt(graph, death, t =>
                t.AddCondition(AnimatorConditionMode.Equals, PlayerAnimator.ModeDead, "Mode"));

            for (int i = 0; i < variants; i++)
            {
                AnimatorState pose = AddState(graph, motions, $"{RagdollClipPrefix} {i}",
                    new Vector3(760, -100 + i * 70));

                int variant = i;
                Interrupt(graph, pose, t =>
                {
                    t.AddCondition(AnimatorConditionMode.Equals, PlayerAnimator.ModeRagdoll, "Mode");
                    t.AddCondition(AnimatorConditionMode.Equals, variant, "RagdollVariant");
                });

                AnimatorStateTransition backUp = pose.AddTransition(idle);
                backUp.hasExitTime = false;
                backUp.duration = 0f;
                backUp.AddCondition(AnimatorConditionMode.Equals, PlayerAnimator.ModeGround, "Mode");
            }

            Interrupt(graph, climbUp, t => t.AddCondition(AnimatorConditionMode.If, 0f, "ClimbUp"));
            Interrupt(graph, climbDown, t => t.AddCondition(AnimatorConditionMode.If, 0f, "ClimbDown"));

            Interrupt(graph, hurt, t =>
            {
                t.AddCondition(AnimatorConditionMode.If, 0f, "Hurt");
                t.AddCondition(AnimatorConditionMode.Equals, PlayerAnimator.ModeGround, "Mode");
            });

            Interrupt(graph, hatChange, t =>
            {
                t.AddCondition(AnimatorConditionMode.If, 0f, "HatChanged");
                t.AddCondition(AnimatorConditionMode.Equals, PlayerAnimator.ModeGround, "Mode");
            });

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorController LoadEmptyController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null)
                return AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            foreach (AnimatorControllerParameter parameter in controller.parameters)
                controller.RemoveParameter(parameter);

            AnimatorStateMachine graph = controller.layers[0].stateMachine;

            foreach (AnimatorStateTransition transition in graph.anyStateTransitions)
                graph.RemoveAnyStateTransition(transition);

            foreach (ChildAnimatorState child in graph.states)
                graph.RemoveState(child.state);

            return controller;
        }

        static AnimatorState AddState(AnimatorStateMachine graph,
            Dictionary<string, AnimationClip> motions, string name, Vector3 at, string speedParameter = null)
        {
            AnimatorState state = graph.AddState(name, at);

            state.motion = motions.TryGetValue(name, out AnimationClip clip) ? clip : null;

            state.writeDefaultValues = false;

            if (!string.IsNullOrEmpty(speedParameter))
            {
                state.speedParameterActive = true;
                state.speedParameter = speedParameter;
            }

            return state;
        }

        static void Go(AnimatorState from, AnimatorState to, params (string Parameter, bool Wanted)[] conditions)
        {
            AnimatorStateTransition transition = from.AddTransition(to);

            transition.hasExitTime = false;
            transition.duration = 0f;

            foreach ((string parameter, bool wanted) in conditions)
                transition.AddCondition(wanted ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                    0f, parameter);
        }

        static void GoOnLanding(AnimatorState from, AnimatorState to,
            Dictionary<string, AnimationClip> motions, params (string Parameter, bool Wanted)[] conditions)
        {
            AnimatorStateTransition transition = from.AddTransition(to);

            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            transition.AddCondition(AnimatorConditionMode.Equals, PlayerAnimator.ModeGround, "Mode");

            foreach ((string parameter, bool wanted) in conditions)
                transition.AddCondition(wanted ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                    0f, parameter);
        }

        static void OnFinishing(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);

            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0f;
        }

        static void Interrupt(AnimatorStateMachine graph, AnimatorState to,
            System.Action<AnimatorStateTransition> conditions)
        {
            AnimatorStateTransition transition = graph.AddAnyStateTransition(to);

            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;

            conditions(transition);
        }
    }
}
#endif
