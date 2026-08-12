# First Person is Hard

A BepInEx plugin for **Flipping is Hard** that puts the camera inside the phone without
letting the phone rotate it. The view is stuck to your body, but **the horizon never tilts** —
not even in the middle of a flip.

## The idea

The naive way to do first person is to parent the camera to the phone's transform. That
inherits every bit of roll and pitch the body has and makes you sick in ten seconds. So the
two things are kept apart:

- **The rotation never comes from the phone.** It is the aim the game already computes (the
  Cinemachine virtual camera, which your mouse drives) with the roll thrown away. Reusing the
  game's aim matters for a second reason beyond comfort: movement is camera relative
  (`CameraManager.AlignByYaw`), so a view pointing anywhere else would desync the controls
  from what you see.
- **The position is anchored to the centre of mass**, not to a point on the body. An offset
  that rotated with the phone would orbit that centre several times a second while flipping,
  and that high frequency wobble is what actually makes people sick. The offset is applied in
  world space, rotated by the view's yaw at most.
- **The vertical axis is filtered.** The phone bounces constantly, so Y gets a longer
  smoothing than the horizontal axes plus a hard cap on how far it may lag behind (1 m by
  default). The fast jolts are eaten, the view never drifts off the body.
- **Teleports cut, they do not sweep.** More than 5 m of movement in a single frame (respawn,
  checkpoint, beacon) snaps the camera instead of flying it across the level.

On top of that come the comfort aids: a vignette that closes in while you turn or move fast, a
fixed dot in the middle of the screen, its own field of view, and the game's speed lines
turned off while you are inside.

Cinemachine is left alone. It keeps framing an invisible third person shot, and the mod
overwrites the real camera's transform right before it renders — `beginCameraRendering`, not
LateUpdate, because Cinemachine 3's brain runs at a very high execution order and would
overwrite anything written earlier. Turning the mod off is simply no longer writing.

## Your body

Your own phone is hidden locally (nobody else's view changes) and the edges of your collider
are drawn instead. Those lines do rotate with you: you can read how you are spinning without
the view spinning with you.

The renderers are not switched off but put into shadows-only, so your shadow stays on the
ground. From inside the phone that shadow is the one thing still telling you how high off the
floor you are.

## Controls

| Key | What it does |
|---|---|
| `F1` | First person on / off |
| `F2` | Settings menu |
| Arrows | Move through the menu and change values |
| `R` | Put the selected value back to its default |
| `Enter` | Rebind a key (`Esc` cancels) |

Both binds can be changed in the menu itself.

## Tuning the sickness away

The menu exists because the point where a first person view stops being sickening is not
something you get right on the first try — it has to be moved while playing. What matters
most, in order:

1. **Vertical smoothing** and **Max vertical lag** — the phone's bouncing.
2. **Field of view** — a wider one feels faster, which helps some people and hurts others.
   Try it both ways.
3. **Comfort vignette** — raise it if fast turns bother you, lower it if it covers too much.
4. **Height above centre of mass** — where the eye sits inside the phone.

Everything the menu touches is saved to `FirstPersonFIH.cfg`, next to the dll.

## Building

```
build.bat
```

Builds against the Steam demo's interop and copies the dll to
`I:\SteamLibrary\steamapps\common\Flipping is Hard Demo\BepInEx\plugins\FirstPersonFIH`.

`build-playtest.bat` does the same against the 1.8 playtest in `D:\playtest`. For any other
install:

```
dotnet build -c Release -p:GameDir="path\to\game" -p:Deploy=true
```

## Compatibility

It is built not to break across game builds. Everything used directly (`PlayerRef`,
`GameReferences.CameraManager`, `CameraManager.cam` / `cinCam` / `speedLinesController`) has
the same signatures in the Steam demo and in the 1.8 playtest. Anything that may not exist in
another version goes through cached reflection with a default (`GameManager`'s state flags) or
is isolated in a non-inlined method that degrades the feature instead of taking the mod down
(the widened look range, which touches Cinemachine).

No method patching: the mod only reads game state and writes the camera's transform.

## Requirements

- Flipping is Hard (Steam demo or playtest) with [BepInEx 6 (IL2CPP)](https://github.com/BepInEx/BepInEx/releases).
