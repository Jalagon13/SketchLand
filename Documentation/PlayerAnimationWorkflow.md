# Player gameplay and animation workflow

## Start here

The Player prefab now has two entry points:

- **PlayerActionController** owns swing timing, impact, charge, ammunition, projectile creation, and gameplay aim.
- **PlayerAnimationPresenter** reads that state and owns locomotion parameters, bow/swing poses, visual clips, and action sounds.

Disabling the presenter does not stop attacks or movement. There are no animation events or tween callbacks applying damage or creating arrows.

For code review, read these in order:

1. `Assets/Game/Scripts/Entity/PlayerActionController.cs`
2. `Assets/Game/Scripts/Entity/PlayerActionClock.cs`
3. `Assets/Game/Scripts/Entity/Animation/PlayerAnimationPresenter.cs`
4. `Assets/Game/Scripts/Entity/Animation/PlayerAnimationProfile.cs`

`BowAnimationPose`, `SwingAnimationPose`, and `SwingBodyPose` are plain serializable helpers inside the presenter, not components added per animation. `PlayerHandGripIK` and `PlayerHeldItemDisplay` remain reusable rig services. The old arm/ranged/bow MonoBehaviours have been removed.

## Change the existing bow or swing

Open `Assets/Game/Content/Art/Animations/PlayerActions`.

- **Swing_UpperBody**: default swing. Edit body/time curves, intensity, torso/head emphasis, free-hand travel, and visual phase boundaries.
- **Bow_UpperBody**: default bow. Edit draw curve, grip positions, raising/release/return times, and intensity.
- **Swing_OriginalBody**: restores the original hip-assisted pose style. Assign it to the presenter's **Default Swing** to compare. The original source remains in git history; this is the new renderer with hip contribution enabled, not a second gameplay implementation.

Duplicate a profile for a variant. On the Player prefab's **Player Animation Presenter**, add an **Item Animations** entry with the item and matching profile. Remove that entry to use the default again. No new component or handler is needed.

The bow and swing remain procedural performances, so their controls are curves and positions in these assets. They have not been baked into ordinary `.anim` clips. Ordinary gestures/reactions can use the clip workflow below.

Upper Body Only keeps procedural action contributions off the hips and legs. Torso, head, arms and hand targets remain expressive; locomotion owns the pelvis and feet. Disabling it allows the original body-sway contribution. Profile settings never change attack duration or damage.

**Gameplay tuning stays on items:** bow charge/minimum charge/projectile speed and swing rate/reach/damage/Impact Fraction. Swing impact defaults to 0.47 of its duration. If you change visual timing, align the visible strike with the gameplay impact; the visual editor deliberately cannot move the damage moment.

## Add a regular animation clip

1. Author a clip against **StickFigureRig**, the object containing the Animator. Use Unity's Animation window. Paths must be relative to that rig, not the Player root.
2. Animate the hand IK controls (`Left_Hand_LimbSolver2D`, `Right_Hand_LimbSolver2D`) for arm poses. Directly keying arm bones can be overwritten by the final IK solve.
3. Create **SketchLand → Animation → Player Animation** from the Project Create menu. Set Style to **Clip**, assign the clip, and choose **UpperBody.mask** for an action that can overlap walking. A null mask allows full-body animation.
4. Set speed, blend in/out, and optionally Additive. Additive clips must be authored as suitable offsets/reference poses.
5. Add a **Cue Animations** entry to the presenter: a gameplay cue name and this profile. Built-in cues are `Damaged`, `Airborne`, and `Landed`.
6. For a new gameplay event, report its semantic name from the gameplay code: `player.Actions.ReportCue("Wave");`. Gameplay never references a clip or presenter. The supplied `Wave_Example` binding demonstrates this path; it has no new keyboard binding.

`Wave_Example.anim` is a simple editable hand-control example, not a new gameplay ability. Duplicate it to try the workflow.

The existing locomotion controller/clips still handle idle, walk, sprint and airborne motion. A small Playables layer runs cue clips above that controller; you do not add an Animator state for each cue clip. Inspector changes to the controller's parameters are not the runtime source of truth: the presenter supplies them from gameplay.

Current overlap policy is deliberately simple: one cue clip at a time; a new cue replaces the previous one. Combat takes priority and suppresses cue clips while aiming/swinging. A clip plays once and fades out even if its source asset loops. Full-body clips change appearance only; a mechanic that blocks movement must enforce that in gameplay. Missing damage-cue bindings fall back to the existing damaged state.

## Projectile launch point

Select the Player prefab's **Player Action Controller**. **Projectile Origin Offset** defaults to `(0, 0.4)` in world units relative to the player root, near the shoulders/below the head. The selected-object cyan gizmo shows it.

Origin = character position + offset. Direction = normalized vector from that origin to the aim target. Arms use the same direction, but their animated position never influences a shot. The launch point does not inherit skeleton rotation, scale or bobbing. The melee origin remains separately configured at `(0, 0.55)` to preserve the old strike-zone location.

A shot is rejected if its spawn bounds overlap solid terrain. Projectiles already ignore their shooter. This controller is the common location for future player projectile weapons to obtain their origin and aim.

## Extending the system

Use profiles for variations of supported motions and clips for ordinary new performances. A genuinely new procedural capability may need another plain pose helper; it should not need another MonoBehaviour on the Player. Preserve one-way ownership: presentation may observe gameplay, but gameplay must not query a bone, clip time, or visual completion callback to decide mechanics.

The action clock and origin are now independent of rendering, which removes a multiplayer dependency. This change does **not** add server validation, action-state replication, prediction, or remote-player aim replication. Those remain future networking work; the existing local/host gameplay and projectile networking behavior is retained.

## Verification

Verified in Unity: clock threshold crossings including skipped frames, cancellation, rig references, mask paths, bow shots and swing impacts with presentation disabled, fixed spawn position despite moving an animated bone, one-arrow consumption, duplicate-release prevention, equipment-change cancellation, held-click weapon switching, inventory toggling, and upper-body clip/procedural masking. Walking, sprinting, and jumping were verified against the presenter; movement also passed with both presenter and Animator disabled. The saved Wave example raised its hand control and returned to locomotion. Pose captures were reviewed after the skinned sprite update. Unity compiled successfully and reported no new runtime exceptions during these checks.
