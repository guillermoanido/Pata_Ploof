# Falling Wizard — scripts

Unity 6.3, URP 2D, new Input System only. Everything reads the project-wide actions in
`Assets/InputSystem_Actions`, already bound to keyboard and gamepad, so PC and consoles work from
the same code with no branches.

## The unit everything is measured in

**One box = 32 px = 1 world unit ≈ one mage.** The art is drawn on a 32 px grid and imported at
32 pixels per unit, so a box is the same thing on the canvas, in the inspector and in the physics.

Every number in the game is expressed in boxes or boxes per second:

| | |
| --- | --- |
| Run / walk | 4 and 2 boxes per second |
| Step up | one tile (0.5) walked up on foot, no staff needed |
| Staff climb | walls of two tiles and up, to 2 boxes - there is no jump |
| Free fall | 3 boxes |
| Fall damage | 1 heart per box past that — so 8 boxes kills a full-health wizard |
| Staff reach | 2 boxes above the feet, authored on the staff - the pole is stretched to match |

The climb being under the damage floor is deliberate: going up something the staff can reach and
coming back down it can never hurt you.

## Layout

| Folder | What lives there |
| --- | --- |
| `Core/` | `Game` (pause, scene flow, quit), `GameSettings`, `Controls` (every input lookup, plus which device is in use), `Progress` (the save: Magical Scrap, spells, loadout, hearts, finished levels, taken pickups), `SaveFile`, `SingletonBehaviour` |
| `Player/` | `PlayerCharacter` (the Unity side), `PlayerLogic` (everything the wizard does), `Staff`, `PlayerAnimator`, `HealthLook`, `RagdollBumps` |
| `Player/Abilities/` | `Ability` and the spells, `AbilityBook` |
| `Localization/` | `Loc`, `TextBook`, `LocalizedText` |
| `World/` | `Item` (the base of everything the wizard touches) and `TileGrid`, with subfolders: `Hazards/` (`Hazard`, `TripHazard`, `BouncyTrash`, `SteamVent`, `WaxDrop`, `WaxCandle`, `PressurePlate`), `Zones/` (`Zone`, `WindZone2D`, `WindTrap`, `SlipperyFloor`), `Pickups/` (`Pickup`), `Level/` (`ShopStop`, `LevelExit`, `MovingPlatform`, `VineAnchor`, `Carryable`), `Camera/` (`FollowCamera`) |
| `UI/` | `PlayerHud`, `HudSlot`, `FlingArrow`, `Ui` and the runtime screens |
| `Menus/`, `Cutscenes/` | `MenuScreen` and the three menus; `CutsceneRunner` |

### The wizard is two scripts

- **`PlayerCharacter.cs`** is the Unity side: the component on the PLAYER prefab that reads the
  buttons, receives Unity's messages and hands everything to the logic.
- **`PlayerLogic.cs`** is everything the wizard *does*, in one file:
  - first the wizard's own fields and the state machine
  - then, in order, its parts as nested classes: `Movement` (with `ClimbRefusal` and
    `ArcSettings`), `Ragdoll`, `Health`, `Vine` (with `Hold`), `Spellbook` (with `Slot`),
    `Modifiers`, and the input structs `Intent` and `Command`

The parts are **nested classes** because they are pieces of a wizard and mean nothing on their own.
The rest of the game reaches them as `PlayerLogic.Movement`, `PlayerLogic.Spellbook` and so on.
`Staff.cs` likewise holds `Staff.Pole`. Nested `[Serializable]` classes serialize exactly like
top-level ones and show up as foldouts in the Inspector.

The file is long (Movement alone is over a thousand lines). Use your editor's outline, or search for
`public class Movement`, to jump to a part.

### Rules that keep it working

- **Never rename `PlayerCharacter.cs`, `Staff.cs` or `FollowCamera.cs`**, or their
  classes. `Level 1.unity` refers to them by GUID, and the GUID lives in the `.meta` beside the
  file. Moving a file is fine — the `.meta` travels with it. Renaming is not.
- **Never use `[SerializeReference]`.** It writes assembly, namespace and class names into the
  scene; renaming a class silently nulls every reference to it.
- **`OnValidate` does not reach nested classes.** Each block has a `Validate()` instead, chained
  from `PlayerCharacter.OnValidate` and `Staff.OnValidate`.
- **Public runtime state needs `[NonSerialized]`.** Inside a `[Serializable]` class a public field
  serializes by default, so a public timer would be baked into the scene at whatever value it held
  when you last saved.

## How the scripts are written

Every script follows the same six rules, so any one of them reads like the rest:

1. **No comments.** Names explain the code. A `[Tooltip]` explains an Inspector field to whoever is
   tuning it.
2. **Small, simple methods.** One job each, about 20 lines at most, early `return`s instead of deep
   nesting. Plain loops, not LINQ; `if (x != null)`, not `?.`, on anything Unity owns.
3. **Objects own their data.** Anything another script does not need is `private`, and shared
   behaviour lives in a base class: `Item`, `Zone`, `Hazard`, `Ability`, `MenuScreen`.
4. **The same order in every class:**
   - constants
   - Inspector fields
   - private state
   - events
   - properties
   - Unity messages (`Reset`, `OnValidate`, `Awake`, `OnEnable`, `Start`, `FixedUpdate`, `Update`…)
   - public methods
   - protected methods
   - private methods, in the order they are first called
   - gizmos
   - nested types
5. **Names say what things are and do.** No abbreviations.
   - Methods start with a verb: `ShowTheRightLook`.
   - Yes/no values read as questions: `IsGrounded`, `HasPole`, `WasPausePressed`, `CanAffect`.
   - A method that may not manage it, and tells you whether it did, starts with `Try`: `TryTrip`,
     `TryBuy`.
   - A method named for what it gives back (`RankOf`, `CentreOf`) only looks something up.
6. **No magic numbers.** Every number in code except 0 and 1 is a named `const`, or a
   `static readonly` for colours and vectors, at the top of its class: `Half`, `BothSides`,
   `NoFixedSlot`. Layer masks come from `Layers`. An Inspector field's default (`runSpeed = 6f`) is
   already named by its field.

## Numbers

Three kinds of field, and which one a thing is decides how it is declared:

| Kind | Declared as | Examples |
| --- | --- | --- |
| **Tuning** — has a unit | `public` under a `[Header]`, with `[Tooltip]` and `[Min]`/`[Range]` | `runSpeed`, `stepHeight`, `bounceHeight` |
| **Wiring** — points at something | `public` under a `[Header]` | `hitbox`, `visual`, `bridgeCollider`, `book`, `ability` |
| **Runtime** — has a lifetime, not a value | `{ get; private set; }` or `[NonSerialized]` private | `IsGrounded`, `Facing`, `Current`, `Progress` |

Every tunable is public so it is visible and editable in the inspector. That is a deliberate
trade: nothing stops another script writing `wizard.Logic.movement.runSpeed = 99`, so **don't**.
If you find yourself wanting to, you wanted a `Modifiers` multiplier or one of the verbs below.

## How the world talks to the wizard

Hazards and spells never reach in and set a number. They call verbs on `PlayerLogic`, each of which
decides for itself whether it applies:

```csharp
wizard.TryTrip();                                // rocks
wizard.TryBounce(heightInBoxes, sideways, resetsFall);  // Bouncy Trash
wizard.TryFling(velocity);                       // the Fling spell, straight into a tumble
wizard.Push(boxesPerSecond, rampup, groundScale);    // wind, every step you are inside it
wizard.Shove(velocity, controlLockout);          // one-off knock
wizard.Hurt(hearts);  wizard.Heal(hearts);
wizard.TryPlantStaff(mode);  wizard.RecoverStaff();  wizard.DropFromStaff();
```

`Movement.Run` writes `linearVelocityX` absolutely every physics step, so an `AddForce` from
outside is wiped within a quarter second. That is why external force has exactly one way in: wind
is folded into `Run`'s target speed — so you can lean into it and partly win, and it self-limits —
and impulses land immediately with a short steering lockout. **Impulses must not be queued for the
next `FixedTick`:** whatever shoves you usually trips you too, and `FixedTick` never runs while
tumbling, so a queued shove would sit unspent and then fire as you stood back up.

## Spells

A spell is a `ScriptableObject` asset. They are **stateless flyweights** — every wizard shares the
one asset, so all the mutable state lives in `PlayerLogic.Spellbook.Slot`. Adding a mutable field
to an `Ability` would leak between play sessions and into the build.

**A spell does not own a button. A slot does.** There are four of them — Q, E, R, F, bound by
index to the `Player/Spell1..Spell4` actions — and what a press does depends entirely on what is
sitting in that slot. A spell you own but did not bring does nothing at all. This is the point of
the whole design: you cannot carry everything, so what you bring is a decision you make before you
go down.

The Staff is the exception, and only because it is the one spell every route assumes: `locked: 1`
and `fixedSlot: 0` pin it to the first slot, **Q**, for good, and the skill screen will not let it
out. That leaves **E, R and F** to fight over.

A spell with `passive: 1` has no button. It still takes a slot, so bringing one costs you an
active — otherwise a passive is free and there is no choice in it.

