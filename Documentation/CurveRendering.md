# Curved rig rendering

`StickFigureCurveRenderer` is the single runtime component. Attach it to a bound
Unity 2D Sprite Skin or an ancestor containing several skins. An optional **Rig
Root** selects another subtree. **Use Curved Limbs** switches between the original
SpriteRenderer and the generated appearance; disabling the component releases its
resources and restores the original renderer. The component also previews in Edit
mode. Animation clips, bones, IK targets, physics, and imported assets are never
edited by rendering.

## Automatic setup

Setup reads the sprite's bind poses, skin weights, bone lengths, and parent indices.
Bone names and humanoid anatomy are not used. Children that continue a bone's rest
axis form a chain; other branches form separate chains. Unweighted terminal
effectors are discovered by their position along the bone, with a length-based
fallback when no effector exists. Chains may have more than two segments.

Narrow regions with a uniform opaque fill become sampled cubic strokes with round
ends. Texture sampling happens only during setup and does not require changing the
texture importer's Read/Write setting. Detailed and broad regions retain their UVs
and skin weights; flexible textured triangles are subdivided at setup and deformed
along curves. Terminal detailed regions, such as heads, remain conventionally
skinned. If texture readback is unavailable, artwork takes the textured path.

Each frame runs after the existing bow/swing/hand IK systems. It reads the final
pose, updates cached vertex buffers, and forwards the source renderer's tint,
material property block, texture, sprite flips, sorting order, and layer. Triangle
indices and UV buffers are built once. Steady rendering does not allocate managed
buffers or perform texture readback. The original Sprite Skin continues to run.

## Existing characters and new rigs

- Player and zombie inherit the component from `StickFigureRig`. The existing five
  widths are retained as transform-reference overrides to preserve the approved
  player output exactly. These overrides are optional for new rigs.
- Deer has the same component on `DeerRig`, with automatic widths and no overrides.
- Cosmetic ragdolls copy this component's settings to the copied transforms and
  regenerate their presentation. Temporary generated meshes are not cloned.
- Add the component to a new rig, then animate and preview normally. **Softness**
  controls rounding. **Width Scale** adjusts automatically inferred stroke widths.
  **Width Overrides** optionally targets a chain's starting transform.

Sprite changes and bone rebinding invalidate caches automatically. Newly bound
skins recover after initially missing references. Use **Reinitialize From Rig**
from the component's context menu after changing the contents of an external rig
subtree or changing runtime sprite mesh data in place. In the Editor, asset changes
also invalidate caches. Nested curve components own their own subtrees.

## Supported scope

This is a renderer for Unity 2D Sprite Skin rigs, using compatible sprite materials
such as the project's `SketchLand/WorldSprite`. It does not infer a skeleton from
an image and does not apply to 3D SkinnedMeshRenderer rigs. The automatic branch
and narrow-region classification is geometric; unusual art may benefit from width
overrides or a different softness. SpriteMask interactions and sliced/tiled sprites
retain their original rendering rather than losing their mask or sizing behavior.
Invalid or unbound skins also keep their original renderer. The `Status` property
reports discovery results.

Automatic setup performs a synchronous texture readback when an imported texture
is not CPU readable. Avoid repeatedly reinitializing a large population in the same
frame. Per-frame deformation uses the CPU; profile dense, heavily subdivided rigs
and large crowds on the target hardware before treating their cost as acceptable.

## Validation

Run the isolated prefab checks with a stopped Editor:

```sh
unity command script run --file Tools/Validation/CurveRendererChecks.cs --entry CurveRendererChecks.Run --project-path /absolute/path/to/SketchLand --format json
```

The harness stays outside `Assets` and is excluded from game builds. It checks
player, zombie, and deer animation poses; renamed imported and live bones; source
pose immutability; finite geometry; flips; tint/sorting/material forwarding;
toggle resource reuse; repeated reinitialization; missing and late
bindings; disable/enable cleanup; and a root containing multiple skins. A golden
fixture compares 7,740 player stroke vertices across 18 poses with the approved
prototype. A fixture change should be an intentional visual change, not part of
routine test updates.
