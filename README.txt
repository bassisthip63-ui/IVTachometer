IV Tachometer 1.0.1 - a rev counter for GTA IV that wraps the minimap with the SpeedoMeter mod
==============================================================================================

A white rev-counter arc with a red zone and a red needle, around the lower left of the minimap, just outside the
SpeedoMeter mod's ring, plus a digital RPM readout. 0 rpm is at the bottom and it sweeps clockwise up to the left,
the same way round as the speedometer. It reads the speedometer's settings (SpeedoMeter.cfg or .ini), so it follows the speedometer's position and size,
and the speedometer's gear ring shows the gear. Night colours switch on with the vehicle's lights.
Without the speedometer it still works, using the speedometer's default layout.

Requirements: IV-SDK .NET (with its ScriptHookDotNet support) or ScriptHookDotNet on its own. Nothing else.
Works on any game version (night colours read the lights from game memory on 1.0.8.0 and 1.2.0.59, and from
ScriptHookDotNet elsewhere). If your script host gives no engine speed (plain ScriptHookDotNet on the Complete
Edition), the tach reads it from game memory on 1.2.0.59.

New in 1.0.1
------------
- Fixed: the needle didn't move with plain ScriptHookDotNet on the Complete Edition (1.2.0.59). It now reads the
  engine speed from game memory when the script host gives none. Thanks to Tubthump for the report.
- Tested both ways on 1.2.0.59: under IV-SDK .NET, and under ScriptHookDotNet 1.7.1.9 on its own.

Install
-------
Back up your game folder first. Copy the contents of "scripts" into GTA IV's "scripts" folder:
IVTachometer.net.dll and the "IVTachometer" folder (the three pictures and the readout font). IVTachometer.cfg (the settings) is created there on first run;
an IVTachometer.ini from an older version is copied to it, so you keep your settings.

Keys (change them in IVTachometer.cfg)
--------------------------------------
  M    Hide / show the gauge (handy for screenshots). The SpeedoMeter mod uses M too, and so does Liberty Tweaks
       for the minimap, so one press hides them all.
  Shift+F10  Adjust mode, in two steps, only while you're driving (Shift+F10, not F10: other mods use F10;
       getting out saves and leaves adjust mode). Hold Shift for fine steps, End resets the current step:
       1. the gauge: PgUp / PgDn = bigger / smaller, Left / Right = turn it around the map; Shift+F10 = next
       2. the readout: arrows move it, PgUp / PgDn = text size; Shift+F10 = done
       Changes are saved to IVTachometer.cfg a second after you make them. Edits to the .cfg while the game
       runs are picked up within a couple of seconds.

Settings (IVTachometer.cfg, all commented; .cfg, not .ini, because under IV-SDK .NET an .ini named after the
script is reset to its old copy when the game closes)
------------------------------------------
  [Gauge]   Scale, Rotate, Opacity, ShowRedZone, FullRpm, ArcColor / RedZoneColor / NeedleColor
  [Night]   Mode (AUTO = with the vehicle lights, ON, OFF) and the night colours
  [Digital] ShowDigital, Format ("{0} RPM", "R: {0}", ...), MaxRpm, RoundTo, Size, Anchor (RADAR or SCREEN),
            X / Y, Font or FontFile (a .ttf / .otf in the IVTachometer folder; default "alarm clock.ttf"),
            Italic, Color, NightColor
  Colours: a name (White, Orange, SkyBlue...), #RRGGBB, R,G,B or A,R,G,B. Names stay names when it saves.
  scripts\IVTachometer.log gets one line each start; Log=1 adds the details (for bug reports).

Nothing shows on screen?
------------------------
On a few PCs, right after a quick restart of the game, IV-SDK .NET's on-screen drawing can stay hidden while
everything keeps running underneath (this gauge, the IV-SDK .NET console, other mods that draw with it). It comes
from start-up timing (the game resets its display while mods start up), not from this mod or the mods below.
Quick fix: Pause > Graphics, change the refresh rate (or resolution), apply, then change it back.
Most players will never see this.

Works with
----------
Tested together in one install, GTA IV Complete Edition 1.2.0.59: Fusion Fix (including its Vulkan renderer),
SISCO 1.0.8, DLSS-IV, Liberty Tweaks 1.7, Liberty's Legacy (with Aru's ScriptHook), and Kunar26's Working
Speedometer in our IV-SDK .NET / 1.2.0.59 update (made to sit together).
Not tested with other HUD or minimap mods yet. Not meant to run with our upcoming IV Dashboard, which has its own
gauges: use one or the other.

What I build by
---------------
- Immersive and fun, never annoying: there when you glance at it, out of the way when you don't.
- Make mod love, not mod war: made to sit with Kunar26's Working Speedometer and the mods under "Works with".
  It stays off Tab and the D-pad buttons other popular mods use; adjust is Shift+F10 because others use F10.
- Honest: only what's been tested gets claimed; "Works with" lists the mods it was actually run with.
- Credit where it's due: the idea came from tartexs's RpmGauge script and is credited; nobody's code or files
  are used; the readout font is used under its free licence.
- No game files shipped: nothing from Rockstar is included.
- Your game, your rules: size, angle, opacity, colours, night colours and the readout's text, font, size and
  position, set in the cfg or live in game. The red zone, readout and night colours each switch off on their own.

How it was built and tested
---------------------------
Rome wasn't built in a day, and neither were these mods.
- One feature at a time: arc, needle, readout, night colours and adjust mode, each tested in game before the next.
- Tested, then tested some more: new games, saved games, day and night, and repeated restarts.
- Logs, not guesses: the detailed logs (Log=1) were read line by line to track down problems. The 1.0.1 fix came
  from one, which showed exactly where the engine speed was, and wasn't, being read.
- Real machines: tested on GTA IV Complete Edition 1.2.0.59, under both IV-SDK .NET and ScriptHookDotNet 1.7.1.9.
- Plays nice with other mods: see "Works with".
- A final optimization pass before release: test code removed, per-frame work kept light, logging cut back to
  one start-up line.

Found a problem?
----------------
No amount of testing catches everything, and every setup is different. Constructive bug reports are very welcome:
what happened, what you were doing, your game version, your script host (IV-SDK .NET or ScriptHookDotNet), other
mods, and IVTachometer.log with Log=1. Rants aren't bug reports, and they'll be treated that way.

Source and building
-------------------
src\IVTachometer.cs is the full source; drag ScriptHookDotNet.dll onto Build_IVTachometer.cmd to build it with the
C# compiler that ships with Windows. The pictures are made by art\make_ring_art.py (Python with Pillow).

Credits and licence
-------------------
Design by bassisthip63-ui; code by bassisthip63-ui with Claude (Anthropic), written from scratch. MIT licence
(LICENSE.txt), code and pictures. The readout font, "Alarm Clock" by David J Patterson, is listed as 100% free
on dafont.com (https://www.dafont.com/alarm-clock.font) and is included unchanged; it is not covered by the MIT
licence. Inspired by tartexs's "RpmGauge and GearBox Script V1.0" (2012); no code or
graphics from it are used. Made to sit with Kunar26's "Working Speedometer".
Not affiliated with Rockstar Games or Take-Two Interactive.