| Hook | When |
| --- | --- |
| `ModifyStats` | Every step, while OWNED. Where a passive lives. Multiply, never assign. |
| `ModifyStatsWhileLit` | Every step, only during the seconds after a cast. |
| `CanCast` | Whether the button would do anything. Greys the HUD slot. |
| `OnCast` | Do it. **Return false for "not yet"** — the press stays buffered and retries, which is what lets you press the staff button just before reaching a ledge. |
| `OnLit` / `OnEnded` | During and at the end of the lit window. |
| `OnEquipped` / `OnUnequipped` | Put into a slot; taken out of one. |
| `OnRunReset` | The wizard died. Refills `usesPerLevel`. |
| `CleanUp` | Called by both `OnUnequipped` and `OnRunReset`. Undo anything the spell left in the level or on the wizard here, once, instead of in both hooks. |

**Shared pieces live in `Ability`.** `PickTier` and `ValidateTiers` handle the per-rank numbers.
`WizardEffect` finds an effect object under the wizard by name, once, and switches it on and off:
Glide's `Glide Canopy` and Haste's `Haste Trail` both use it.

**Every spell keeps its own script, on purpose.** Unity only loads a spell asset whose script file
has the same name as its class. The seven spells also really do behave differently, so one shared
"spells" class would need a switch over every spell, plus Inspector fields that each mean nothing
to most spells.

`usesPerLevel` above 0 gives a spell a number of casts that only the next level brings back — for
something a cooldown alone cannot hold back. `Slot.UsesLeft` counts them and the HUD prints it.

A spell that needs to remember something between frames — a wall it grew, a rope it is holding —
keeps it in `spellbook.StateOf<T>(this)`, never in a field on itself. The asset is shared; a field
on it would survive a scene load and follow you into the build.

### When a press does nothing

Every gate a spell can fail — wrong state, cooling down, out of casts, nothing in reach — used to
fail **silently**, which makes a spell that will not fire almost impossible to chase: there is no
way to tell an empty slot from a missed ledge from a cooldown.

So `Ability.WhyNot(PlayerLogic)` says, in the player's words, why the press just now came to
nothing, and `Spellbook` prints it the moment a buffered press runs out of patience without ever
going off. Not earlier than that — until the buffer expires the press is still legitimately waiting
for a ledge to arrive, which is the whole reason the buffer exists.

It is editor-only, it covers the empty-slot case too (*"there is nothing in that slot"*), and
`Spellbook.explainRefusals` turns it off. Cooldown and per-level charges are answered generically by
`Spellbook` itself, so `WhyNot` only has to speak for the spell's own conditions.

**Sorting order is the trap it exists to catch.** Both the vine and the wall shipped at −1 and were
therefore behind the tilemap: solid, working perfectly, and completely invisible — which reads
exactly like a spell doing nothing. Anything a spell puts into the world wants an order above the
map.

The Staff is yours from the start and welded to E; everything else fights over three buttons.

| Spell | What it is | Rank 2 / 3 |
| --- | --- | --- |
| **Staff** | Plant it at a ledge and climb down, or raise it against a wall and climb up. | longer staff |
| **Mage Hand** | A spectral hand you swing from. | wider swing / climb it |
| **Wall Growth** | One tile of stone off the edge of the floor you stand on, so the floor gets longer. | a second block |
| **Glide** | A canopy. It barely slows the drop - it carries you. | wider wing |
| **Telekinesis** | Take a hazard with you and set it down where you want it. | longer reach |
| **Haste** | You quicken; the world wades. Once per level. | deeper haste |
| **Fling** | Hold to aim up or down (never more than 45° up), let go to be thrown into a tumble. | harder throw |

Two of them want level content and already have it: Mage Hand needs a `Vine`, Telekinesis needs a
`Carryable` — which the `Bouncy Trash` and `Wet Floor Sign` prefabs now carry, so every Bouncy
Trash and sign already in a level can be picked up and put down somewhere better.

**Glide does not forgive fall damage, and that is the design.** Feather Fall — the spell that
used to live in this slot — did, and that forgiveness was the only reason it was worth a button.
Glide earns its slot a different way: it roughly **doubles how far a run off a ledge carries you**
before you land. Giving one spell both would make it two spells over three contested buttons.

Note what `fallSpeed` does *not* buy. Fall damage counts **boxes fallen**, not how fast
(`SenseGround` bills `highestPoint - position.y`), so dropping the canopy's fall speed to
0.8 lowers the terminal speed and bills you exactly the same. That is worth letting a player
discover once. You survive under a canopy by **going somewhere else**, not by falling softer.

`AirSpeedMultiplier`, `AirControlMultiplier` and `AirDragMultiplier` are air-only for a reason:
`MoveSpeedMultiplier` would make the wizard sprint along the floor with a wing out. They are
applied after the move multiply and **before** `targetSpeed += wind.x`, so a canopy carries you
under your own steam without also amplifying a gale.

**Steering and coasting are separate multipliers, and they must be.** `Run` picks `acceleration`
when the stick is held and `groundFriction` when it is not; one multiplier over both means a wing
that bites harder when steered *also brakes harder when released* — so letting go dropped the
wizard straight down, which is the exact opposite of gliding. `AirControlMultiplier` scales the
first, `AirDragMultiplier` the second, and Glide pushes them in opposite directions: 1.6 and 0.45.

**`foldsOnLanding` needs the spell's own memory of having flown.** An early version read a
lifetime air-time counter that only ever counted up, so it guarded exactly one cast — the first of
a session. After the first fall it was true forever, and throwing the canopy out while stood at a
ledge folded it on the next physics step, put it on cooldown, and left the wizard falling at
completely normal speed with nothing to say why. The `Wing.hasFlown` flag is set the first step the
wizard is off the ground and cleared on every cast. (The counter is gone now; nothing else read it.)

**Telekinesis is one button doing two jobs**, chosen by whether your hands are full. Empty, the
press takes hold of the nearest `Carryable` and stows it; full, the press sets it down in the first
tile ahead that will have it. There is no lit window and nothing to time — `activeDuration` is 0
and the only thing standing between the two presses is a short `cooldown`, so the spell cannot
grab and drop on the same press.

### Adding spell #5

1. One `.cs` in `Player/Abilities/`, deriving from `Ability`, named the same as its class.
2. One asset via **Assets ▸ Create ▸ Falling Wizard ▸ Abilities ▸ …**, with a `cost` in Magical Scrap.
3. Drag it into `Assets/Resources/Spellbook.asset` → `spells`.

**No input edits, no scene edits, no HUD edits.** The buttons already exist and belong to the
slots; the skill screen lists whatever is in the catalogue. Press buffering, cooldown, per-level
charges, buying, equipping and persistence all come for free.

A spell that wants to drive the wizard itself takes over a `PlayerState` — see the vine below,
which is the worked example.

### Ranks

A spell's rank lives in `Progress.ranks` — `Dictionary<string,int>`, on disk until the wizard dies, and
it round-tripped values above 1 from the day it was written, so there was no save-format work to
do. Rank 1 is "learned"; `TryBuy` and `Grant` hand that out and `Progress.TryUpgrade` deliberately
**refuses to learn**, so no bug in a screen can hand rank 2 to a spell nobody bought.

The split matters: **`Ability.upgrades[]` is the shop** — a cost, a title and a sentence per rank,
read by the skill screen without it knowing what a `MageHandAbility` is. **The numbers a rank
actually changes live on the spell's own script**, as a `Tier[]` grouped by rank rather than by
stat, because "what does rank 2 give me" is the question a designer asks. There is no separate
`maxRank` field: `MaxRank => upgrades.Length + 1`, because two numbers that can disagree is a bug
waiting to be authored.

A spell reads its rank off the **slot**, not out of `Progress` — `ModifyStats` runs for every slot
every fixed step. `Reload` caches it, and it writes `slot.Rank` **above** its own early-out: below
that line, buying an upgrade would not take effect until the next level loaded, and nothing anywhere
would say why.

A spell overrides `protected override void Validate()` and **never writes its own `OnValidate`** —
Unity delivers `OnValidate` to the most-derived type only, so declaring one would hide the base's
and silently kill every clamp on the chain.

### Order and unlocking

`Assets/Resources/Spellbook.asset` is the single source of truth. `spells` is the skill screen's
list, top to bottom — **drag to reorder, nothing in any scene depends on it**. `known` is granted
free on a new game; the Staff belongs there and nothing else has to.

Everything else is **bought with Magical Scrap** at the skill screen.

`Core.Progress` holds all of it, outside the wizard, because every scene load builds a brand new
wizard.

## Magical Scrap, hearts and dying

There is one purse and one save, and dying empties both:

| What | Where it lives | Kept while alive | After a death |
| --- | --- | --- | --- |
| Magical Scrap | `Progress.Scrap` | yes, on disk | gone |
| Spells and their ranks | `Progress.ranks` | yes, on disk | gone (the `known` spells come back free) |
| The loadout | `Progress.equipped` | yes, on disk | gone |
| Bonus hearts | `Progress.BonusHearts` | yes, on disk | gone |
| Finished levels | `Progress.completedLevels` | yes, on disk | gone |
| Taken pickups | `Progress.taken` | yes, on disk | gone, so every pickup is back |
| Settings | `GameSettings` | yes, on disk | **kept** |

**Magical Scrap goes straight into the purse** the moment a pickup is touched, and is spent at the
skill screen. There is no carrying and no banking: what you picked up is yours, and it is saved
straight away, so quitting halfway down and coming back carries on with everything you had.

**Dying starts the game again from nothing.** `PlayerCharacter.OnDied` calls `Progress.StartOver()`
the moment the last heart goes, waits `secondsBeforeDeathMessage` so the death can be seen, then
`ShowDeathMessage` opens a `ChoiceScreen` saying so (`death.title`, `death.blurb`) with two buttons:
**Start again** opens the skill screen and then Level 1, exactly like Play on the Main Menu, and
**Main menu** goes back there. `StartOver` is `ResetSave` — the save file is deleted and every list
above is emptied — except in a `Playtest` sandbox, where it empties the sandbox and leaves the real
save alone. The wipe happens at the moment of death rather than when a button is pressed, so
pausing, quitting or walking away from the message cannot dodge it.

