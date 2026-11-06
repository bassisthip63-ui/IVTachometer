IV Tachometer 1.0 - a rev counter for GTA IV that wraps the minimap with the SpeedoMeter mod
===========================================================================================

A white rev-counter arc with a red zone and a red needle, around the lower left of the minimap, just outside the
SpeedoMeter mod's ring, plus a digital RPM readout. 0 rpm is at the bottom and it sweeps clockwise up to the left,
the same way round as the speedometer. It reads the speedometer's settings (SpeedoMeter.cfg or .ini), so it follows the speedometer's position and size,
and the speedometer's gear ring shows the gear. Night colours switch on with the vehicle's lights.
Without the speedometer it still works, using the speedometer's default layout.

Requirements: ScriptHookDotNet, or IV-SDK .NET with its ScriptHookDotNet support. Works on any game version
(night colours read the lights from game memory on 1.0.8.0 and 1.2.0.59, and from ScriptHookDotNet elsewhere).

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

Laptops with two graphics chips
-------------------------------
If the gauge vanishes or flickers, the game may be getting moved between the laptop's built-in graphics and its
NVIDIA / AMD card. Try these, easiest first:
  1. Play in borderless windowed mode instead of exclusive fullscreen.
  2. Give GTAIV.exe the dedicated card: Windows Settings > System > Display > Graphics (High performance), or
     NVIDIA Control Panel > Manage 3D settings > Program settings.
  3. If the laptop has a MUX switch, set it to the dedicated card ("Discrete" / "dGPU only") in the maker's own app,
     for example MSI Center (MSI), Armoury Crate (ASUS), Lenovo Vantage / Legion Space (Lenovo), OMEN Gaming Hub (HP),
     Alienware Command Center (Dell) or NitroSense / PredatorSense (Acer). It needs a restart and uses more battery.
     (Tested on an MSI laptop with MSI Center's Discrete Graphics mode.)

Works with
----------
Kunar26's Working Speedometer (and our 1.2.0.59 update of it), and our upcoming IVAutoCare and IVWrecks.
Not meant to run with our upcoming IV Dashboard, which has its own gauges: use one or the other.

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
