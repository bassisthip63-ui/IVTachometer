# IV Tachometer 1.0

**A rev counter for GTA IV that wraps the minimap, made to sit with Kunar26's Working Speedometer.**

A white rev-counter arc with a red zone and a red needle sits around the lower left of the minimap, just outside the speedometer's ring, with a digital RPM readout below it. 0 rpm is at the bottom and the needle sweeps clockwise up to the left, the same way round as the speedometer. It reads the speedometer's settings (`SpeedoMeter.cfg` or `.ini`), so it follows the speedometer's position and size, and the speedometer's gear ring shows the gear. Night colours switch on with the vehicle's lights.

It works without the speedometer too, using the speedometer's default layout.

Download the ready-to-use mod from Nexus Mods. This repository holds the source.

---

## What you need

- **ScriptHookDotNet**, or **IV-SDK .NET** with its ScriptHookDotNet support.
- Any game version. Night colours read the lights from game memory on 1.0.8.0 and 1.2.0.59, and from ScriptHookDotNet elsewhere.

## Install

Back up your game folder first. Copy the contents of `scripts` into GTA IV's `scripts` folder:

```
GTA IV\
  scripts\
    IVTachometer.net.dll
    IVTachometer\          the three pictures and the readout font
    IVTachometer.cfg       the settings, made on first run
```

## Keys

You can change both keys in `IVTachometer.cfg`.

| Key | What it does |
|---|---|
| `M` | Hide / show the gauge. The speedometer uses `M` too, and so does Liberty Tweaks for the minimap, so one press hides them all. |
| `Shift+F10` | Adjust mode, in two steps. **1. The gauge:** `PgUp` / `PgDn` make it bigger or smaller, `Left` / `Right` turn it around the map. **2. The readout:** the arrows move it, `PgUp` / `PgDn` change the text size. Hold `Shift` for fine steps, and `End` resets the current step. Press `Shift+F10` to go to the next step, and again to finish. It only works while you're driving, and getting out saves and leaves adjust mode. It's `Shift+F10` rather than plain `F10` because other mods use `F10` (LocalDentIV, for one). |

Changes are saved to `IVTachometer.cfg` a second after you make them. Edits to the ini while the game runs are picked up within a couple of seconds.

## Settings

Every setting in `IVTachometer.cfg` is commented. It's a `.cfg`, not an `.ini`, because under IV-SDK .NET, ScriptHookDotNet resets an `.ini` named after the script to its old copy when the game closes. An `IVTachometer.ini` from an older version is copied over on the first start, so you keep your settings.

- **`[Gauge]`:** `Scale`, `Rotate`, `Opacity`, `ShowRedZone`, `FullRpm`, `ArcColor`, `RedZoneColor`, `NeedleColor`
- **`[Night]`:** `Mode` (`AUTO` = with the vehicle's lights, `ON`, `OFF`) and the night colours
- **`[Digital]`:** `ShowDigital`, `Format` (`{0} RPM`, `R: {0}`, ...), `MaxRpm`, `RoundTo`, `Size`, `Anchor` (`RADAR` or `SCREEN`), `X` / `Y`, `Font` or `FontFile`, `Italic`, `Color`, `NightColor`
- **Colours** can be written as a name (`White`, `Orange`, `SkyBlue`...), `#RRGGBB`, `R,G,B` or `A,R,G,B`. A name stays a name when the gauge saves the file.
- **`scripts\IVTachometer.log`** gets one line each start. Set `Log=1` to add the details for a bug report.

## Nothing shows on screen?

On a few PCs, right after a quick restart of the game, IV-SDK .NET's on-screen drawing can stay hidden while everything keeps running underneath: this gauge, the IV-SDK .NET console (F4) and other mods that draw with it. It comes from start-up timing (the game resets its display while mods are still starting up), not from this mod or the mods below.

Quick fix: **Pause > Graphics**, change the refresh rate (or resolution), apply, then change it back. Most players will never see this.

## Works with

Tested together in one install, GTA IV Complete Edition 1.2.0.59:

- Fusion Fix (including its Vulkan renderer)
- SISCO 1.0.8
- DLSS-IV
- Liberty Tweaks 1.7
- Liberty's Legacy (with Aru's ScriptHook)
- Kunar26's Working Speedometer, in our IV-SDK .NET / 1.2.0.59 update - made to sit together

Not tested with other HUD or minimap mods yet. It isn't meant to run with our upcoming IV Dashboard, which has its own gauges: use one or the other.

## Building

`src/IVTachometer.cs` is the whole mod, written for C# 5 / .NET Framework 4.

1. Put a copy of `ScriptHookDotNet.dll` next to `Build_IVTachometer.cmd`, or drag the dll onto the cmd file.
2. Run `Build_IVTachometer.cmd`. It uses the C# compiler that ships with Windows, so Visual Studio isn't needed, and it writes `IVTachometer.net.dll` and `build.txt`.

`art/make_ring_art.py` draws the three pictures. It needs Python and Pillow.

## Credits and licence

- **Design** by hip63 (bassisthip63-ui). **Code** by bassisthip63-ui with Claude (Anthropic), written from scratch.
- **Licence:** MIT, see `LICENSE.txt`. It covers the code and the pictures.
- **Readout font:** [Alarm Clock](https://www.dafont.com/alarm-clock.font) by David J Patterson, listed as 100% free on dafont.com and included unchanged. The font isn't covered by the MIT licence.
- **Inspired by** tartexs's *RpmGauge and GearBox Script V1.0* (2012). No code or graphics from it are used.
- Made to sit with Kunar26's *Working Speedometer*.

Not affiliated with Rockstar Games or Take-Two Interactive.