### Pickups

There is **one `Pickup` script** for every collectable. Its **Gives** setting picks what it is, and
**Amount** says how much:

| Gives | What it does |
| --- | --- |
| Scrap | Adds **Amount** Magical Scrap to the purse. |
| Heart | Adds **Amount** hearts to the wizard's health and heals them. |

Either way the pickup's id goes into `Progress.taken` and `Pickup.Awake` destroys it on every later
load, so it stays taken until the wizard dies. Then the wipe empties `taken` and it is standing
there again.

To add a new kind of collectable, add a value to `Pickup.Gift` and a short `Give…` method for it.

The highest **Health Look** under the wizard is what a brand new save starts with (see Health
below) and `Health.maxBonusHearts` caps what can be added on top of it. A heart pickup that finds
the bar already at that cap pays out `scrapIfFull` Magical Scrap instead — never leave a player
standing over dead loot — and is taken all the same.

A `Pickup` remembers itself by **where it
stands** (quarter-box resolution, scene-qualified) unless you type an `id`. That survives renaming
and re-parenting; moving one makes it a new pickup. Give two pickups sharing a spot explicit ids.

The name is worked out once, in `Awake`, and kept. It has to be: the idle bob would otherwise drag
the position across a quarter-box boundary and quietly rename the pickup between being seen and
being touched. For the same reason **only the art bobs** — `FindArtToBob` refuses to bob a
`SpriteRenderer` sitting on the root, because that would walk the trigger around with it.

Drag in `Assets/Prefabs/Top Level/Basura Mágica` (Magical Scrap) and `Assets/Prefabs/Heart Upgrade`
and they work as they land: a trigger `CircleCollider2D`, the component, and an `Art` child to drop
a sprite onto. The code never changes that sprite — what you put there is what is drawn.

### Shop stops

A `ShopStop` is the place to spend Magical Scrap mid-level. Walk into one — alive, with no other
screen open — and it opens the skill screen with `SkillScreen.OpenInLevel`, which pauses the game.
Buy spells and upgrades, rearrange the loadout, and leave with the back button or Escape: the
screen's close callback reloads the spellbook so the wizard has exactly what is on the slots, and
unpauses, and the wizard carries on from where they stand. It does not save a position, heal, or
end anything.

`Item` reports one entry per visit, so standing in a shop stop does not reopen the screen. Walk
out and back in to open it again.

Drag in `Assets/Prefabs/Shop Stop.prefab`: a trigger box one box wide and one and a half tall,
with a `ShopStop` on it and a placeholder block on its `Art` child for you to swap for real art.

`ChoiceScreen` and `SkillScreen` build their own canvas at runtime out of `Ui`, which is the old
editor `UiFactory` with the editor-only parts taken out — including
`AssetDatabase.GetBuiltinExtraResource`, which does not exist in a player and silently produced
untinted white boxes when it was tried. They sit at sorting order 200 and 220, above the Pause
Menu's 100. Both set `Screens.IsModalOpen`, and `MenuScreen.Update` checks it, so the pause menu
underneath does not eat Escape.

**A panel that sizes itself must be built with `Ui.CreateSheet`, never `Ui.CreatePlate` plus a
`ContentSizeFitter`.** `Ui.SetSize` attaches a `LayoutElement`, and a `LayoutElement` outranks a
`VerticalLayoutGroup` when a `ContentSizeFitter` asks how tall the thing wants to be — so a panel
built that way with height 0 fits itself to **zero**. The group then has less room than its
children need and shrinks every one of them toward its minimum. The result is a screen you can read
and cannot use: the text still draws, but each button is 0 pixels tall and has no area to click.
`SetSize` now writes `minWidth`/`minHeight` as well as the preferred pair, so nothing built through
it can ever be squeezed to nothing again.

## Haste, and why it is not `Time.timeScale`

Haste is a **flag**, `Core.Haste`, that anything which moves under its own steam multiplies itself
by. Scaling time would have been fewer lines and wrong three ways: pausing already owns
`timeScale`; physics runs on a fixed step, so slowing it changes how the wizard's own collisions
resolve; and there would be no way to exempt anything.

As a flag, **the wizard is untouched by construction, and so is their ragdoll** — only things that
*ask* are slowed, and the ragdoll never asks. Wind is the first thing that asks
(`WindZone2D.OnPlayerInside` scales `push`). Anything that moves later reads `Haste.WorldScale`
the same way - the steam vents, wax drips, moving platforms and pressure plates all do.

`usesPerLevel` is the limit, and it means what it says: charges refill on the scene load that
rebuilds the spellbook, which is what a level transition is. Dying wipes everything and starts the
game again, so there is nothing there to police.

## Fling: aim, then tumble

**Holding the button aims; letting go throws.** There is no wind-up: how hard you are thrown is
set per rank (`Tier.speed`, 9 and then 11 boxes a second), and holding only turns the arrow.

- **Aiming.** The arrow starts at `startAngle` (20°), and holding up or down turns it at
  `aimSpeed` (90° a second) between `lowestAngle` (−45°) and `highestAngle` (45°). The sliders stop
  at 45° on purpose, so a fling is a throw across rather than a jump. Set Lowest Angle to 0 if you
  never want to be thrown downward. Left and right follow the way the wizard faces, so pushing the
  stick sideways while aiming turns them round.
- **The throw is a ragdoll.** `PlayerLogic.TryFling` stops the wizard, puts gravity back to normal,
  starts the fall-damage count at the launch height and hands the speed to `Ragdoll.Throw`. They
  tumble until they land and slow down, exactly like a trip, then get back up. A throw that ends
  more than `safeFallDistance` below its highest point still costs hearts.
- **The arrow warns before you commit.** `Movement.WouldTumbleThroughHazard` flies the same throw
  ahead of time, with the plain gravity a tumbling wizard falls with, and the arrow's Danger Look
  switches on when the throw would cross a hazard before it lands. Hazards do not stop the
  prediction: every hazard here is a trigger you pass straight through, so stopping at the first
  one would hide the rest of the flight.

**`IsRooted` is set from `ModifyStats`, never from `OnHeld`.** `TryCast` runs before `Rebuild`, and
`Rebuild` opens with `stats.Reset()` — anything written during the hold is wiped on the next line,
and the wizard walks away while aiming with nothing in the console to say why.

**The release is latched in `Observe` and consumed before the hook runs.** `Observe` is an Update
and `TryCast` is a FixedUpdate: polling `WasReleasedThisFrame` from a fixed-step hook misses the
edge on a slow frame and fires twice on a fast one.

A held spell wants **`pressBuffer: 0`**. Anything else and `WhyNot` complains to the console every
tenth of a second you spend aiming.

## Carrying a hazard

`Carryable` stows an object by **deactivating** it, not by destroying and re-spawning it. That
keeps every field the level author set, keeps its icon available to the HUD while it is stowed, and
means putting it down cannot lose anything.

`Hazard.Disarm` exists because **`Awake` does not run again when an object is switched back on** —
without it, a Bouncy Trash set down at your feet still has the re-arm timer it had when you picked it up,
and bounces you on the very next physics step.

**Placement settles; it does not test one row.** `TileGrid.TryFindRestingCell` takes a column and the
height the wizard aimed at, carries the thing up over anything in the way (`StepOver`, two boxes),
then walks it down onto the first floor beneath (`LookDown`, six). What comes back is where the
thing would come to rest — *on top of the tiles, as low as the column allows*.

Asking instead whether one fixed row was empty is what made the spell feel broken. Aimed at the
row the wizard's feet were in, that row is solid floor everywhere except at a ledge — so the spell
refused on flat ground, worked only at edges, and when it did work it dropped the rock into the
thin air past the lip. Settling has no such failure: the answer is right whether the row it
started from was or not.

`needsAFloor` only decides what happens to a column with **no** ground within `LookDown`. On, it
is refused. Off, the thing hangs at the height it was aimed at — which over a drop is a Bouncy
Trash the player placed exactly where they wanted it.

**Set down on the cell's FLOOR, not its middle.** A Bouncy Trash's hitbox is 0.7 of a box tall and a
rock's is 0.6, so `PutDown` on `CentreOf` left both floating a sixth of a box off the ground.
`PutDownOn` measures the thing's own footprint once it is switched back on — `Physics2D.SyncTransforms`
first, because a transform move does not reach the physics shapes until the next step and stale
bounds would settle it against wherever it used to be standing.

## The grid line is not the floor

`mainlev_build.png` is sliced **34×35 px on a 32 px grid**, so every tile is drawn — and collides
— about a pixel proud of its cell. Read it straight off the composite collider in `Level 1`: the
outline sits on `x.03125`, not on whole numbers.

That is not a rounding error to ignore. **It is the surface everything else has to line up with.**
A one-box block built to the bare grid tops out a pixel *below* the floor beside it, and a box
collider walking back onto the platform does not step up over a lip — it stops dead against it. A
wizard who walks out onto their own Wall Growth block and then cannot walk back is a spell that
reads as broken, with the cause nowhere near the spell.

The amount is a property of the **art**, not of the maths, and it is not even the same on both
axes. So nothing carries a constant for it — the two spells each ask the world:

