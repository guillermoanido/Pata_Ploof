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
        const string ParentFolder = "Assets";
        const string ClipFolderName = "Animations";
        const string ClipFolder = ParentFolder + "/" + ClipFolderName;
        const string ControllerPath = ClipFolder + "/Player.controller";

        const int FramesPerSecond = 12;
        const int DefaultRagdollVariants = 4;

        const string RagdollStatePrefix = "Ragdoll";

        static readonly Clip[] Clips =
        {
            new Clip { State = "Idle",        IsLooping = true },
            new Clip { State = "Walk",        IsLooping = true },
            new Clip { State = "Run",         IsLooping = true },
            new Clip { State = "Falling",     IsLooping = true },
            new Clip { State = "Climb Up",    IsLooping = false },
            new Clip { State = "Climb Down",  IsLooping = false },
            new Clip { State = "Climb",       IsLooping = true },
            new Clip { State = "Take Damage", IsLooping = false },
            new Clip { State = "Hat Change",  IsLooping = false },
            new Clip { State = "Death",       IsLooping = false },
        };

        static readonly BoolCondition IsGrounded = new BoolCondition("Grounded", true);
        static readonly BoolCondition IsNotGrounded = new BoolCondition("Grounded", false);
        static readonly BoolCondition IsMoving = new BoolCondition("Moving", true);
        static readonly BoolCondition IsNotMoving = new BoolCondition("Moving", false);
        static readonly BoolCondition IsWalking = new BoolCondition("Walking", true);
        static readonly BoolCondition IsNotWalking = new BoolCondition("Walking", false);

        static readonly Vector3 IdlePosition = new Vector3(300f, 0f);
        static readonly Vector3 WalkPosition = new Vector3(560f, -80f);
        static readonly Vector3 RunPosition = new Vector3(560f, 80f);
        static readonly Vector3 FallingPosition = new Vector3(300f, 180f);
        static readonly Vector3 ClimbUpPosition = new Vector3(-40f, -140f);
        static readonly Vector3 ClimbDownPosition = new Vector3(-40f, -60f);
        static readonly Vector3 ClimbPosition = new Vector3(180f, -100f);
        static readonly Vector3 HurtPosition = new Vector3(300f, 300f);
        static readonly Vector3 DeathPosition = new Vector3(300f, 380f);
        static readonly Vector3 HatChangePosition = new Vector3(300f, 460f);
        static readonly Vector3 FirstRagdollPosition = new Vector3(760f, -100f);
        static readonly Vector3 RagdollSpacing = new Vector3(0f, 70f);

        [MenuItem("Falling Wizard/Build Player Animator")]
        static void Build()
        {
            int variants = ReadVariantCount();

            EnsureFolder();

            Dictionary<string, AnimationClip> motions = LoadOrCreateClips(variants);

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
                AssetDatabase.CreateFolder(ParentFolder, ClipFolderName);
        }

        static Dictionary<string, AnimationClip> LoadOrCreateClips(int variants)
        {
            var motions = new Dictionary<string, AnimationClip>();

            foreach (Clip clip in Clips)
                motions[clip.State] = LoadOrCreateClip(clip);

            for (int i = 0; i < variants; i++)
            {
                var ragdoll = new Clip { State = RagdollStateName(i), IsLooping = false };
                motions[ragdoll.State] = LoadOrCreateClip(ragdoll);
            }

            return motions;
        }

        static AnimationClip LoadOrCreateClip(Clip clip)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clip.Path);

            if (existing != null)
                return existing;

            var created = new AnimationClip { frameRate = FramesPerSecond };

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(created);
            settings.loopTime = clip.IsLooping;
            AnimationUtility.SetAnimationClipSettings(created, settings);

            AssetDatabase.CreateAsset(created, clip.Path);
            return created;
        }

        static string RagdollStateName(int variant) => $"{RagdollStatePrefix} {variant}";

        static void BuildController(Dictionary<string, AnimationClip> motions, int variants)
        {
            AnimatorController controller = LoadEmptyController();
            AddParameters(controller);

            AnimatorStateMachine graph = controller.layers[0].stateMachine;
            MainStates states = AddMainStates(graph, motions);

            AddWalkingTransitions(states);
            AddFallingTransitions(states);
            AddClimbingTransitions(states);
            AddReactionEndings(states);
            AddDeathInterrupt(graph, states.Death);
            AddRagdollStates(graph, motions, variants, states.Idle);
            AddTriggerInterrupts(graph, states);

            EditorUtility.SetDirty(controller);
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

        static void AddParameters(AnimatorController controller)
        {
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
        }

        static MainStates AddMainStates(AnimatorStateMachine graph, Dictionary<string, AnimationClip> motions)
        {
            var states = new MainStates
            {
                Idle = AddState(graph, motions, "Idle", IdlePosition),
                Walk = AddState(graph, motions, "Walk", WalkPosition, "Speed"),
                Run = AddState(graph, motions, "Run", RunPosition, "Speed"),
                Falling = AddState(graph, motions, "Falling", FallingPosition),
                ClimbUp = AddState(graph, motions, "Climb Up", ClimbUpPosition),
                ClimbDown = AddState(graph, motions, "Climb Down", ClimbDownPosition),
                Climb = AddState(graph, motions, "Climb", ClimbPosition, "ClimbRate"),
                Hurt = AddState(graph, motions, "Take Damage", HurtPosition),
                Death = AddState(graph, motions, "Death", DeathPosition),
                HatChange = AddState(graph, motions, "Hat Change", HatChangePosition),
            };

            graph.defaultState = states.Idle;
            return states;
        }

        static void AddWalkingTransitions(MainStates states)
        {
            Go(states.Idle, states.Walk, IsMoving, IsWalking);
            Go(states.Idle, states.Run, IsMoving, IsNotWalking);
            Go(states.Walk, states.Run, IsNotWalking);
            Go(states.Run, states.Walk, IsWalking);
            Go(states.Walk, states.Idle, IsNotMoving);
            Go(states.Run, states.Idle, IsNotMoving);
        }

        static void AddFallingTransitions(MainStates states)
        {
            Go(states.Idle, states.Falling, IsNotGrounded);
            Go(states.Walk, states.Falling, IsNotGrounded);
            Go(states.Run, states.Falling, IsNotGrounded);

            GoOnLanding(states.Falling, states.Idle, IsNotMoving);
            GoOnLanding(states.Falling, states.Walk, IsMoving, IsWalking);
            GoOnLanding(states.Falling, states.Run, IsMoving, IsNotWalking);
        }

        static void AddClimbingTransitions(MainStates states)
        {
            GoOnFinishing(states.ClimbUp, states.Climb);
            GoOnFinishing(states.ClimbDown, states.Climb);

            AnimatorStateTransition offThePole = Go(states.Climb, states.Idle);
            offThePole.AddCondition(AnimatorConditionMode.NotEqual, PlayerAnimator.ModeStaff, "Mode");
        }

        static void AddReactionEndings(MainStates states)
        {
            GoOnFinishing(states.Hurt, states.Idle);
            GoOnFinishing(states.HatChange, states.Idle);
        }

        static void AddDeathInterrupt(AnimatorStateMachine graph, AnimatorState death)
        {
            AnimatorStateTransition dying = Interrupt(graph, death);
            RequireMode(dying, PlayerAnimator.ModeDead);
        }

        static void AddRagdollStates(AnimatorStateMachine graph, Dictionary<string, AnimationClip> motions,
            int variants, AnimatorState idle)
        {
            for (int i = 0; i < variants; i++)
            {
                Vector3 position = FirstRagdollPosition + RagdollSpacing * i;
                AnimatorState pose = AddState(graph, motions, RagdollStateName(i), position);

                AnimatorStateTransition tumble = Interrupt(graph, pose);
                RequireMode(tumble, PlayerAnimator.ModeRagdoll);
                tumble.AddCondition(AnimatorConditionMode.Equals, i, "RagdollVariant");

                AnimatorStateTransition backUp = Go(pose, idle);
                RequireMode(backUp, PlayerAnimator.ModeGround);
            }
        }

        static void AddTriggerInterrupts(AnimatorStateMachine graph, MainStates states)
        {
            InterruptOnTrigger(graph, states.ClimbUp, "ClimbUp");
            InterruptOnTrigger(graph, states.ClimbDown, "ClimbDown");

            AnimatorStateTransition hurt = InterruptOnTrigger(graph, states.Hurt, "Hurt");
            RequireMode(hurt, PlayerAnimator.ModeGround);

            AnimatorStateTransition hatChange = InterruptOnTrigger(graph, states.HatChange, "HatChanged");
            RequireMode(hatChange, PlayerAnimator.ModeGround);
        }

        static AnimatorState AddState(AnimatorStateMachine graph,
            Dictionary<string, AnimationClip> motions, string name, Vector3 position, string speedParameter = null)
        {
            AnimatorState state = graph.AddState(name, position);

            state.motion = motions.TryGetValue(name, out AnimationClip motion) ? motion : null;

            state.writeDefaultValues = false;

            if (!string.IsNullOrEmpty(speedParameter))
            {
                state.speedParameterActive = true;
                state.speedParameter = speedParameter;
            }

            return state;
        }

        static AnimatorStateTransition Go(AnimatorState from, AnimatorState to, params BoolCondition[] conditions)
        {
            AnimatorStateTransition transition = from.AddTransition(to);

            MakeInstant(transition);
            AddConditions(transition, conditions);

            return transition;
        }

        static void GoOnLanding(AnimatorState from, AnimatorState to, params BoolCondition[] conditions)
        {
            AnimatorStateTransition transition = Go(from, to, IsGrounded);

            RequireMode(transition, PlayerAnimator.ModeGround);
            AddConditions(transition, conditions);
        }

        static void GoOnFinishing(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);

            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0f;
        }

        static AnimatorStateTransition Interrupt(AnimatorStateMachine graph, AnimatorState to)
        {
            AnimatorStateTransition transition = graph.AddAnyStateTransition(to);

            MakeInstant(transition);
            transition.canTransitionToSelf = false;

            return transition;
        }

        static AnimatorStateTransition InterruptOnTrigger(AnimatorStateMachine graph, AnimatorState to,
            string trigger)
        {
            AnimatorStateTransition transition = Interrupt(graph, to);
            transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            return transition;
        }

        static void RequireMode(AnimatorStateTransition transition, int mode) =>
            transition.AddCondition(AnimatorConditionMode.Equals, mode, "Mode");

        static void AddConditions(AnimatorStateTransition transition, BoolCondition[] conditions)
        {
            foreach (BoolCondition condition in conditions)
                transition.AddCondition(condition.Mode, 0f, condition.Parameter);
        }

        static void MakeInstant(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.duration = 0f;
        }

        class Clip
        {
            public string State;
            public bool IsLooping;

            public string Path => $"{ClipFolder}/{State.Replace(" ", string.Empty)}.anim";
        }

        class BoolCondition
        {
            public string Parameter { get; }
            public AnimatorConditionMode Mode { get; }

            public BoolCondition(string parameter, bool isWanted)
            {
                Parameter = parameter;
                Mode = isWanted ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
            }
        }

        class MainStates
        {
            public AnimatorState Idle;
            public AnimatorState Walk;
            public AnimatorState Run;
            public AnimatorState Falling;
            public AnimatorState ClimbUp;
            public AnimatorState ClimbDown;
            public AnimatorState Climb;
            public AnimatorState Hurt;
            public AnimatorState Death;
            public AnimatorState HatChange;
        }
    }
}
#endif
