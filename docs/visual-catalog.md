# Visual catalog

Every look Squadra has, by type: what it is called, what makes it recognizable, how it renders, and the Unity assets that define it. Browse it before planning new visuals; the workflow for adding looks is in [the visual variant workflow](../specifications/visual-variant-workflow-spec.md).

The pictures are rendered by the game, from the Sandbox scene with the game's camera, space backdrop and post-processing, by [`render-pictures.cs`](visual-catalog/render-pictures.cs) (see [Adding a look](#adding-a-look)). Each shows the look alone:

- **Worlds**: the surface on a body of radius 1, in a view 3 units high, without owner ring, aura or labels. Every type is drawn the same size, so compare surfaces, not sizes.
- **Scene backgrounds**: the whole Sandbox battlefield framing in a 16:9 view, with nothing in front.
- **Darts**: one Blue Dart, nose to the right, at the size and tint the game draws it with, in a view twice its length high.

The descriptions also say what still shows at gameplay scale: the default size of the type (planet 1, moon 0.5, asteroid 0.3) at the scale the Sandbox battlefield gets in a Game view 1080 pixels high. A world given another size in a mission, or a larger map, shows more or less of it.

## Planets

All planet, moon and asteroid surfaces use the [`Squadra/Planet`](../Assets/Squadra/Visuals/Shaders/Planet.shader) shader and are listed in [`WorldLooks.asset`](../Assets/Squadra/Visuals/WorldLooks.asset). Light falls from the upper left, toward the sun just outside the battlefield. The globe turns slowly and its clouds drift.

| Picture | Look | Description | Assets |
| --- | --- | --- | --- |
| <img src="visual-catalog/planet-ember.png" width="128" alt="Ember"> | **Ember** `ember` | Round rust-orange volcanic globe. Glowing orange-red fractures and pale gray-tan ash patches, both still visible at gameplay scale; thin dusty clouds and a warm atmospheric rim. Reads as the orange planet. | [`Ember.mat`](../Assets/Squadra/Visuals/Materials/Planets/Ember.mat), [`EmberSurface.png`](../Assets/Squadra/Visuals/Textures/EmberSurface.png) |
| <img src="visual-catalog/planet-thalassa.png" width="128" alt="Thalassa"> | **Thalassa** `thalassa` | Round earthlike globe: blue oceans, green and brown land, white clouds, small ice caps and a bright blue atmospheric rim. Warm night lights speckle the dark limb up close but are lost at gameplay scale, where it reads as the blue-green planet. | [`Thalassa.mat`](../Assets/Squadra/Visuals/Materials/Planets/Thalassa.mat), [`ThalassaSurface.png`](../Assets/Squadra/Visuals/Textures/ThalassaSurface.png) |

## Moons

| Picture | Look | Description | Assets |
| --- | --- | --- | --- |
| <img src="visual-catalog/moon-lune.png" width="128" alt="Lune"> | **Lune** `lune` | Round cool-gray moon with no clouds. A large rayed crater near the top stays visible at gameplay scale; smaller craters and the faint blue-gray rim show only up close. | [`Lune.mat`](../Assets/Squadra/Visuals/Materials/Moons/Lune.mat), [`LuneSurface.png`](../Assets/Squadra/Visuals/Textures/LuneSurface.png) |

## Asteroids

Asteroid surfaces have no texture: their outline (`_Lumps`), craters and colors are procedural.

| Picture | Look | Description | Assets |
| --- | --- | --- | --- |
| <img src="visual-catalog/asteroid-cinder.png" width="128" alt="Cinder"> | **Cinder** `cinder` | Lumpy rock in muted olive-gray, covered in small rimmed craters. At gameplay scale the craters blur into texture and it reads as a dull gray-olive rock. | [`Cinder.mat`](../Assets/Squadra/Visuals/Materials/Asteroids/Cinder.mat) |
| <img src="visual-catalog/asteroid-ferrite.png" width="128" alt="Ferrite"> | **Ferrite** `ferrite` | More irregular rock in warm copper-brown with a mottled, swirled surface and faint craters. Next to Cinder at gameplay scale the warmer color survives; the difference in outline and craters mostly does not. | [`Ferrite.mat`](../Assets/Squadra/Visuals/Materials/Asteroids/Ferrite.mat) |