| | takes its height from | why that is exact |
| --- | --- | --- |
| A carried Bouncy Trash, rock or boulder | `TileGrid.TryFindSurfaceUnder` — a cast straight down inside the cell, and `Footing.y` if it falls through | it is the tile's own top, whatever the slice did |
| Wall Growth's block | `Movement.Footing.y`, the wizard's soles | they are STOOD on the platform being extended, so their soles *are* its surface |

Re-slice the sheet to 32×32 one day and both keep working, having never been told what the old
number was.

**The cast carries `LookDown`, not one box.** Stopping at a single box meant a cell chosen even one
row high found nothing beneath it, fell back to its own grid line, and left the rock hanging there
— a grid line being the one number in this whole file that is never the floor. Reaching further
cannot pick the wrong surface: a downward cast reports the FIRST thing it meets.

**A footprint is measured in `Stow`, while the object is still switched on.** `Collider2D.bounds`
on a GameObject reactivated the same step answers with the shape the physics world still holds —
which is wherever the thing was standing when it was picked up. Setting something down against
that is how a rock ends up in mid-air, and no amount of `Physics2D.SyncTransforms` fixes it,
because the shape is not stale, it is *absent*.

**A block flush with the platform is the worst case, not the best.** Two box colliders whose top
faces are exactly level still meet at a vertical face, and gravity sinks the wizard about a fifth
of a pixel into the floor between solver steps — enough that the face catches them and they stop
dead at the block instead of walking onto it. The tilemap never shows this because the composite merges its tiles and
there are no internal faces at all.

So `Stone Wall`'s collider is **0.9 square with a `0.05` edge radius**: one box on the outside,
same flush top, but with rounded corners, so a wizard a fraction low rides up over the seam
instead of walking into it. Keep both numbers if you resize it — `size + 2 × edgeRadius` is what
has to come to one box.

Do **not** reach for a custom `physicsShape` on the tiles instead. `spriteMeshType` is Tight and
`physicsShape` is empty, so the slopes and half-tiles get outlines that follow their own art;
squaring all 212 of them off would wall the level in.

## Which row is the wizard standing in

Telekinesis settles with `TryFindRestingCell`, which survives the answer being a row out. Wall
Growth does **not** hunt for a floor any more: it takes the row straight under `StandingCell` as
the floor, on purpose. It used to search down `TryFindFloorRowUnder`, up to six rows, and a wizard
stood with their middle over a drop found the bottom of the pit instead — and then walked along
*that* floor and grew a block on the far side of the gap. Now it walks ahead along the floor the
wizard is on, stops dead at a wall, and only grows in the first empty tile past an edge it has
followed there, so it can only ever make that floor longer.

So `StandingCell` has to be right, and **it has to answer with the empty cell the body is in, never
the solid one holding it up.** One row out is not a near miss — it is the spell aiming into the
ground:

- Telekinesis asked whether the tile ahead was free, got told about the *floor*, and refused every
  cast with "every tile within 3 ahead of you is already filled".
- Wall Growth hunted for the lip of a drop along the row *below* the floor, found solid rock all
  the way out, and refused with "no lip within 3 tiles ahead of you".

Both read exactly like a spell that does nothing, and neither is.

It used to ask `Movement.FeetY + 0.05f` — the ground **probe**, lifted by what the default
`groundCheckSkin` happens to be. The probe hangs below the boots on purpose, and it drifts further
the moment the collider is resized without `FitGroundCheckTo` being run again, which is exactly
what had happened: the probe sat almost a tenth of a box under the wizard, and `floor()` landed a
row low. `Movement.Footing` asks the collider itself — `bounds.center.x` and `bounds.min.y` — and
`StandingCell` lifts that clear of the cell line before flooring it, because the soles rest exactly
*on* that line and `floor()` on a line is a coin toss decided by float error.

`FitGroundCheckTo` now measures from the collider's bottom and its own middle rather than from half
its height about the transform, so a collider carrying an offset — which is what Unity's
fit-to-sprite button writes — no longer bakes that drift in the moment someone hits Reset. The
PLAYER in `Level 1` has been refitted to its collider: `groundCheckOffset` went from
`(0, -0.596875)` to `(-0.050636888, -0.5525605)`, and `groundCheckSize.x` from `0.703125` to
`0.64969389`.

Both probes now hang from one `ProbeOrigin`. The ground check reads it, the ledge check reads it,
and `TryFindLedgeEdge` reports its lip relative to it — otherwise a collider with an x offset of
its own leaves the two disagreeing about which foot is over the drop.

**Wall Growth wants the wizard stood still on the floor, not just `PlayerState.Normal`.** `Normal`
is every state that is not staff, vine or ragdoll, and that includes falling — so the spell would
grow a block under a wizard in mid-air, one press at a time, all the way down. `IsGrounded` alone
is not enough either: the ground probe on the PLAYER is wider than the body, so a wizard sliding
down a wall face counts as grounded while they fall. `IsStandingFirmly` also refuses anyone
dropping faster than `FallingSpeed`, half a box a second.

**Rank 2 grows two blocks.** `WallGrowthAbility.tiers` holds how many blocks each rank may grow
before the spell rests. Each block crumbles `activeDuration` seconds after it grew; the spell stays
lit until the last one goes, and only then does the cooldown start and the count go back to zero.

## The vine

The worked example of a spell owning the wizard's movement.

`PlayerState.OnVine` is a fourth state beside `Normal`, `OnStaff` and `Ragdoll` — **appended, not
inserted**, because the enum serialises as an int in scene YAML.

**The body stays Dynamic and is steered by velocity.** This is the one thing not to change. Going
kinematic and teleporting with `MovePosition` — which is exactly what the staff does — swings the
wizard *straight through the level*, because a kinematic body is not stopped by static geometry.
The staff gets away with it by standing still against a ledge it has already checked; a swing
covers ground, and the ground has walls in it.

So each step works out where the swing wants the wizard, and asks for the velocity that gets them
there: `(wanted - position) / dt`. Physics then does what physics does, and a wall stops them. The
next step reads their **actual** position back and re-derives the angle and the length from it, so
the arc always agrees with where the wizard really is rather than grinding along the inside of a
wall insisting otherwise. Ending a step more than `BlockedDistance` boxes from where the last one asked
means they hit something, and the swing is killed. That check is against **the step's own target**,
not against the arc recomputed from their position — a rope pulling taut on the first step of a
grab is not the same thing as hitting a wall, and confusing the two throws away the run they
arrived with.

**It is a real pendulum**, which is one line of the fixed tick:

```
spin += (-(gravity / rope) * sin(angle) + lean.x * (swingPush / rope)) * dt
spin *= 1 - damping * dt
```

Gravity always pulls the wizard back under the knot and pulls harder the further out they are, so
**letting go of the stick settles them at the bottom on its own**. Left and right are a *push*, not
a speed: you pump a swing the way you would on a real one, and how much you get out depends on when
you push. `damping` is what stops it swinging forever.

Catching a vine keeps whatever you were already doing — the run you arrived with is projected onto
the arc — and letting go leaves at the speed you were genuinely travelling, `spin * depth`, capped
by `maxReleaseSpeed` so a long vine swung hard cannot fire the wizard across the level. The arc has
been showing the player that speed for the last second or two, which is what makes it aimable.

Hitting `maxSwing` only kills the swing if it is still trying to go *further* out. A swing that
reaches the limit already on its way back keeps its speed, or every big swing stalls at the top.

### The knot is the whole mechanic

A vine hangs invisible. What the level shows is a **knot** tied to the ceiling - your own art - and
the object in `glowLook` is switched on whenever the wizard is close enough to reach it. Press the
button and `CallDown()` unrolls the vine over `unrollTime` and you are already swinging on it.

That means a vine costs the player nothing to walk past and everything to notice — and it is why
the glow look matters more than any number here. If a player never learns what a glowing knot
means, no vine in the game ever gets used.

`staysDown` (on by default) leaves a called vine hanging for the rest of the level: finding it was
the achievement, not re-finding it. Turn it off and it rolls back up — and it rolls itself up by
**asking** whether the wizard is still on it rather than waiting to be told, because letting go
with Jump never passes through the spell at all.

Reach is measured to the nearest point *on the vine* rather than to the knot, so a long vine can be
caught anywhere down its length. Give the knot and the rope a sorting order above the tilemap
yourself — at −1 a vine hangs behind the wall it is tied to and simply cannot be seen.

The drawn rope leans with the swing: it is turned to the angle the wizard is actually hanging at,
rather than standing bolt upright while they arc away from underneath it.

## Scripts never change your art

**What you place in a scene or prefab is what the game shows.** No script draws art, recolours it,
resizes it, swaps its sprite or forces its sorting order, and no script has a slot for a sprite to
paint with. When something needs to show a state - a vent about to blow, a plate being pressed, a
knot in reach - it has a **Look** slot: drag in a child object holding your own
art or animation and the script only switches that object on and off. Leave the slot empty and
nothing is shown.

Spell effects work the same way, as children of the wizard found by name: **Glide Canopy** (under
`Visual`, so it faces the right way), **Haste Trail** (anywhere under the wizard), and an object
with a **Fling Arrow** component (a direct child of the wizard, art pointing right, Danger Look
optional). Missing one just means that effect does not show.

**Placeholder art ships in the prefabs**, so every effect shows something until the real art
arrives. The sprites are plain shapes in `Assets/Art/Placeholders`:

- **PLAYER:** a `Glide Canopy` under `Visual`, and a `Fling Arrow` with an `Arrow Art` child and a red
  `Danger Look`.
- **Hand Hold:** a blue `Glow Look` behind the hand.
- **Stone Wall:** a stone block on its `Body`.

