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
| `Core/` | `Game` (pause, scene flow, quit), `GameSettings`, `Controls` (every input lookup, plus which device is in use), `Progress` (the save: Knowledge, spells, loadout, hearts, finished levels, taken pickups), `SaveFile` |
| `Player/` | `PlayerCharacter` (the Unity side), `PlayerLogic` (everything the wizard does), `Staff`, `PlayerAnimator`, `RagdollBumps` |
| `Player/Abilities/` | `Ability` and the spells, `AbilityBook` |
| `Localization/` | `Loc`, `TextBook`, `LocalizedText` |
| `World/` | `Item` (the base of everything the wizard touches), with subfolders: `Hazards/` (`Hazard`, `TripHazard`, `BouncyTrash`, `SteamVent`, `WaxDrop`, `WaxCandle`, `PressurePlate`), `Zones/` (`Zone`, `WindZone2D`, `WindTrap`, `SlipperyFloor`), `Pickups/` (`Pickup`, `TimedReward`), `Level/` (`ShopStop`, `LevelExit`, `MovingPlatform`, `VineAnchor`, `StaffPlatform`, `MagicDoor`, `Furniture`), `Camera/` (`FollowCamera`) |
| `UI/` | `PlayerHud`, `HudSlot`, `FlingArrow`, `SelectedLook`, `ButtonPrompt`, `ButtonIcons`, `Ui` (with `BlipRow`) and the screens (`ChoiceScreen`, `SkillScreen`, `ControlsScreen`) |
| `Menus/` | `MenuScreen`, `SettingsPanel` and the three menus |
| `Editor/` | `PixelArtImporter` (import settings for everything in `Assets/Art/Sprites`; editor only, never in the build) |

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
  classes. `1-1.unity` refers to them by GUID, and the GUID lives in the `.meta` beside the
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
Glide's `Glide Canopy` and Falling Star's `Falling Star Trail` both use it.

**Every spell keeps its own script, on purpose.** Unity only loads a spell asset whose script file
has the same name as its class. The seven spells also really do behave differently, so one shared
"spells" class would need a switch over every spell, plus Inspector fields that each mean nothing
to most spells.

`usesPerLevel` above 0 gives a spell a number of casts that only the next level brings back — for
something a cooldown alone cannot hold back. `Slot.UsesLeft` counts them and the HUD prints it.
`usesAddedPerRank` lets an upgrade buy more of them: 2 and 1 would be two casts at rank 1 and
three at rank 2 (`Ability.UsesPerLevelAt`). The charges refill on the scene load that rebuilds the
spellbook, which is what a level transition is.

A spell that needs to remember something between frames — a trail it switched on, a rope it is holding —
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

**Sorting order is the trap it exists to catch.** The vine shipped at −1 and was therefore behind
the tilemap: solid, working perfectly, and completely invisible — which reads
exactly like a spell doing nothing. Anything a spell puts into the world wants an order above the
map.

The Staff is yours from the start and welded to E; everything else fights over three buttons.

| Spell | What it is | Rank 2 / 3 |
| --- | --- | --- |
| **Staff** | Plant it at a ledge and climb down, or raise it against a wall and climb up. | longer staff / longest staff |
| **Mage Hand** | A spectral hand you swing from. | wider swing / climb it |
| **Star Slap** | A magical star hand slaps a wall to shove you away from it, or knocks furniture flying. As often as you like. | slaps slow your tumble / a longer arm, slowing it more |
| **Glide** | A canopy for steering your fall. A wind catching it starts the fall over from there. | falls slower / a slow, steady sink |
| **Telekinesis** | A push of magic all round you: Magic Doors open, and the moving things around you stop for a few seconds. | longer sleep |
| **Falling Star** | While tumbling, dive into the ground and bounce back up on your feet. Once per level. | a second dive / a third dive |
| **Fling** | Hold to aim up or down (never more than 45° up), let go to be thrown into a tumble. | harder throw / fling while tumbling |

Each spell's picture is its asset's **Icon**, from `Assets/Art/Sprites/Icons/Spells`: 10 x 10
pixel art in the HUD's palette, which the HUD draws at 4x inside the ring. Redraw one there, or
drag a different sprite into the asset's Icon, and the HUD and the skill screen both follow.