## Scene backgrounds

Backdrops use the [`Squadra/Space`](../Assets/Squadra/Visuals/Shaders/Space.shader) shader and are listed under `backdrops` in [`WorldLooks.asset`](../Assets/Squadra/Visuals/WorldLooks.asset).

| Picture | Look | Description | Assets |
| --- | --- | --- | --- |
| <img src="visual-catalog/backdrop-space.png" width="360" alt="Space"> | **Space** `space`<br>The main menu always shows it | Near-black blue starfield with a violet and magenta nebula across the upper left, deep blue haze and a teal cloud low on the right, and scattered cool and warm stars; the nebula drifts slowly. The sun that lights the worlds sits just outside the upper-left corner. | [`Space.mat`](../Assets/Squadra/Visuals/Materials/Backdrops/Space.mat), [`NebulaDust.png`](../Assets/Squadra/Visuals/Textures/NebulaDust.png) |

## Darts

A Dart look is a material in [`Materials/Darts`](../Assets/Squadra/Visuals/Materials/Darts) with its mesh. Missions draw the one assigned in [`FleetStyle.asset`](../Assets/Squadra/Visuals/Styles/FleetStyle.asset), the same for every player; only the owner's color changes.

| Picture | Look | Description | Assets |
| --- | --- | --- | --- |
| <img src="visual-catalog/dart-dart.png" width="128" alt="Dart"> | **Dart** `dart` | Small round dot with a soft glow in the owner's color, so it shows no facing. At gameplay scale it is a few pixels across; stationed Darts read as a ring of colored dots along the world's owner ring. Trails, shots, explosions and sparks are round dots too. | [`Dart.mat`](../Assets/Squadra/Visuals/Materials/Darts/Dart.mat), [`Dart.asset`](../Assets/Squadra/Visuals/Meshes/Dart.asset) (mesh), [`Dot.shader`](../Assets/Squadra/Visuals/Shaders/Dot.shader) |

## Adding a look

Add or update entries in the same change that adds the look to the game: one entry per look in its type's section, and a new section for a new type. Looks still being worked on stay out of the catalog.

Render the pictures with [`render-pictures.cs`](visual-catalog/render-pictures.cs), so new pictures match the ones here. With the editor open, out of Play Mode and with every scene saved, run from the project folder:

```powershell
unity command script eval --project-path . -- --file docs/visual-catalog/render-pictures.cs
```

- It renders every look of the game into `docs/visual-catalog/<type>-<id>.png` and lists the files it wrote: the surfaces and backdrops in `WorldLooks.asset` (**Squadra > Rebuild Mission Scene** lists the materials put in `Materials/Planets`, `Moons`, `Asteroids` and `Backdrops`), and every material in `Materials/Darts`, drawn with the mesh of the same name in `Visuals/Meshes`, or else the one in `FleetStyle.asset`. A Dart's ID is its material's file name in lower case. Every picture comes out slightly different each run (the globes turn and the nebula drifts), so commit only the pictures of the looks you added or changed.
- With `comparison = true` it also writes one strip per type to `Recordings/VisualCatalog/compare-<type>.png` (not committed): every look of the type side by side at gameplay scale, and for Darts a Blue, a Red and a Green one per look. Looks still being worked on go in its `candidates` list, by asset path, under any folder named `Planets`, `Moons`, `Asteroids`, `Backdrops` or `Darts` (a subfolder such as `Materials/Planets/Work` keeps them out of the game); they appear in the strips, with their close-ups beside them, and never in `docs`. Nothing is assigned or saved. Use it to judge new looks against their closest existing look and each other, then check them in the Sandbox Game view next to rings, labels, routes and fleets.
- A new type is not covered: render it with a temporary preview of its own, at the scale it will have on screen.