To replace one, open the prefab and drag your sprite onto that object's Sprite Renderer, or put your
own object in its place under the same name. Haste Trail is the one still empty: until a child of
that name exists, Haste shows nothing.

The only things a script still moves are the ones where the movement *is* the mechanic: the
wizard's `Visual` is flipped to face their way and spun when they tumble, the staff sprite is
flipped, the vine's rope is stretched as it unrolls and leans with the swing, the Wall Growth
block is scaled as it grows, and the fling arrow is pointed along the aim.

## Getting to the loadout, and making it stick

Three separate faults made "I cannot swap abilities" true, and all three had to go.

**The screen could not target a slot.** Every equip path went through `Progress.FirstEmptySlot()`,
and the rail along the top was `Ui.CreatePlate` images with no `Button` on them — a read-out, not a
control. Swapping Q and R was not expressible. The verb now is **pick a spell, then press the
button you want it on** (or click the rail cell). Those four actions already exist and are already
enabled, and the glyph on the rail is the same one the HUD prints under that slot in play — so the
binding teaches the binding.

**`Progress.Equip` was a move-into, not a swap.** It cleared the key from wherever it was and
overwrote the target, so dropping one spell onto another lost the second one silently.
`Progress.Place` trades them instead. `Equip` keeps its meaning for the paths that want it.

**`Playtest` re-dealt the loadout on every scene load.** `BeginSandbox()` → `Clear()` blanks
`ranks` and `equipped`, and it ran at execution order −100 — a frame ahead of the spellbook reading
them. Sandbox is now a property of the play **session** and seeding it is a one-time act:
`Progress.IsSandboxSeeded` gates it, `redealOnEveryLoad` forces it back for when you are tuning the
ticks. `SceneManager.LoadScene` does not re-run `RuntimeInitializeOnLoadMethod`, so `Progress`'s
statics would have survived the reload on their own — Playtest was the entire failure.

**And you could barely reach the screen.** It used to open only from a rest site or the death
screen, both of which ended the run first. Now **Play** opens it from the main menu, **Start
again** on the death message opens it the same way, and walking into a **shop stop** opens it
mid-level. The **Spellbook** button (`UI/Loadout`, Tab) also opens it mid-level from anywhere, but
that one is a **debug tool** for testing loadouts, not part of the game. The button's door is the
nested `SkillScreen.Door`, installed once when the game starts; the shop stop and the button both
go through `SkillScreen.OpenInLevel`.

## Testing a spell without earning it

Drop a `Playtest` component on anything in the scene. It lists every spell in the book with a box
each — tick the ones you want, press Play, and they are already learned and in a slot. The list
fills itself in from `Assets/Resources/Spellbook.asset` and keeps itself in step, so a spell added
later turns up on its own. `scrap` and `bonusHearts` start you with a purse and a longer bar for
trying the skill screen.

**Nothing it does is written to the save.** `Progress.BeginSandbox()` wipes what is in memory and
puts `Save()` to sleep for the session, so a playtest cannot spend, unlock or consume anything in
the real one. Dying in a playtest wipes the sandbox, not the real save, and the ticks are dealt
again when the next level loads. It runs early (`[DefaultExecutionOrder(RunBeforeTheWizardWakes)]`, which is −100), ahead of `PlayerCharacter.OnAwake`, which
is the frame the spellbook reads `Progress` and builds its slots.

Spells welded to a slot are placed **first**, before any tick is honoured. Otherwise a ticked spell
lands in the Staff's own slot and the spellbook evicts it a moment later when it puts the Staff
where it belongs. That leaves three: four buttons, one of them the Staff's. Tick more and it says
which ones it could not fit rather than quietly dropping them.

### Why the book is a field

`Playtest` keeps a reference to the `AbilityBook` rather than looking one up when it needs it, and
that is not an optimisation. The list of tick boxes is built from the book, and it has to be built
somewhere Unity will actually **save** the result.

The first version deferred that work to `EditorApplication.delayCall`, to keep `Resources.Load` out
of `OnValidate` — where it can be refused mid-deserialisation. But a serialized field written from
`delayCall` is changed on the live C# object only. Nothing marks the component dirty, so the list
sitting in the inspector never reached the scene file, and entering Play Mode deserialised an empty
one straight back over it: no ticks, nothing granted, every slot empty.

With the book in a field, `SyncWithTheBook()` runs directly in `OnValidate`, where Unity re-serialises what it
changes. The deferred path survives only to fill in a component that predates the field — and it
calls `EditorUtility.SetDirty` afterwards, which was the piece missing all along.

## Prefabs

Everything below is a plain object you drag in. All the art is Unity's built-in Square tinted a
flat colour, so each one is visible the moment it lands and is waiting for a sprite of yours.

| Prefab | What it does | Layer |
| --- | --- | --- |
| `Basura Mágica` | Magical Scrap, the currency. Goes straight into the purse. Taken until the wizard dies. | Default |
| `Heart Upgrade` | +1 max HP. Taken until the wizard dies, like everything else. | Default |
| `Wet Floor Sign` | `TripHazard`: trips a runner. `minimumSpeed` 3, so a walk is safe. | Hazard |
| `Rake`, `Rake 1` | `TripHazard` aimed Left or Right: trips you BACKWARDS, the way you came. | Hazard |
| `Ice` | Ground that does not hold you. Nothing else changes. | Hazard |
| `Stairs` | `TripHazard`: lay it over a staircase. Take them fast and you tumble down them. | Hazard |
| `Bouncy Trash` | `BouncyTrash`: bounces you three boxes and sends you tumbling. | Hazard |
| `Wind` | Pushes you the way its arrows point. Rotate it to aim it. | Hazard |
| `Wind Trap` | The same push, on a timer, with a Warning Look and a Blowing Look. It shoves, it never hurts. | Hazard |
| `Hand Hold` | The knot Mage Hand catches. | Default |
| `Boulder` | What Telekinesis lifts. Solid, so it is also a platform. | **Ground** |
| `Stone Wall` | What Wall Growth raises. Already wired into the spell. | **Ground** |
| `Level Exit` | The bottom. Marks the level completed and opens the Level Select. | Default |

The steampunk set for the second level lives in `Assets/Prefabs/Mid Level Hazards`, and is set up
by hand like every other prefab. Every coloured box in them is a child called `Stand-in`: put your
art beside it and delete it.

| Prefab | What it does | Layer |
| --- | --- | --- |
| `Steam Vent` | Quiet, Warning Look, then a burst of steam that burns while you stand in it. | Hazard |
| `Wax Candle` | Drips a `Wax Drop` on a rhythm. Select it to see where drops land. | Default |
| `Wax Drop` | Hangs as a warning, falls, burns whoever it lands on. | Hazard |
| `Moving Platform` | Slides along `travel` and back, carrying the wizard. | **Ground** |
| `Pressure Plate` | Stand on it for a second and it throws you up. | Hazard |
| `Broken Glass` | A plain `Hazard`: cuts you every second you are on it, unless you are walking across it. Standing still on it hurts, walk mode or not. | Hazard |

The ones on **Ground** are there because the wizard has to be able to stand on them, and the ground
check only looks at that layer. The ones on **Hazard** are triggers you pass straight through.
`Hazard.passThrough` sets that on Awake, so ticking the box fixes one already placed, and wind and
ice are triggers in their prefabs.

`Stone Wall` is shaped the way the spell needs: the root carries nothing and the **first child**
carries the sprite and collider, one box square and centred on itself. The spell centres that
child on the root and scales the root up from nothing over `growTime`. Any stone art of your own
wants that same two-object shape.

**Where it grows is the whole spell.** It only ever makes the floor you are stood on longer. It
walks ahead of you along that floor, up to `reachInTiles`, and grows one block in the first empty
tile past its edge, with its top level with your soles so you walk straight onto it. A wall in the
way stops the hunt, and it will not cast while you are in the air or falling, so there is no way
to grow a block on the far side of anything. Stand on the first block and cast again (rank 2) and
the second one carries on from the first.

`Level Exit` marks the level completed (`Progress.CompleteLevel`) and loads the `Level Select`
scene.

## Level Select

A scene of its own, `Assets/Scenes/Level Select.unity`, set up by hand. It is a black screen, a
`Tower Map` image for the map art, and three pips on the map — `Top Pip`, `Mid Pip` and `Bottom
Pip` — each a button whose label carries a `LocalizedText` (`levels.top`, `levels.mid`,
`levels.bottom`).

`LevelSelectMenu` sits on the canvas with one entry per pip, top of the tower first: the scene it
loads and the button. The first level is always open; every other one opens once the level above
it is completed. Completed levels are saved, so they stay open across launches until the wizard
dies or the save is reset. A pip whose scene is missing from Build Settings stays locked and says
so in the console. Selection starts on the deepest open pip.

**Level 2 and Level 3** were set up by hand from a copy of `Level 1`: the wizard, the camera, the
light, the HUD, the menus and the tile grid, a strip of floor and one Level Exit. Both are in
File > Build Settings right after Level 1, so the pips can open them.

Finishing Level 2 opens Level 3, because `LevelExit` completes whichever scene is playing.


## Saving

There is no checkpoint and no respawn: every level starts the wizard where the scene places them,
and dying shows a message and starts the whole game again with nothing (see *Magical Scrap, hearts
and dying*).