Each spell's words live in `Resources/Language/Text.csv`, English and Spanish side by side:
`ability.<id>.name`, `.desc`, and per upgrade `.upgrade.<n>.title` (the upgrade button, "Longer
staff - 4") and `.upgrade.<n>.desc` (the card's "Next level" line). A line there wins over the text
typed on the spell asset, which is only the fallback. The card shows the description with the Next
level line under it, and has room for about two lines plus that one, so keep a description near
90 characters. Anything longer shrinks to the smallest size and then spills. For more room, raise
**Cell Size Y** on `Cards` and the `Blurb`'s **Preferred Height** by the same amount.

Two of them want level content: Mage Hand needs a `Vine`, and Telekinesis wants a `Magic Door`, a
moving platform, a trap, a rake or a Bouncy Trash nearby.

**Glide only forgives a fall through wind, and that is the design.** Every rank gives the same
steering (`airSpeed`, `airControl`, `drag`). While the canopy is out, any wind that catches the
wizard calls `PlayerLogic.RestartFall`, which moves the fall's highest point down to where they
are, every step they stay in it. The drop is measured from where they **leave the wind**, so an
updraft or a gust is a fresh start. It works in any wind, a downward one too, so keep downdrafts
short where a Glide player could ride one all the way down.

The ranks only change `fallSpeed`: 1 (no slowing) when learned, 0.6 at rank 2, and 0.2, a slow
steady sink, at rank 3. Note what that does *not* buy. Fall damage counts **boxes fallen**, not
how fast (`SenseGround` bills `highestPoint - position.y`), so a slower canopy lowers the speed
and bills you exactly the same. That is worth letting a player discover once. Away from wind, you
survive under a canopy by **going somewhere else**, not by falling softer.

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

**Telekinesis is the one spell that is not about moving the wizard.** It is the "make this part
easier" button; see *Telekinesis: doors and sleeping hazards*.

### Adding spell #5

1. One `.cs` in `Player/Abilities/`, deriving from `Ability`, named the same as its class.
2. One asset via **Assets ▸ Create ▸ Falling Wizard ▸ Abilities ▸ …**, with a `cost` in Knowledge.
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

Everything else is **bought with Knowledge** at the skill screen.

`Core.Progress` holds all of it, outside the wizard, because every scene load builds a brand new
wizard.

## Knowledge, hearts and dying

There is one purse and one save, and dying empties both:

| What | Where it lives | Kept while alive | After a death |
| --- | --- | --- | --- |
| Knowledge | `Progress.Scrap` | yes, on disk | gone |
| Spells and their ranks | `Progress.ranks` | yes, on disk | gone (the `known` spells come back free) |
| The loadout | `Progress.equipped` | yes, on disk | gone |
| Bonus hearts | `Progress.BonusHearts` | yes, on disk | gone |
| Finished levels | `Progress.completedLevels` | yes, on disk | gone |
| Taken pickups | `Progress.taken` | yes, on disk | gone, so every pickup is back |
| Settings | `GameSettings` | yes, on disk | **kept** |

**Knowledge goes straight into the purse** the moment a pickup is touched, and is spent at the
skill screen. There is no carrying and no banking: what you picked up is yours, and it is saved
straight away, so quitting halfway down and coming back carries on with everything you had.

**Dying starts the game again from nothing.** `PlayerCharacter.OnDied` calls `Progress.ResetSave()`
the moment the last heart goes, waits `secondsBeforeDeathMessage` so the death can be seen, then
`ShowDeathMessage` opens a `ChoiceScreen` saying so (`death.title`, `death.blurb`) with two buttons:
**Start again** opens the skill screen and then level `1-1`, exactly like Play on the Main Menu, and
**Main menu** goes back there. The save file is deleted and every list above is emptied. The wipe happens at the moment of death rather than when a button is pressed, so
pausing, quitting or walking away from the message cannot dodge it.

### Pickups

There is **one `Pickup` script** for every collectable. Its **Gives** setting picks what it is, and
**Amount** says how much:

| Gives | What it does |
| --- | --- |
| Knowledge | Adds **Amount** Knowledge to the purse. |
| Heart | Adds **Amount** hearts to the wizard's health and heals them. |

Either way the pickup's id goes into `Progress.taken` and `Pickup.Awake` destroys it on every later
load, so it stays taken until the wizard dies. Then the wipe empties `taken` and it is standing
there again.

To add a new kind of collectable, add a value to `Pickup.Gift` and a short `Give…` method for it.

The highest **Health Look** under the wizard is what a brand new save starts with (see Health
below) and `Health.maxBonusHearts` caps what can be added on top of it. A heart pickup that finds
the bar already at that cap pays out `knowledgeIfFull` Knowledge instead — never leave a player
standing over dead loot — and is taken all the same.

A `Pickup` remembers itself by **where it
stands** (quarter-box resolution, scene-qualified) unless you type an `id`. That survives renaming
and re-parenting; moving one makes it a new pickup. Give two pickups sharing a spot explicit ids.

The name is worked out once, in `Awake`, and kept. It has to be: the idle bob would otherwise drag
the position across a quarter-box boundary and quietly rename the pickup between being seen and
being touched. For the same reason **only the art bobs** — `FindArtToBob` refuses to bob a
`SpriteRenderer` sitting on the root, because that would walk the trigger around with it.

Drag in `Assets/Prefabs/Knowledge` and `Assets/Prefabs/Heart Upgrade`
and they work as they land: a trigger `CircleCollider2D`, the component, and an `Art` child to drop
a sprite onto. The code never changes that sprite — what you put there is what is drawn.

### Timed rewards

`Assets/Prefabs/Timed Reward` is one Knowledge and one Heart Upgrade the wizard can only grab
within **60 seconds** of the level starting. Every level has one, on the way to its exit. Drag it to
move it: its root sits on the floor, at the middle of the pair.

```
Timed Reward    TimedReward: Seconds Allowed
├── Knowledge, Heart Upgrade    the real pickups
├── Faded Scrap, Faded Heart    switched off until time is up
└── Timer
    ├── Running Clock    white, shown while it counts up
    └── Stopped Clock    red, shown once time is up
        ├── Minutes      a child for each number, 0 to 9
        ├── Colon
        ├── Tens         0 to 5
        └── Ones         0 to 9
```

**There is no clock on the HUD.** The timer lives on the prefab, always on, counting up in white
from `0:00`, so the player reads it when they reach the rewards. At the limit it stops and turns
red, reading `1:00`, each pickup still there is switched off, and its faded copy is switched on. A
reward already taken leaves no faded copy, and once none are left the timer goes away too.

`TimedReward` reads `Time.timeSinceLevelLoad`, so the clock starts over every time a level loads and
stops while the game is paused or a menu is open.

**Nothing here is drawn by the script.** It only switches objects on and off. `Faded Scrap` and
`Faded Heart` are ordinary sprites with the alpha turned down on their Sprite Renderers, so restyle
them there. The digits are slices of `Art/Sprites/Timer Digits.png`, drawn by hand because the
project has no pixel font: redraw that sheet and every timer follows. The sheet is white, and the
`Stopped Clock` digits are red because of the Color on their own Sprite Renderers, so that is where
to change the red. Changing Seconds Allowed needs no new art, up to 9:59, and the red clock always
reads it, so a level can have its own limit. To change what a reward gives, select its pickup in the
prefab, or in one level to change only that level.

### Shop stops

A `ShopStop` is the place to spend Knowledge mid-level. Walk into one — alive, with no other
screen open — and it opens the skill screen with `SkillScreen.OpenInLevel`, which pauses the game.
Buy spells and upgrades, rearrange the loadout, and leave with the back button or Escape: the
screen's close callback reloads the spellbook so the wizard has exactly what is on the slots, and
unpauses, and the wizard carries on from where they stand. It does not save a position, heal, or
end anything.

`Item` reports one entry per visit, so standing in a shop stop does not reopen the screen. Walk
out and back in to open it again.

Drag in `Assets/Prefabs/Shop Stop.prefab`: a trigger box one box wide and one and a half tall,
with a `ShopStop` on it and a placeholder block on its `Art` child for you to swap for real art.

### The storybook look

The menus share the HUD's palette, and **all of it is set in the prefabs themselves**: no script
paints a colour or swaps a sprite on a menu while the game runs, so whatever you change in a prefab
is exactly what shows. The pieces are in `Assets/Art/UI Kit`, all 9-sliced, drawn at 4 screen
units per art pixel (`Pixels Per Unit Multiplier` 0.78125 on the Image):

| Piece | Used for | Text on it |
| --- | --- | --- |
| `Panel Parchment` | every page: pause, title, settings, skill screen, controls, choices | plum ink, magenta titles |
| `Button Cloth` (hat red) | buttons, slider fill, scrollbar handles | cream, bold |
| `Card Cloth` (plum) | spell cards and slots, dropdowns, the fullscreen box, the slider track | cream |

Card Cloth is a dark plum on purpose: cream text on it reads at about 7:1, where the old mauve was
4:1 and hard to read at blurb size. The star from the wizard's Impact Stars marks checkboxes and
the slider handle. Behind every page is a `Shroud`: a see-through plum layer, opaque on the title
screen.

### Screens are prefabs

`ChoiceScreen` (the death message and the reset-save question), `SkillScreen` and `ControlsScreen`
are prefabs in `Assets/Resources`: `Choice Screen`, `Skill Screen` and `Controls Screen`. Each
`Open` loads its prefab, makes a copy and fills in the words, so to restyle a screen, open its
prefab and change it like any other UI. They sit at sorting order 200, 220 and 260, above the Pause
Menu's 100. All of them set `Ui.IsModalOpen`, and `MenuScreen.Update` checks it, so the pause
menu underneath does not eat Escape.

**Settings is one prefab too, `Assets/Prefabs/Settings Panel.prefab`.** The Pause Menu prefab and
the Main Menu scene each hold a copy of it, so edit the prefab and both follow. Its `Page` child is
the parchment; the rows, dropdowns, toggle, slider and buttons are inside it.

The parts that repeat - an answer button, a spell slot, a spell card, an upgrade pip - are
**templates**: one switched-off copy in the prefab, copied once per answer, slot, spell or rank and
placed where the template sits. Restyle the template and every copy follows. The Skill Screen
lists each template's parts (icon, name, button...) under **Slot Template** and **Card
Template**; keep those filled in if you rearrange a card.

The Skill Screen lays its cards out **two to a row** (`Cards`, a Grid Layout Group: change Cell Size
to resize every card). A card shows its states with Look objects you can redraw:

- **Locked Look** (a padlock on the icon) while the spell is not learned yet.
- **Equipped Look** (the button's own picture, top right) while the spell sits in a slot.
- **Pips**, one per rank: the `Pip` template is the empty blip, its `Full` child the filled one.

The card itself goes dim while the spell is not learned, and a learn button you cannot afford goes
dim, through each Button's **Disabled Color**. Change those tints on the Button components.

## Falling Star: dive out of a tumble

**It only casts while the wizard is tumbling** (`PlayerState.Ragdoll`), once per level, plus one
more cast per rank (`usesPerLevel` 1 and `usesAddedPerRank` 1: one, two, then three). The press
throws them head first along a diagonal: `diveAngle` (45°) below level, at `diveSpeed` (12 boxes a
second), towards the stick if it is held, otherwise the way they were already going. The dive
lasts until they have dropped `diveDepth` boxes (1.25 at every rank). That depth **is** the window: the ground has to be within it when they cast. Tighten or loosen it on the Falling Star asset.

- **Ground inside the dive: saved.** `UpdateRagdoll` checks for ground *before* `CheckLanding`, so
  the landing is thrown away (`Movement.ForgetLanding`) instead of billed. The tumble ends on the
  spot, the wizard bounces `bounceHeight` boxes up (2) with `bounceOnward` sideways, and the fall
  is counted again from the top of the bounce. The Impact Stars burst, as for any hit.
- **No ground inside the dive: a miss.** The dive simply stops and they go on tumbling, still
  carrying its speed. The fall was being counted the whole time from the highest point before the
  dive, so the landing costs what it would have, plus the extra drop. That is the risk.

Cast while skidding along the floor, the ground is already there, so it is an instant bounce back to
your feet: the safe way to use it to stop a tumble.

The dive lives in `Ragdoll` (`BeginDive`, `Dive`, `IsDiving`) because it has to beat the landing
check, which runs inside the ragdoll update, by a step. `Ragdoll.Tick` holds the dive velocity every
step, so neither gravity, wind nor the stick bends the line. It gives up early if the wizard stops
going down at all, so wedging into a corner cannot leave them hanging in a dive forever. The trail
is the `Falling Star Trail` child of PLAYER, a copy of Star Sparkles that streams behind.

## Star Slap: push off walls

The spell's id is `slap`; its script is `StarSlapAbility` and its asset `Abilities/Star Slap`.

**Falling Star saves you; Star Slap moves you.** It has no charges, only a 0.75 s `cooldown`, so it
can never be what stops a fall: it never pushes the wizard up and never restarts the fall count.
What it can do is put them somewhere better, such as onto the ledge they are falling past.

**From rank 2 it calms a tumble.** Every slap made while tumbling first takes `tumbleSlowdown` of
the wizard's speed away (`PlayerLogic.SlowTumble`): none when learned, a quarter at rank 2, half at
rank 3, which also reaches 4 boxes instead of 2. It slows the tumble, up and down included, but
the fall is still billed by height, so it never saves them from a drop.

The press slaps the way the stick points, snapped to the nearest of eight directions, or the way
the wizard faces with no stick held. `Aim` sweeps the wizard's own hitbox that way, up to `reach`
boxes from their edge (2, 2, then 4 at rank 3):

| The hand meets | What happens |
| --- | --- |
| A wall | The wizard is shoved straight away from it at `sidewaysShove` (9 boxes a second). |
| A ceiling | They are shoved down at `downwardShove` (6). |
| The floor | Nothing upward. Slapped diagonally, it shoves them sideways; straight down, nothing. |
| `Furniture` | Every piece in the way is knocked flying, at `furnitureKnock` for a Mass of 1. |
| Anything else | Hazards, wind, Bouncy Trash and pickups are passed straight through. |
| Nothing in reach | The hand swats the air at full stretch. Nothing moves. |

The shove only ever **tops up** speed: a wizard already flying away from the wall faster than the
shove keeps their speed. The stick is locked for `steeringLockout` (0.2 s) after it, so a slap
carries the wizard instead of being steered straight back out of. It works stood up, in the air and
while tumbling, where it steers the tumble but never ends it: getting back up is Falling Star's
job. Not on the Staff, on a Mage Hand vine, or during a Falling Star dive.

**Every press slaps.** `CanCast` only checks that the wizard is free to, so the hand always shows,
even when it only swats the air. Only walls, floors and ceilings on the **Ground** layer push the
wizard; the sweep is a skin smaller than the hitbox so the floor they stand on never counts as a
wall.

**The hand** is `Star Slap Hand.prefab`, spawned where the slap lands, turned to face it, and
removed after `handSeconds` (0.35 s):

```
Star Slap Hand      root = where the hand lands
├── Look            the hand: Art/Placeholders/Star Slap Hand.png until you draw your own
└── Sparkles        a small copy of Star Sparkles, the burst on impact
```

Draw your hand **pointing right**, and set the `Look` so the fingertips sit on the root (the
placeholder is moved a quarter box left for that). Left slaps mirror the whole prefab, so the thumb
stays on top. Keep its sorting order above the wizard's (6); the placeholder uses 8.

### Furniture

`Assets/Prefabs/Furniture` holds a prefab per sprite on `Art/Sprites/Muebles.png`: Teapot,
Armchair, Side Table, Bookcase, Chest, Crystal Ball, Sofa, Gramophone and Lantern. **Furniture is
decoration**: the wizard walks straight through it, and nothing stands on it. A Star Slap is the only
thing that moves it.

```
Armchair        Rigidbody 2D, Box Collider 2D, Furniture    root on the floor
└── Art         Sprite Renderer, your sprite at native size
```

- **The Rigidbody 2D and the Box Collider 2D** exclude the **Player** and **Hazard** layers (Layer
  Overrides > Exclude Layers, with the collider's Layer Override Priority at 1 so it wins). That is
  what lets the wizard pass through, and keeps furniture out of hazards and wind. It still lands on
  floors and bumps into walls and other furniture.
- **How far a slap sends it is the Rigidbody 2D's Mass**: 1 for the teapot, 4 for the bookcase.
- **`Furniture`** adds a `hop` up and a `spin` the way it was slapped, so pieces tumble instead of
  sliding. Tick **Freeze Rotation** on the Rigidbody 2D to keep a piece upright.
- **A new piece:** duplicate one, drop your sprite on its `Art`, and set the collider to the
  sprite's size with its bottom on the root.

Nothing about furniture is saved: a level reload puts every piece back where you placed it.

## Fling: aim, then tumble

**Holding the button aims; letting go throws.** There is no wind-up: how hard you are thrown is
set per rank (`Tier.speed`, 9 and then 11 boxes a second), and holding only turns the arrow.

**At rank 3 it works while tumbling** (`Tier.canFlingWhileTumbling`), even in mid-air: aim while
you tumble, let go, and the tumble starts over along the new throw. That redirects a tumble but
never saves it. `TryFling` only restarts the fall count when the wizard was standing; from a
tumble the fall is still counted from the highest point before it, so a long drop still hurts.
Below rank 3 the HUD slot greys out while tumbling.

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

## Telekinesis: doors and sleeping hazards

Telekinesis is the wizard holding still the space around them, so it helps exploring more than
surviving. One press pushes magic out in a circle of `reach` boxes round the wizard (4 at every
rank; the upgrade only makes things stay stopped longer), and everything inside answers:

- **A `MagicDoor` opens for good.** Every collider on it and on its shown children switches off,
  its Closed Look hides and its Open Look shows. The Closed Look slot must hold the object that
  holds all of the shut art, or part of the shut door stays on screen. The level
  reloads on death, so there is nothing to close it again.
- **Anything `ISleepable` with Can Sleep ticked falls asleep** for `sleepSeconds` (3, then 5 at
  rank 2), and its `Asleep Look` shows on top of its own art: each of these prefabs ships with a
  copy of Star Sparkles there, so stars hang around it while it sleeps.

| Sleeps | While asleep |
| --- | --- |
| `MovingPlatform` | Stops where it is, then carries on along its path. |
| Rakes, Bouncy Trash (`Hazard`s) | Cannot catch the wizard. |
| `SteamVent` | Held quiet. It wakes at the start of its quiet time, so the warning always comes first. |
| `WindTrap` | Held shut. It wakes shut and winds up again, so the warning always comes first. |
| `PressurePlate` | Jammed up: it does not count or throw, so the wizard can stand on it. |

Things that just lie there have **Can Sleep** unticked, because stopping them makes no sense: the
wet floor sign, stairs, broken glass and wax drops. Untick it as well on anything the level needs
to keep working, like a Bouncy Trash that is the only way across a gap.

**A new thing that should sleep** implements `ISleepable` (`World/ISleepable.cs`): `CanSleepNow`
and `TrySleep(seconds)`. Telekinesis finds it on the collider it touches or any parent of it, so
it needs no other change. Every `Hazard` already does.

The press does nothing, and greys out on the HUD, when there is no shut door or awake sleeper in
reach, so the 5 second `cooldown` is never spent on nothing.

`Hazard.TrySleep` pushes the hazard's own re-arm timer out to the end of the sleep and never pulls
it in, so a steam vent that has just burned the wizard keeps its own wait if that is longer.

**Plain wind does not sleep.** A `WindZone2D` is a `Zone`, and Glide wants it. Only a `WindTrap`,
a wind on a timer, can be held shut.

## The grid line is not the floor

`mainlev_build.png` is sliced **34×35 px on a 32 px grid**, so every tile is drawn — and collides
— about a pixel proud of its cell. Read it straight off the composite collider in `1-1`: the
outline sits on `x.03125`, not on whole numbers.

That is not a rounding error to ignore. **It is the surface everything else has to line up with.**
Anything placed to sit level with a floor takes its height from the floor itself, never from a grid
line: `Movement.Footing.y`, the wizard's soles, *is* the surface they are standing on, and is the
height to use. Re-slice the sheet to 32×32 one day and anything built that way keeps working,
having never been told what the old number was.

**A block flush with the platform is the worst case, not the best.** Two box colliders whose top
faces are exactly level still meet at a vertical face, and gravity sinks the wizard about a fifth
of a pixel into the floor between solver steps — enough that the face catches them and they stop
dead at the block instead of walking onto it. The tilemap never shows this because the composite
merges its tiles and there are no internal faces at all.

So `Stone Wall`'s collider is **0.9 square with a `0.05` edge radius**: one box on the outside,
same flush top, but with rounded corners, so a wizard a fraction low rides up over the seam
instead of walking into it. Keep both numbers if you resize it — `size + 2 × edgeRadius` is what
has to come to one box.

Do **not** reach for a custom `physicsShape` on the tiles instead. `spriteMeshType` is Tight and
`physicsShape` is empty, so the slopes and half-tiles get outlines that follow their own art;
squaring all 212 of them off would wall the level in. The two stair tiles are the one exception:
they get a straight triangle (see *Stairs*), because their drawn steps are not a shape to walk on.

## Where the feet are

`Movement.Footing` is the one answer to *where are the wizard's soles*: it asks the collider itself,
`bounds.center.x` and `bounds.min.y`. The ground **probe** is not that answer. It hangs below the
boots on purpose, and it drifts further the moment the collider is resized without
`FitGroundCheckTo` being run again — it once sat almost a tenth of a box under the wizard.

`FitGroundCheckTo` measures from the collider's bottom and its own middle rather than from half
its height about the transform, so a collider carrying an offset — which is what Unity's
fit-to-sprite button writes — no longer bakes that drift in the moment someone hits Reset. The
PLAYER in `1-1` has been refitted to its collider: `groundCheckOffset` went from
`(0, -0.596875)` to `(-0.050636888, -0.5525605)`, and `groundCheckSize.x` from `0.703125` to
`0.64969389`.

Both probes hang from one `ProbeOrigin`. The ground check reads it, the ledge check reads it,
and `TryFindLedgeEdge` reports its lip relative to it — otherwise a collider with an x offset of
its own leaves the two disagreeing about which foot is over the drop.

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
`Visual`, so it faces the right way), **Falling Star Trail** (anywhere under the wizard), and an
object with a **Fling Arrow** component (a direct child of the wizard, art pointing right, Danger
Look optional). Missing one just means that effect does not show.

**Placeholder art ships in the prefabs**, so every effect shows something until the real art
arrives.

**Star Sparkles is the default for anything without art yet.** `Assets/Prefabs/Star Sparkles.prefab`
is the wizard's Impact Stars turned into a gentle loop: your star sprite (`Art/Sprites/VFX.png`),
circling slowly around a ring. Its emitter is in **Shape** scaling mode, so scaling the object
stretches the ring around whatever it sits on while the stars stay the same size. Every copy is a
nested prefab, so editing Star Sparkles itself changes them all; a copy can still override its own
numbers (the slap and the trail do). It is used for:

- **Hand Hold:** the `Glow Look`, stars around the knot while Mage Hand can reach it.
- **Every hazard:** the `Asleep Look`, stars around it while Telekinesis holds it asleep.
- **Star Slap Hand:** the `Sparkles`, a small burst where the hand lands.
- **PLAYER:** the `Falling Star Trail`, streaming behind the dive.

The other placeholders are plain shapes in `Assets/Art/Placeholders`:

- **PLAYER:** a `Glide Canopy` under `Visual`, and a `Fling Arrow` with an `Arrow Art` child and a red
  `Danger Look`.
- **Stone Wall:** a stone block on its `Body`.

To replace one, open the prefab and put your own object in its place under the same name (or drag
your sprite onto that object's Sprite Renderer).

The only things a script still moves are the ones where the movement *is* the mechanic: the
wizard's `Visual` is flipped to face their way and spun when they tumble (and pointed head first
along a Falling Star dive), the staff sprite is flipped, the vine's rope is stretched as it unrolls
and leans with the swing, and the fling arrow is pointed along the aim.

## Getting to the loadout, and making it stick

Three separate faults made "I cannot swap abilities" true, and all three had to go.

**The screen could not target a slot.** Every equip path went through `Progress.FirstEmptySlot()`,
and the rail along the top was plain images with no `Button` on them — a read-out, not a
control. Swapping Q and R was not expressible. The verb now is **pick a spell, then press the
button you want it on** (or click the rail cell). Those four actions already exist and are already
enabled, and the glyph on the rail is the same one the HUD prints under that slot in play — so the
binding teaches the binding.

**`Progress.Equip` was a move-into, not a swap.** It cleared the key from wherever it was and
overwrote the target, so dropping one spell onto another lost the second one silently.
`Progress.Place` trades them instead. `Equip` keeps its meaning for the paths that want it.

**And you could barely reach the screen.** It used to open only from a rest site or the death
screen, both of which ended the run first. Now **Play** opens it from the main menu, **Start
again** on the death message opens it the same way, and walking into a **shop stop** opens it
mid-level. The **Spellbook** button (`UI/Loadout`, Tab) also opens it mid-level from anywhere, but
that one is a **debug tool** for testing loadouts, not part of the game. The button's door is the
nested `SkillScreen.Door`, installed once when the game starts; the shop stop and the button both
go through `SkillScreen.OpenInLevel`.

## Prefabs

**Wooden Platform** (`Assets/Prefabs/Wooden Platform.prefab`) is solid planks on the Ground layer
with a `StaffPlatform` on them. **You walk on it, and the staff takes you straight down through it
from wherever you stand.**

```
Wooden Platform     Box Collider 2D (the planks, solid, on Ground) and StaffPlatform
└── Planks          your Wood Platform sprite, native size
```

A tap of the staff button locks the staff looking down, as it does before a ledge. While it is
locked and the wizard stands on a wooden platform, the staff is planted straight down through the
planks **right where the wizard stands**, and they climb down it as they would off a ledge: push
down to slide, hold down at the bottom to let go, push up at the top to step back onto the planks.
Lock it first and walk onto a platform, or lock it while stood on one: it goes the moment both are
true, and going down unlocks the staff again. There is no spot to find - anywhere on the planks
works, on one piece or a long row.

**How it is wired.** Each physics step `PlayerLogic.GoDownWhileLookingDown` checks the lock and
tries `TryGoDownThroughPlatform` first, then `TryTakeLedge` (the ordinary ledge climb-down).
`Movement.TryFindPlatformUnderFeet` looks at what is under the wizard's feet with the same probe the
ground check uses and takes every `StaffPlatform` there. `Movement.FitOverThePlanks` keeps the
wizard where they stand, only nudging them in until their whole body is over those planks - so a
wizard hanging half off the end, or stood against a wall at the end of the planks, goes down beside
the wall instead of inside it. `Movement.FindFloorBelowPlanks` then sweeps a box the size of the
wizard straight down from where they stand, **skipping the planks they are stood on**, to find the
first real floor underneath. `Staff.Pole.TryPlantThroughFloor` plants the pole with its top flush
with the planks and lets the wizard slide as far as the staff reaches or that floor, whichever comes
first. The wizard is kinematic on the pole and slides through the solid planks, and stepping off at
the top puts them back on solid wood.

**It never leaves the wizard stuck.** The pole is only planted if the wizard can slide far enough to
be completely under the planks. If there is not that much room (less than about a box of open space
under the platform), the staff stops looking down and the Console says how much room there was. The
old version measured from the bottom of the plank collider's bounds; with the rounded corners that
could start the measurement inside the wood, which planted the staff with almost no reach and left
the wizard hanging on top of the planks until they climbed back up.

**The planks have rounded corners**, for the same reason as `Stone Wall` (see *The grid line is
not the floor*): the collider is **0.9 × 0.0875 with a 0.05 edge radius**, which still comes to one
box wide and 0.1875 tall with its top flush where it was. Two plank pieces side by side, or a plank
beside a floor tile, otherwise meet at a sharp vertical face the wizard walks into and stops dead.
Keep `size + 2 × edgeRadius` at one box if you resize it.

**A platform the staff will not go down through** has something solid right under its planks.
Check the level's Composite Collider 2D first: its **Generation Type must be Synchronous**. On
Manual, the collision is only rebuilt when someone presses Regenerate Geometry, so tiles that were
painted and later erased still collide, invisibly. That is what blocked the staff under two
platforms in `1-1`.

Everything below is a plain object you drag in. All the art is Unity's built-in Square tinted a
flat colour, so each one is visible the moment it lands and is waiting for a sprite of yours.

| Prefab | What it does | Layer |
| --- | --- | --- |
| `Knowledge` | Knowledge, the currency (a book). Goes straight into the purse. Taken until the wizard dies. | Default |
| `Heart Upgrade` | +1 max HP. Taken until the wizard dies, like everything else. | Default |
| `Timed Reward` | A Knowledge and a Heart Upgrade to grab within 60 seconds of the level starting, under a white timer counting up. At `1:00` the timer stops and turns red, and they fade. | Default |
| `Wet Floor Sign` | `TripHazard`: trips a runner. `minimumSpeed` 3, so a walk is safe. | Hazard |
| `Rake`, `Rake 1` | `TripHazard` aimed Left or Right: trips you BACKWARDS, the way you came. | Hazard |
| `Ice` | Ground that does not hold you. Nothing else changes. | Hazard |
| `Stairs` | `TripHazard`: lay it over a staircase. Take them fast and you tumble down them. | Hazard |
| `Bouncy Trash` | `BouncyTrash`: bounces you three boxes and sends you tumbling. | Hazard |
| `Wind` | Pushes you the way its arrows point. Rotate it to aim it. | Hazard |
| `Wind Trap` | The same push, on a timer: a wall mouth that opens and blows left. Shut, Warning and Blowing Looks. It shoves, it never hurts. | Hazard |
| `Hand Hold` | The knot Mage Hand catches. | Default |
| `Magic Door` | `MagicDoor`: a solid door, one box wide and two tall, that Telekinesis opens for good. Put your art in its Closed Look and Open Look children. | **Ground** |
| `Stone Wall` | A one-box stone block to stand on. It was the old Wall Growth spell's block; now it is a plain level piece. | **Ground** |
| `Star Slap Hand` | The hand Star Slap shows where it lands. Already wired into the spell; not for placing by hand. | Default |
| `Furniture/…` | Decoration a Star Slap knocks flying: the wizard walks through it. See *Star Slap > Furniture*. | Default |
| `Star Sparkles` | Stars hanging around whatever it sits on. The stand-in for any effect without art yet; scale it to fit. | Default |
| `Level Exit` | The bottom. Marks the level completed and opens the Level Select. | Default |

The steampunk set for the second world lives in `Assets/Prefabs/Mid Level Hazards`, and is set up
by hand like every other prefab. `Steam Vent`, `Pressure Plate` and `Wind Trap` already carry your
frames and animations (see [Animations > Hazards](#hazards-1)). Any coloured box left in the
others is a child called `Stand-in`: put your art beside it and delete it.

| Prefab | What it does | Layer |
| --- | --- | --- |
| `Steam Vent` | The gas trap: a floor vent that sits shut, opens over its warning, and burns anyone on it while fully open. | Hazard |
| `Wax Candle` | Drips a `Wax Drop` on a rhythm. Select it to see where drops land. | Default |
| `Wax Drop` | Hangs as a warning, falls, burns whoever it lands on. | Hazard |
| `Moving Platform` | Slides along `travel` and back, carrying the wizard. | **Ground** |
| `Pressure Plate` | Stand on it for a second and it throws you up. It sinks while it counts and springs up as it throws. | Hazard |
| `Broken Glass` | A plain `Hazard`: cuts you every second you are on it, unless you are walking across it. Standing still on it hurts, walk mode or not. | Hazard |

The ones on **Ground** are there because the wizard has to be able to stand on them, and the ground
check only looks at that layer. The ones on **Hazard** are triggers you pass straight through.
`Hazard.passThrough` sets that on Awake, so ticking the box fixes one already placed, and wind and
ice are triggers in their prefabs.

`Level Exit` marks the level completed (`Progress.CompleteLevel`) and loads the `Level Select`
scene.

## Level Select

The tower is **three worlds of three levels each**, and each level is a scene named after its
number:

| World | Name on the map | Scenes |
| --- | --- | --- |
| 1, the top of the tower | `levels.top` | `1-1`, `1-2`, `1-3` |
| 2, the middle | `levels.mid` | `2-1`, `2-2`, `2-3` |
| 3, the bottom | `levels.bottom` | `3-1`, `3-2`, `3-3` |

`Assets/Scenes/Level Select.unity` is set up by hand. It is a black screen and a `Tower Map` image
for the map art. The map is one column reading down the tower, with twelve stops 90 px apart. Each
world's name is a plain label, and each level is a button named after its scene, whose label
carries a `LocalizedText` (`levels.level1` to `levels.level3`).

`LevelSelectMenu` sits on the canvas with a list of **Worlds**, top of the tower first. A world holds
its name label and its levels, and a level holds the scene it loads and its button. `1-1` is always
open, and every other level opens once the one before it is completed, so finishing `1-3` opens
`2-1`. `LevelExit` completes whichever scene is playing. Completed levels are saved, so they stay
open across launches until the wizard dies or the save is reset. A level whose scene is missing
from Build Settings stays locked and says so in the console. Selection starts on the deepest open
level.

**The dotted line** runs through the Worlds list in order: a world's name, its levels, then the next
world's name. It is made of copies of `Dot`, a switched-off child of `Tower Map`, placed when the
screen opens. Restyle `Dot` (sprite, size, colour) and every dot follows. Move a level or a name on
the map and the line follows it. `Dot Spacing` sets the gap, and no dot lands on top of a label or
a button, so a spacing that is large for the room between stops can leave very few dots. At 15 each
gap gets three. The line only shows in Play mode, because the copies do not exist before then.

**Travelling.** Picking a level lays copies of `Travel Dot` along the line, one at a time, from the
top of the tower down to that level, and then the level loads. Restyle `Travel Dot` the same way as
`Dot`, and set the pace with `Seconds Per Dot`. A level further down is a longer journey: `3-3` takes
about a second.

**Cheats.** Press **M** anywhere to turn cheats on: every level on this screen opens, whatever has
been completed, and **L** jumps here from any scene. It changes nothing in the save, so turning
cheats off locks the levels again.

`2-1` and `3-1` started as empty levels: the wizard, the camera, the light, the HUD, the menus and
the tile grid, a strip of floor and one Level Exit. The other new levels are copies of that same
empty level, so every level can be finished from the start. To add one, duplicate a scene in the
Project window, add it to File > Build Settings, and add it to a world in the Worlds list.


## Saving

There is no checkpoint and no respawn: every level starts the wizard where the scene places them,
and dying shows a message and starts the whole game again with nothing (see *Knowledge, hearts
and dying*).

**Everything is written the moment it changes.** Picking up Knowledge or a heart, buying,
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
stale save so well that a long-forgotten Glide once reappeared.
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

**The feet probe stays inside the body.** `SenseGround` never lets the probe be wider than
`groundCheckWidthFactor` of the wizard's collider, and centres it under the collider, whatever
`groundCheckSize` says. The saved probe had been sized for an older, wider collider, so it stuck
out past the body; a wizard dropping off the staff slid down the wall touching it, counted as
standing the whole way, and landed with a fall of nearly zero — no fall damage at all.

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

**Better staves let you drop further.** Each staff's `Pole` has a **Safe Drop Bonus**: extra
boxes a drop off the bottom of that staff can fall before it hurts, on top of the wizard's usual
safe fall. Level 1 has 0; Levels 2 and 3 have 1, so they take the wizard one tile further down
unhurt. `DropFromStaff` hands it to `Movement.BeginFallFrom`, and the next real landing uses it up.

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
├── Pickup                collectables: Gives Knowledge or a Heart
├── ShopStop              opens the skill screen mid-level
└── LevelExit             the way out
```

**Lift beats a fall.** A hazard that throws the wizard upward never lets a fall eat the throw.
`PlayerLogic.Lift` (rakes, through `TripHazard.extraLift`) drops any downward speed before
adding the lift, and still works on a wizard who is already tumbling. `BouncyTrash` with **Keeps
The Fall** on bounces the wizard at least as high as they fell from, so going from one bouncy hat
into another never loses height.

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

The steps themselves are **tilemap**, not the hazard. The two stair tiles (`Tilemap-Tutorial-W1_31`
rising, `_32` falling) are drawn as 3-pixel steps, but their **hitbox is a clean 45 degree
triangle**, set as a custom Physics Shape in the Sprite Editor. Following the drawn steps gave the
collider ten tiny ledges per tile, which the step assist had to hop one by one; a straight face hands
the job to the ramp handling instead, so the wizard runs up at full speed. A stacked flight of them
meets corner to corner with no lip. The feet land in the notches of the drawn steps.

The `Stairs` prefab is a `TripHazard` trigger you lay over a staircase, saying *this stretch of floor
is a staircase*, and all it does is `TryTrip()` above `minimumSpeed`. Its shape covers one two-tile
hump (rise then fall) plus half a box above the treads, where the wizard's body is. Put its root at
the bottom of the hump, on the line between the two tiles. For a longer flight, edit the collider's
points to follow it.

Walk mode (`ignoresWalking`) is always safe on stairs. So is a staircase Telekinesis has put to
sleep: run it while the stars are still out.

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

**Health is shown on the HUD, not on the wizard.** The top-left `Hearts` row holds the hat
(`UI Health.png`) and then one blip (`Health Icon.png`) per heart the wizard can have:

```
HUD
└── Hearts      Horizontal Layout Group, top left
    ├── Hat     the hat, 32 px art drawn at 4x
    └── Blip    TEMPLATE, switched off - copied once per heart
```

`PlayerHud.ShowHealth` builds as many blips as the wizard's maximum health, and rebuilds them when
a Heart Upgrade raises it. Blips for hearts still held show the sprite as drawn, and blips for hearts
already lost take the HUD's **Lost Tint**. Restyle the `Blip` template and every blip follows.

The wizard starts every run with **Starting Health** (3), set on PLAYER. Heart Upgrades add to it,
up to **Max Bonus Hearts** under Logic > Health. The wizard's own sprite never changes with health:
the old Health Look system, one look per health level, is gone.

`PlayerLogic.reportHits` (on by default, editor only) prints every hit to the console - *"Broken
Glass took 1 heart(s) from the wizard - 2 left."* `PlayerLogic.Hurt` takes that cause as its second
argument: a hazard passes its own name, a fall passes how far it was.

**The `Any State → Take Damage` transition is muted until its clip has frames.** An empty clip still
lasts a second, and nothing changes the sprite during it, so the wizard would freeze on whatever
frame was showing. Once `TakeDamage.anim` has frames, open `Player.controller`, select its
transition from Any State and untick **Mute**. The `Hat Change` state is left over from the old
system and nothing triggers it any more, so it can be deleted.

## Animations

`Player.controller` is set up by hand: its states, transitions and clips are yours, and no script
ever writes to a clip - the frames, timing and looping in a clip are exactly what you put there. To
fill a clip: select a Health Look in the PLAYER prefab, open Window > Animation > Animation, pick
the clip from the drop-down, and drag the sliced frames of a sprite sheet onto the timeline. Set
Samples (the frame rate) in the same window.

### Hazards

A hazard with frames has **one Look child per state**, and its script only switches them on and
off. A Look that animates has its own Animator with a one-state controller, and a clip that plays
**once** and holds its last frame. Switching the Look on starts the clip over, so the animation
always begins when the state does, and a clip that runs a little short just waits on its last
frame. The clips and controllers live in `Assets/Animations/Hazards`.

| Prefab | Looks, in the order they show | Frames | Clip |
| --- | --- | --- | --- |
| `Pressure Plate` | Idle → Pressed → Launched → Idle | `PressurePlate_0` → `_1`, `_2` → `_3` | `Pressure Plate Pressed`: 1 s, as long as Seconds To Launch |
| `Steam Vent` | Quiet → Warning → Steam → Quiet | `Gas trap` frame 5 (shut) → 4, 3, 2 → frame 1 (open), counting from the left | `Steam Vent Warning`: 0.6 s, as long as Warning Seconds |
| `Wind Trap` | Shut → Warning → Blowing → Shut | `SteamValve_0` → `_1`, `_2` → `_3` | `Wind Trap Warning`: 0.6 s, as long as Shutter Travel |

The steam vent burns only while the Steam Look shows, which is the vent fully open.

**Every piece of hazard art is at its own size.** Transform scale 1, Draw Mode Simple, so a sprite
is exactly as many boxes as it has pixels divided by 32. A 32×16 frame is 1 × 0.5 boxes. Never
size art with the Transform or with a Sliced Size. Each prefab's root sits **on the floor**, at the
bottom middle of the art: the Looks are lifted by half the frame height (0.25 for a 16 px frame,
0.5 for a 32 px one), and the trigger collider is the frame's rectangle. The one exception is
the Wind Trap's collider, which is where the wind reaches: one box tall (the frame's height) and
three boxes long, starting at the mouth. The mouth faces left, so `push` and `kick` both point
left.

To change a timing, change the number on the script and the clip's length together. To add a
frame, open the Look's clip in the Animation window and drag the frame onto the timeline.

## HUD

**The HUD is a prefab, `Assets/Prefabs/HUD.prefab`, and every level holds one instance of it.**
Open the prefab to change the spell bar, the health row or the scrap count, and all nine levels
follow. A new level only needs the prefab dragged in. It is an ordinary screen-space `Canvas` you
can open up and restyle:

```
HUD              Canvas (Overlay, order 10) + CanvasScaler + PlayerHud
├── Spell Bar    bottom-left, HorizontalLayoutGroup + ContentSizeFitter
│   └── Spell Slot   TEMPLATE, inactive - Image + HudSlot
│       ├── Icon         the spell's own icon, 40 x 40: a 10 pixel icon at 4x, like the ring
│       ├── Charge       Image, Filled/Radial360 - the running and cooling wipe
│       ├── Ring         your ring art
│       ├── Button       ButtonPrompt; its text is only the fallback
│       │   └── Icon         the button's picture
│       └── Charges      one blip per cast left this level, above the ring
│           └── Charge Blip  TEMPLATE: the empty blip, with its Full child on top
└── Knowledge    top-right, HorizontalLayoutGroup + ContentSizeFitter
    ├── Icon         the Knowledge pickup's book (Upgrade.png), 128 x 128: 32 pixels at 4x
    └── Digit        TEMPLATE: children 0 to 9, the Timer Digits sprites at 4x (32 x 44)
```

**Charges are blips.** A spell with `Uses Per Level` (Falling Star) shows one `Charge
Blip` per cast it can make this level; `Full` hides on each blip as the casts are spent. Spells with
no limit show none. With **cheats on** (`M`) every spell keeps all its charges: `Slot.UsesLeft`
answers the maximum and casting spends nothing.

Slots are **copies of the template**, made at runtime, one per spell. Restyle the template —
sprite, size, colour, add a border — and every slot follows. `PlayerHud` only ever fills them in;
it never builds layout. The icon each slot shows is the spell's own `icon`.

The **Knowledge** counter shows the one purse: the book icon, then the number drawn with the same
digit art as the timed reward clock. It has no words, so it reads the same in every language. The
digits are a `DigitRow` (in `Ui.cs`, next to `BlipRow`): `Digit` is copied once per digit the
number needs and the script only switches its `0` to `9` children on and off, so redraw the
digits by swapping those sprites. Move or restyle the whole row in the HUD prefab, or clear
`knowledgeCounter` to show none. In code the purse is still `Progress.Scrap`.

Set `CanvasScaler.referencePixelsPerUnit` to **32**, not the default 100, or every icon comes out
at a third of its size.

It finds the wizard through the singleton and re-checks every frame, because every scene load
destroys them and builds a new one — anything that had subscribed would be holding a destroyed
object.

The button under each spell follows **whichever device is actually in use**: the E key on a
keyboard, the left face button on a pad. `Core.Controls` watches only the actions this game asked
for, which is what stops a mouse twitch flipping the HUD back to keyboard pictures mid gamepad
play.

### Button prompts are pictures

Everywhere the game names a button - under each spell, on the skill screen's slots and cards, both
columns of the controls sheet - it shows a **picture**, through a `ButtonPrompt`. The pictures are
all in one table, `Assets/Resources/Button Icons.asset`: one row per key or pad button, the
control on the left (`<Keyboard>/q`, `<Gamepad>/leftShoulder`), the sprite on the right. **Swap a
sprite there and every prompt in the game follows.** The starter pictures are in
`Assets/Art/Sprites/Icons/Buttons`.

- A combined binding is named `composite:` and its name: the WASD keys are `composite:WASD`.
- The menu buttons are bound as "whatever the device calls Submit", so their rows name the device
  first: `Keyboard&Mouse */{Submit}`, `Gamepad */{Cancel}`.
- A button with no row shows its name as text instead (the prompt's `Fallback` label), so a new
  binding never shows a blank. Add a row to give it a picture.

To put a prompt anywhere else, for example a "press this to open the spellbook" sign in a level:
add a `ButtonPrompt` to a UI object, give it an `Image` for `Icon`, and type the action in
`Action` (`UI/Loadout`). `Device` picks the keyboard, the controller, or whichever is in use.

The face buttons are drawn as the four-dot diamond with one dot lit, so the same picture is right on
an Xbox, PlayStation or Switch pad. The shoulders and triggers say LB, RB, LT and RT.

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

### Menus on a controller

Each button in the Pause Menu, the Settings Panel, the Main Menu's title page and the three screen
prefabs has a `Highlight` child: a frame that is part of the prefab,
so it is moved, resized or redrawn there like any other UI element. A `SelectedLook` on the
button switches it on while that button is selected and a controller is in use (tick **Show With
Keyboard** to show it always). The stand-in art is `Art/Placeholders/Menu Highlight.png`; keep
the Image **Sliced** so its corners stay crisp. To give another menu's button the same frame,
copy a `Highlight` child into it, add `SelectedLook` to the button, and drag the child into
**Look**.

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

**Background.** Every level's `Grid` has a second tilemap called `Background`, for tiles that are only
scenery: no collider, sorting layer `Default`, Order in Layer **-10**. Pick it under Active Tilemap in
the Tile Palette before painting. The lowest order on any prefab is -3, so everything draws in front
of it. A plain sprite behind it goes lower still, like the `BG` object in `1-1` at -11.

Keep background art on `Default`. The other two sorting layers both go wrong: the light does not
target `Background`, so anything on it renders pure black, and `BG` is lit but sits after `Default`,
so anything on it draws in front of the whole game.

## Importing sprites

Any picture in `Assets/Art/Sprites` (or a folder inside it) is imported as pixel art by
`PixelArtImporter`: Sprite, 32 Pixels Per Unit, Full Rect, Extrude Edges 1, Center pivot, Generate
Physics Shape on, Clamp, Point (no filter), Max Size 2048, Mitchell, Automatic, no compression.

- These are set on every import, so changing one of them in the Inspector puts it straight back.
  To change the rule, change the constants or the lines in `PixelArtImporter`.
- **Sprite Mode is the exception.** A new picture starts as Single, and after that the importer
  never touches it. Switch it to Multiple and slice it, and it stays that way.
- To bring old sprites in line, right-click `Assets/Art/Sprites` and pick **Reimport**.

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