**Everything is written the moment it changes.** Picking up Magical Scrap or a heart, buying,
upgrading, moving a spell and finishing a level each save on the spot, so quitting halfway down and
coming back carries on with everything you had. The save is also **read back** —
`Progress.Load()` runs on `BeforeSceneLoad`, after `ResetOnPlay` clears the statics.
`Progress.ResetSave()` wipes it — on a death, and from Settings ▸ Reset Save — and lets saving
start again if the old file could not be read. Settings live in their own file and survive both.

The save carries a `version`, now **2**. Version 1 kept a banked `wisps` count and a `spent` list;
`JsonUtility` ignores fields it does not know, so an old save still loads without errors — its
spells, loadout, hearts and finished levels come across, and the purse and taken pickups start
empty.

**Where it is written.** The save is pretty-printed JSON in `<project>/Saves/progress.json`
— beside `Assets`, so it is in the working copy where you can open it, read it and diff it. It used
to be PlayerPrefs, which on Windows is a handful of registry values nobody can track, and which hid a
stale save so well that turning the playtest sandbox off made a long-forgotten Glide reappear.
`Assets/Scripts/Core/SaveFile.cs` does the reading and writing: it writes to a scratch file and swaps
it in, so a crash mid-write cannot truncate the save; it sorts the ranks and the taken list so a diff
means something; and it answers **three** ways, not two — `Missing`, `Loaded` and `Unreadable`. That
third one is the whole point. A save that exists but will not open must never read back as "nothing
saved", or the next purchase writes a blank over it. A build keeps the same promise beside the
executable, dropping to the OS save folder only when that is read-only.

## Tripping

A trip **launches you onward** — your speed, plus `launchForward`, never below `minimumLaunch`, and
`launchUp` of lift so you actually leave the floor. Landing back down, the skid bleeds at
`slideFriction` boxes per second squared. All of it on `PlayerLogic ▸ Ragdoll`, and all of it the
same every time: a wet floor sign, a Bouncy Trash and a bad staircase throw you identically, because none of
them supply their own knock — they just call `TryTrip()`.

The one exception is the **rake**, which calls `TryTrip(int)` to throw you *back the way you came*. That
overload also drops the momentum you arrived with, because `Begin` adds `launchForward` to your
current speed: keep it, and a wizard running in at 4 boxes a second and "thrown backwards" comes out
still going forwards. Do not add a second hazard that reverses you — one is a punchline, two is a
movement system that takes things back.

**The sprite tumbles; the collider does not.** A rotating box levers itself up on its corners — its
half-diagonal is longer than its half-height, 0.64 against 0.52 here, so the solver must lift it
0.11 boxes every time a corner swings down. That reads as bouncing along the floor, and no material
setting removes it because it is geometry, not bounciness. It also makes how far you slide depend
on which corner happens to be down. Spinning the art instead costs nothing and looks the same.

The wizard's `Rigidbody2D` rotation is frozen and nothing in the game ever unfreezes it.

**Bump stars.** `RagdollBumps` on the PLAYER spawns its `bumpEffect` wherever a tumbling wizard hits
something:

- **Floor and ceiling:** any collision faster than `minimumBumpSpeed`.
- **Walls:** the tumble turns round just *before* it touches a wall, so a wall hit would never count
  as a collision. `Ragdoll.BouncedOffAWall` reports those bounces instead.

`secondsBetweenBumps` stops a shower of stars. The effect is whatever prefab is plugged into the
PLAYER's `bumpEffect` slot by hand — a short burst of stars is what it is meant for. Any prefab
works in the slot, and each copy is cleared away after `secondsToKeepEachBump`.

## Slopes, steps, and why neither of them slows you down

Two separate pieces of `Movement` handle uneven ground, and they answer different questions.

**`TryRunAlongRamp` steers ALONG a slope instead of across it.** Driving sideways into a 45 degree
face cannot work here and the numbers say why: acceleration is 20 boxes a second squared, of which
only cos(45) — about 14 — pushes up the face, while gravity pulls about 21 straight back down it,
and there is no friction to make up the difference. The wizard loses that argument every time and
slides. So on a ramp both velocity components are written together and gravity is left out of the
sum entirely, which is also what makes letting go of the stick leave them stood on the slope
instead of sliding off it.

**The target speed is a HORIZONTAL one**, the same number the flat ground uses, so it is divided by
the tangent's x before being applied along the face — and so are the top speed and the
acceleration. Without that divide a 45 degree ramp quietly costs the wizard 30% of their pace the
moment they touch it and hands it back at the top, which does not read as *that was steep*; it
reads as the stairs being sticky. With it, a slope changes neither how fast they end up nor how
long it takes to get there. The divide is bounded by cos(`maxSlopeAngle`), so a wall-ish surface
sneaking past the walkable test cannot divide by nothing.

**`StepUpSmallLedges` carries them over a lip a box collider cannot climb.** The wizard is a box with
square corners, frozen rotation and no friction, so a lip is a flat vertical face meeting a flat
vertical face; the solver's only answer is to delete the sideways speed, and `Run` puts it straight
back on the next step. That is what "stuck on the scenery" feels like — the stick is forward, the
wizard is not moving, and nothing is actually broken.

It drives **`linearVelocityY`, not `position`**, and that is the whole of "the movement teleports".
Writing `Rigidbody2D.position` outright moves the body a quarter of a box in a single frame *and*
makes Unity throw away that frame's interpolation, so even a small step snapped. Driven by speed,
the same climb spreads over three or four physics steps and interpolates across every rendered
frame between them.

Two things follow from using speed, and both are handled:

- It goes **straight up**, never diagonally. The line to a destination out over the lip passes
  through the lip's own corner, so a partial move along it can end inside the geometry. The column
  directly overhead cannot, because the wizard is standing in it. Once their soles clear the lip,
  `Run` carries them forward on its own.
- The leftover speed is **taken back** the moment the lip runs out, or the assist would read as a
  small hop. `steppedLastStep` is what remembers, and the guard only takes back speed the assist
  could plausibly have produced, so a Bouncy Trash or a fling is left alone. It is the identical
  tidy-up `TryRunAlongRamp` does when a ramp ends.

`stepHeight` stays far below a whole box on purpose. This is for tile seams and the pixel-high
teeth along a ramp's edge, **not** for real steps — anything taller is what the staff is for.

## Ground, friction and getting stuck

**Contact friction is 0** (`Movement.surfaceFriction`), applied as a material in `Attach`.
Horizontal speed is written outright every physics step, so contact friction never helps the wizard
move — it only fights them, catching on corners and seams. The only thing that slows them is
`groundFriction`, which is a number you can see. That makes `Ragdoll.slideFriction` the only thing
that stops a tumble.

**Everything the wizard stands on must be on a layer in `Movement.groundLayers`** (Ground, layer 6).
Physics does not care about that mask, so a wrong layer still holds the wizard up — but every query
comes back empty, and then `IsGrounded` is never true, which means permanent air control,
no ledge detection and no staff. Tilemaps start on Default, which is how you walk into it. The
wizard logs a warning naming the mask if it has not found ground after three seconds.

A tumble also ends on a timer (`Ragdoll.maximumDuration`) as well as on landing. A ragdoll that can
only recover once grounded is one bad drop — or one wrongly-layered floor — away from a wizard who
can never move again.

## The staff

A child of the wizard with its own hitbox and sprite. **`reachAboveFeet` is the mechanic** — you
author how far above their feet the staff can get them, in boxes, and the pole is stretched to
whatever length produces exactly that. The hitbox and the art are derived from the reach rather
than tuned until they happen to add up to it, so the number you type is the number the climb obeys
and the number `WhyNot` quotes back at you. Raising `raiseHeight` or `gripHeight` spends part of
that reach and shortens the pole to pay for it; it does not make the climb taller.

**The wizard never jumps.** The staff is how they get up anything tall. What the staff does not have
to do is single steps: `Movement.stepHeight` is 0.55 and a painted tile is 0.5, so the
wizard walks up any one-tile lip on their own. The staff is for walls of two tiles and up.

It is a **held** button and a **tapped** one, and the two mean different directions. A hold is the
staff reaching for whatever is in front of you, and it asks again every physics step — raise the
staff, look, and the moment the wizard shuffles into range they go up. No stick input is involved:
holding it *is* the request to climb. A tap shorter than `StaffAbility.tapSeconds` is the other
request, reaching the pole down over a ledge instead. Holding it with nothing in front of you is not
a failure, it is the answer: the staff is in the air and you are still on the ground.

Two directions, then, and the **button** decides which — not the ground. Both end with the wizard
hanging on the same pole, driven by the same stick.

*Down*, on a tap at a ledge: the pole is driven in just past the lip with its top flush to the
ledge, so the far end is where your feet will end up and you can read the drop off it. Slide down,
and keep pushing down at the bottom to let go.

*Up*, on a hold against a wall: `Movement.TryFindClimbAt` measures the wall, `Staff.Pole.TryPlantAsClimb`
stands the pole against it, and from there it is the identical ride — push up to climb, push up at
the top to step over the lip. Releasing the button does **not** drop you off: a climb is a place you
are, not a button you are holding.

**A drop wins, and `StaffAbility` is what decides it — not the movement code.** `CanClimbHere` and
`TryClimbStaff` do *not* test `IsAtEdge`; they look for a wall wherever the wizard is standing. The
precedence lives in the ability: `CanCast` fires when either a ledge or a climb is there, `OnReleased`
plants the pole downward on a tap at a ledge, and `OnHeld` asks for the climb every physics step. At
a real ledge there is nothing in front of the toes, so the climb answers `NoWall` and the tap wins by
default. The two reaches are not the safety net they read as, either: `climbReach` is measured from
the toes and `ledgeCheckAhead` from the ground probe, which on the PLAYER prefab puts the wall probe
about 0.15 boxes *further* out than the ledge probe, not nearer.

### Finding the wall: one swept box

`Movement.TryFindWall` sweeps a single box forward from the toes — one `Physics2D.BoxCast`, 0.05
boxes thick, as tall as the climbable band (from the top of the step assist up to the top of the
staff's reach), travelling `climbReach` past the toes. Where it stops is the face.

It used to be one ray, at one height, a hair above the step assist — and that is the whole reason
the staff answered "no wall" while the wizard stood flush against one. A tile whose collider is
bevelled, notched, or simply starts a few pixels up is empty at exactly that height and solid
everywhere else. Then it was a fan of rays every quarter box, which fixed the common case and left
the gaps between the rays. A sweep has no sample heights at all: nothing can hide between two of
them, and nothing can get lucky by sitting on one. Nearest is now the definition of the result
rather than a signed comparison across a loop.

The box starts 0.02 boxes **behind** the toes and travels that much further. A wizard resting
against a wall is allowed to sink into it by the contact offset, so a box starting exactly on the
toes would begin *inside* the collider in the mechanic's main case and report a distance of zero.
Backed off, the distance the box travels is the real gap, and the face is `toes + distance - 0.02`
exactly.

A forward cast that reports **distance zero** has begun inside ground, and is refused as `Blocked`
rather than guessed at. `queriesStartInColliders` is on and the tilemap is one merged composite, so
a zero-distance hit carries no usable point and hides the whole rest of the level behind it. Since
the backoff rules out the wall the wizard is pressed against, the only thing that can be in that
strip is a ceiling or an overhang right over them — which is what the message says. The fan used to
answer this case with a face at the toes and then a `TooTall` from a down cast started inside the
same rock.

Staircases are still walked rather than climbed, and it is the **down** cast that decides it, not
the choice of nearest hit: the down cast runs from the top of reach down to `soles + stepHeight` and
no further, so a tread whose top is under the step assist returns no hit at all (`NothingOnTop`) and
the assist walks it. The first tread's face is entirely below the band; the forward probe never saw
it under the fan either.

From the face, `TryFindClimb` casts **down** from as high as the staff reaches, inset past the face
so the ray lands on the surface rather than skimming the wall it is measuring. A down cast that
reports **distance zero** is refused: a cast beginning inside a tall wall answers with a top at
exactly the height it was asked about, which would make every tower in the level read as climbable
right up until you were left dangling.

Then two overlaps: the wizard has to fit standing on the lip, and the space **above their head** has
to be clear all the way up. That second box is narrow — six tenths of their width, centred on them —
because the wall is a hand's breadth away and a full-width box catches it every time. A full-width
column check is what made the first version refuse while the wizard stood flush against exactly the
wall it had been asked about.

`Movement.DrawGizmos` draws the box the probe actually swept, at the size and place it was last
cast, red when it came back `NoWall` or `Blocked`, with a tick on the face it found. A wall the
wizard is plainly against that the box does not reach is a wall the staff will refuse; a tick that
lands two pixels proud of that wall is why an obvious climb was refused — the tiles are
`ColliderType.Sprite`, so their outlines follow the sprite alpha, and switching the load-bearing
ones to `ColliderType.Grid` is the fix. There is no other way to see either. Before play there is no
staff to ask for a reach, so the gizmo draws only the floor of the band, `climbReach` long: that
line is `stepHeight` and `climbReach`, the two numbers you author there.

### When it still says no

A held spell never expires a buffered press, so the console line `Spellbook` prints for an ordinary
refused press could never fire for this one — a button held down with nothing happening was exactly
the silence that line exists to break. `Spellbook.AdvanceCharge` now reports a hold that is coming
to nothing, once, after a third of a second, and `StaffAbility.WhyNot` splits the two cases that
look identical from the outside: *nothing in reach to raise it against*, and *what is ahead is too
tall, or there is no room on top of it*.

**The staff rides whatever it is planted in.** A planted pole is a set of world positions — where
the pole stands, where the wizard hangs, where a climb lands — so on a `Moving Platform` the
platform used to slide away and leave the wizard hanging from nothing. Now `Pole.RideOn` looks at
the ground the pole went into, at the lip it hooked or the ledge it went over, and if that ground
belongs to a Rigidbody2D that is not Static it remembers it. `Pole.FollowCarrier` runs first thing
in every `Slide` and shifts all of those positions by however far that body moved since the last
step. It reads the body's position, so the wizard trails the platform by one physics step, about
a pixel at the prefab's speed. Climbing down off a platform's edge and climbing up onto one both
work this way, and topping out leaves the wizard standing on the platform, which carries them from
there as normal.

`Staff.cs` can still lay the pole flat as a **Bridge** — `StaffMode.Bridge`, `TryPlantAsBridge`, and a
separate solid collider on a child on the Ground layer, because the staff itself is on the Player
layer, which the ground check deliberately ignores. Nothing casts it since the Staff Bridge spell
was retired. It is left in because it works and is one small class away from coming back.

The guards it left behind are still earning their keep. `IsStaffFree` says whether the pole is busy,
and `TryPlantStaff` refuses a pole already in the ground. Without that, standing on your own bridge puts you at a lip with `IsAtEdge`
true and the Staff spell would happily re-plant the same pole as a ladder, pulling the floor out
from under you.

## Hazards

**The family tree.** Everything the wizard touches starts from `Item`:

```
Item                      finds the wizard in its trigger; shared Look helpers
├── Zone                  something you are INSIDE: Affects On Staff, Affects Ragdolled
│   ├── Hazard            catches you: speed gate, re-arm, damage, walking (a plain Hazard just hurts)
│   │   ├── TripHazard    trips you - wet floor sign, stairs, rakes
│   │   ├── BouncyTrash   bounces you
│   │   ├── SteamVent     burns on a timer
│   │   └── WaxDrop       falls and burns
│   ├── WindZone2D        pushes you (WindTrap: on a timer)
│   └── SlipperyFloor     takes your grip
├── Pickup                collectables: Gives Magical Scrap or a Heart
├── ShopStop              opens the skill screen mid-level
└── LevelExit             the way out
```

A hazard only gets its own script when it **does** something different. Hazards that only differ in
numbers share one script, and the prefab holds the numbers: the wet floor sign, the stairs and both
rakes are all `TripHazard`, and broken glass is a plain `Hazard`.

| | What it is | Notes |
| --- | --- | --- |
| `TripHazard` | Something you clip at a run | `minimumSpeed` 3 means a run trips and a walk does not. **Aim** sets the way you go down: the way you were running (wet floor, stairs), Left or Right (the rakes, which throw you back the way you came), or the way its art faces. **Extra Kick** and **Extra Lift** add a shove on top. |
| `SlipperyFloor` | Ground that does not hold you | Calls `Slicken` every step you are in it; nothing to leave, because not calling IS leaving. |
| `BouncyTrash` | Fall into it, get thrown back up | Launches you on the way past. The `Bouncy Trash` prefab. |
| `WindZone2D` | A volume that pushes you | One `Vector2` covers left, right, up and down. `groundScale` decides how much you feel with both feet down. |
| `WindTrap` | A `WindZone2D` on a timer | Shuts, warns, then blows. `Blast` is a one-off `Shove`; the rest is the base class's steady `Push`. No damage: it is a `Zone`, not a `Hazard`. |

**Nothing on the Hazard layer blocks you.** `Hazard.passThrough` is on by default and applied on
`Awake`, so hazards are things you pass straight through that do something to you on the way, not
things you bump into. That is also why they belong on layer 8: the ground check ignores that layer,
so a *solid* hazard there would be something the wizard comes to rest on while the game still
believes they are falling — no step assist, and no way out of a tumble, since a ragdoll only recovers once
grounded. Ticking `passThrough` off is supported, but move it off layer 8 if you do.

`Hazard` handles speed gating, re-arming and damage. `Zone` decides whether it can reach a wizard
who is on their staff or already tumbling. **A new hazard that only hurts needs no script at all:**
use a plain `Hazard`. One that does something new is one subclass with one `Affect` method.

**`Hazard.everyStep` is the difference between a thing and a place.** Off — the default — it fires
once, on the way in, which is right for anything you HIT: a Bouncy Trash, a rake, a sign. On, it is asked
every physics step you are inside it, which is right for a stretch of floor, where breaking into a
run half way along has to count for as much as arriving at a run. `rearmDelay` is what stops it
firing fifty times a second. `SlipperyFloor` and `WindZone2D` are `Zone`s, not hazards: they act
every step you are inside them and have no gate at all — a floor cannot be dodged by being slow.
That is also why they no longer show the Hazard settings, which did nothing on them.

### Stairs

The steps themselves are **tilemap**, not the hazard. `Movement` already walks them: the slope
handling steers along the face and the step assist carries the wizard over each tread. The `Stairs`
prefab is a `TripHazard` trigger box you lay over the top saying *this stretch of floor is a
staircase*, and all it does is `TryTrip()` above `minimumSpeed`.

That gives the whole rule in two numbers a designer can see: **run at them and you go down them the
way you were already travelling; walk, or jog, and you go up and down without noticing.** The
tumble is `Ragdoll`'s, shared with every other trip in the game, so all of them tune together.

### Seeing the wind

A rock and a Bouncy Trash are objects — you can see them coming. Wind is a volume, so it is the one
hazard a player can only learn by being caught out unless the level shows it: put your own wind art
(an animated sprite, particles) inside the zone. The script draws nothing in the game.

In the editor, `WindZone2D` draws **arrows** as scene-view gizmos *without selecting the zone
first* (`alwaysShowArrows`). Arrow length reads the strength against the same scale as everything
else: a run is 6 boxes a second, so a gale you cannot walk out of draws longer than the wizard is
tall.

**`push` turns with the object.** Rotate a Wind or a Wind Trap and its wind - and a trap's
`kick` - turn with it, which the arrows show. `groundScale` only softens the *sideways* push while
the wizard is standing; up and down is always felt in full, so an updraft lifts a wizard who is
standing in it.

**Hazards push you onward, never back.** Rocks, Bouncy Trash and tumbles all send you the way you were
already travelling — `Movement.TravelDirection`, which is the direction you *arrived* in, not the
direction you happen to be facing and not away from the hazard's own centre. Being thrown back the
way you came is the least predictable thing a hazard can do: you lose the ground you covered and
land somewhere you were not looking. Tripping keeps your speed too (`Ragdoll.momentumKept`), so a
trip is a loss of footing rather than a wall.

Two things follow from that, and they matter when you place hazards: a hazard read live from
`HorizontalSpeed` would measure *after* the impact — which against a solid rock is nearly zero, so
every speed gate would refuse to fire. Both direction and speed come from the value recorded at the
end of the previous physics step instead.

Two things worth knowing:

- Hazards go on **layer 8 (Hazard)** and at the **scene root**. Several platforms in the test level
  carry non-uniform scales that would squash anything parented under them.
- `Item` only answers to the wizard's **Hurtbox**: a trigger child of PLAYER called `Hurtbox`,
  shaped to the art and separate from the collider the wizard walks with, so a hazard that touches
  the hat or the robe counts. Everything else on the wizard's rigidbody — the walking collider, the
  staff's trigger — is ignored, so no hazard fires twice. If the child is missing,
  `PlayerCharacter` warns and falls back to the walking collider.

## Health and the hat

There is no health bar. **The hat is the health display**, and it lives in the PLAYER prefab, the
same way the staves do:

```
PLAYER
└── Visual           flipped to face left/right, spun when tumbling
    ├── Health 3     SpriteRenderer + Animator + Health Look (health 3)
    ├── Health 2     SpriteRenderer + Animator + Health Look (health 2)
    └── Health 1     SpriteRenderer + Animator + Health Look (health 1)
```

Each health level is its own child with its own art and its own Animator, and its `Health Look`
says which health it stands for. The wizard starts with as much health as the highest look, and
`PlayerAnimator` switches on only the look matching their health right now (the highest one, if a
heart pickup has given them more). A hit that changes the look plays the new look's `Hat Change`
clip; a hit that does not plays `Take Damage`.

**Right now the PLAYER has only `Health 3`, and `TakeDamage.anim` is empty**, so a hit changes
nothing you can see: the hearts go, the hat stays, and the third hit kills. That reads exactly
like hazards not working. Until `Health 2`, `Health 1` and the clip are filled in,
`PlayerLogic.reportHits` (on by default, editor only) prints every hit to the console - *"Broken
Glass took 1 heart(s) from the wizard - 2 left."* `PlayerLogic.Hurt` takes that cause as its second
argument: a hazard passes its own name, a fall passes how far it was.

Every look's Animator uses `Player.controller` - or an Animator Override Controller made from it,
holding that look's own clips. The clips animate the sprite of the object the Animator is on, so
one set of clips works for any look.

## Animations

`Player.controller` is set up by hand: its states, transitions and clips are yours, and no script
ever writes to a clip - the frames, timing and looping in a clip are exactly what you put there. To
fill a clip: select a Health Look in the PLAYER prefab, open Window > Animation > Animation, pick
the clip from the drop-down, and drag the sliced frames of a sprite sheet onto the timeline. Set
Samples (the frame rate) in the same window.

## HUD

An ordinary screen-space `Canvas` you can open up and restyle:

```
HUD              Canvas (Overlay, order 10) + CanvasScaler + PlayerHud
├── Spell Bar    bottom-left, HorizontalLayoutGroup + ContentSizeFitter
│   └── Spell Slot   TEMPLATE, inactive - Image + HudSlot
│       ├── Icon         the spell's own icon
│       ├── Charge       Image, Filled/Radial360 - the running and cooling wipe
│       └── Button       TextMeshProUGUI
└── Scrap Count  top-right, TextMeshProUGUI - built at runtime unless Scrap Label is set
```

Slots are **copies of the template**, made at runtime, one per spell. Restyle the template —
sprite, size, colour, add a border — and every slot follows. `PlayerHud` only ever fills them in;
it never builds layout. The icon each slot shows is the spell's own `icon`.

The **Scrap Count** shows the one purse: how much Magical Scrap the wizard has, from the `hud.scrap`
line (`{0} Magical Scrap`, `{0} Basura Mágica`). Drag your own label into `scrapLabel` to place it
yourself, tick `hideScrapCount` to show none, or type a `scrapFormat` for a one-off wording.

Set `CanvasScaler.referencePixelsPerUnit` to **32**, not the default 100, or every icon comes out
at a third of its size.

It finds the wizard through the singleton and re-checks every frame, because every scene load
destroys them and builds a new one — anything that had subscribed would be holding a destroyed
object.

The button under each spell follows **whichever device is actually in use**: `E` on a keyboard,
`X` on an Xbox pad, `Square` on a DualSense. `Core.Controls` watches only the actions this game
asked for, which is what stops a mouse twitch flipping the HUD back to keyboard letters mid
gamepad play. Asking for the glyph without naming a device returns `"E | X"` — always go through
`Controls.Glyph`.

No `GraphicRaycaster`, on purpose: the HUD must never swallow a pause-menu click or steal
controller focus from the menus. The `Charge` image must stay set to **Filled** — an `Image` with
no sprite, or one set to Simple, draws a plain quad and silently ignores `fillAmount`, so every
cooldown would sit frozen at full with nothing in the console.

## Input

| Action | Keyboard | Gamepad | Read by |
| --- | --- | --- | --- |
| `Player/Move` | WASD / arrows | left stick | `PlayerCharacter.Controls` |
| `Player/Jump` | Space | south | `PlayerCharacter.Controls` — only to let go of a vine; the wizard never jumps |
| `Player/Walk` | Left Shift | left trigger | `PlayerCharacter.Controls` |
| `Player/Spell1` | Q | left shoulder | slot 1 — the Staff, always |
| `Player/Spell2` | E | west | slot 2 |
| `Player/Spell3` | R | right shoulder | slot 3 |
| `Player/Spell4` | F | north | slot 4 |
| `UI/Pause` | Esc | start | `Core.Controls` |
| `UI/Skip` | Space, Enter, click | south, start | `Core.Controls` |

Looking down, climbing the staff and swinging on a vine all read `Move`, so they need no action of
their own.

The four spell actions are bound to the four slots **by index**, once, in `Spellbook.Attach` — the
action never changes, only what is sitting in front of it. That is why adding a spell needs no
input work at all, and why the HUD's per-slot glyph cache is safe: a slot's button is fixed for the
life of the scene even as its contents change.

## Setting a scene up by hand

There are no editor tools: every prefab and scene, including the tilemap collision, is set up by
hand:

**Tilemap ground**, on the Tilemap that actually has tiles painted on it, never the Grid:

| | |
| --- | --- |
| `TilemapCollider2D` | the shape of the painted tiles |
| `Rigidbody2D`, **Static** | a composite needs a body, and a Dynamic one makes the level fall on Play |
| `CompositeCollider2D` | welds the per-tile boxes into one outline, so you cannot catch on a seam |
| `compositeOperation` = **Merge** on the tilemap collider | without it the composite stays empty |
| layer **Ground** | or the wizard never registers as standing on it |

A `Grid` is a coordinate system. A collider or a rigidbody on one is always a mistake.

**Hazards.** A trigger collider plus one of the hazard scripts above, on layer **Hazard**, at the
scene root — several platforms carry non-uniform scales that would squash a child.
`Hazard.passThrough` sets the collider to a trigger on Awake, so an existing one is fixed by
ticking a box.

**HUD.** A `Canvas` (Screen Space Overlay, order 10) with a `CanvasScaler` at
`referencePixelsPerUnit` **32** and **no `GraphicRaycaster`**, laid out as the diagram above. The
template starts inactive; `PlayerHud` copies it.

**Background.** Plain sprites placed in the scene. Put them on a sorting layer below `Default` —
**and add that layer to the `Light2D`'s Target Sorting Layers**, or in URP 2D they render pure black. Simpler alternative:
leave them on `Default` at negative sorting orders, which the existing light already covers.

## Worth knowing

- `groundLayers` must exclude the wizard's own layer or they stand on themselves. It defaults to
  Ground (layer 6) in code, and `Validate()` warns if you widen it. Layers: 6 Ground, 7 Player,
  8 Hazard.
- The project queries triggers by default, so every ground query passes
  `ContactFilter2D { useTriggers = false }`. Without it any trigger on the Ground layer becomes
  walkable floor.
- Never write `body.gravityScale` from a spell — `ApplyFallGravity` reassigns it 50 times a second.
  Use `Modifiers.FallSpeedMultiplier`, which is already threaded through both the gravity and the
  terminal-speed clamp.
- Buttons are wired in `Awake` in code, not through the inspector's OnClick list. Add both and they
  fire twice.
- The **Exit** button is always shown. Console certification usually forbids quitting to the OS from
  a menu, so hide it in `MainMenuController.WireButtons` for a console build.
