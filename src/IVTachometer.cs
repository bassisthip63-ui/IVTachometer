// IV Tachometer - a rev counter that wraps the minimap next to the SpeedoMeter mod (ScriptHookDotNet script)
// Written from scratch by bassisthip63-ui with Claude (Anthropic), 2026. MIT licence (see LICENSE.txt).
// Artwork (scripts\IVTachometer\*.png) is original, drawn for this mod from bassisthip63-ui's design.
//
// The arc sits just outside the speedometer's ring, around the lower left of the radar. 0 rpm is at the bottom and
// it sweeps clockwise up to the left, so it reads the same way round as the speedometer. It reads SpeedoMeter.cfg (or .ini),
// so it follows the speedometer's position, size and stretch; without the speedometer it uses the same defaults.
//
// Threads: OnTick runs on the game's script thread and is the only place that asks the game anything.
// OnDraw runs on the render thread and only draws the last State that OnTick published.
//
// Written for C# 5 / .NET Framework 4 so it builds with the csc.exe that ships with Windows.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GTA;

namespace IVTachometer
{
    public class Tachometer : Script
    {
        const string Version = "1.0.1 beta";
        // ---- the artwork's geometry (must match art\make_ring_art.py) -----------------------------------------
        const float ArtSpan = 2.6f;                 // the arc pictures are 2.6 x the speedometer ring radius, centred on the radar
        const float ArtStart = 258f, ArtEnd = 186f; // degrees (180 = left of the ring, 270 = bottom): 0 rpm .. full
        const float NeedleIn = 0.93f, NeedleOut = 1.28f;   // the needle spans these ring radii
        const float NeedleAspect = 64f / 200f;      // needle picture width / height

        class TachSettings
        {
            public bool Enabled = true, ShowForPassenger = false, ShowRedZone = true, Log = false;
            public Keys HideKey = Keys.M, AdjustKey = Keys.Shift | Keys.F10;   // Shift+F10: plain F10 is used by other mods (LocalDentIV...)
            public float Scale = 1f;                // 1 = sized to the speedometer's ring
            public float Rotate = 0f;               // degrees, turns the whole gauge around the radar
            public float Opacity = 1f;
            public float FullRpm = 1f;              // the game's engine speed (0 - 1) that puts the needle at the end
            // colours: the pictures are multiplied by these (white = the pictures as drawn)
            public Color ArcColor = Color.White, RedZoneColor = Color.White, NeedleColor = Color.White;
            // night: used while the vehicle's lights are on (Mode AUTO), always (ON) or never (OFF)
            public string NightMode = "AUTO";
            public Color NightArcColor = Color.FromArgb(255, 170, 178, 190), NightRedZoneColor = Color.FromArgb(255, 215, 200, 200),
                         NightNeedleColor = Color.FromArgb(255, 225, 215, 215);
            // digital readout
            public bool ShowDigital = true;
            public string DigitalFormat = "{0} RPM";
            public float DigitalMaxRpm = 8000f; public int DigitalRoundTo = 50;
            public float DigitalSize = 1.5f;        // text height, % of the screen height
            // where it sits: Anchor RADAR = moves with the minimap / speedometer, X / Y = offset from the map's centre in
            // ring radii (right / down); Anchor SCREEN = X / Y are % of the screen (its centre)
            public string DigitalAnchor = "RADAR";
            public float DigitalX = -1.13f, DigitalY = 1.02f;
            public string DigitalFont = "Arial", DigitalFontFile = "alarm clock.ttf";   // Arial is the fallback if the file is missing
            public bool DigitalItalic = true;
            public Color DigitalColor = Color.White, NightDigitalColor = Color.FromArgb(255, 170, 170, 170);
        }

        // the speedometer's ring, as SpeedoMeter.cfg / .ini sets it (defaults = the speedometer's own defaults)
        class Ring
        {
            public float RadarX = 0.064f, RadarW = 0.161f, RadarY = 0.745f, RadarH = 0.211f;
            public int OffsetX, OffsetY;
            public float ScaleRadius = 0.12f, WidthStretch = 1f, HeightStretch = 1f;
        }

        class State
        {
            public bool Visible, Adjusting, Night;
            public int AdjustStep;                  // 1 = gauge, 2 = digital readout
            public float Rpm;                       // 0 - 1 along the arc
            public float ScreenW, ScreenH;
        }

        TachSettings S = new TachSettings();
        Ring ring = new Ring();
        volatile State state;
        string iniPath, speedoIniPath, logPath, artDir;
        DateTime speedoIniTime = DateTime.MinValue; int lastIniCheck;

        // tick side
        int vehicleId, vehicleSince, lastAdjustTick;
        bool hidden, hideWas, adjustWas, adjusting; int adjustStep;
        bool gaugeUp;                               // last tick: in a vehicle you're driving, with the gauge on
        bool settingsDirty; int lastSettingsChange; DateTime iniTime = DateTime.MinValue;
        bool rpmErrLogged, rpmMemLogged; int rpmLogAt, rpmRawAt;
        int offLightsB, offLightsC, offRpm;         // + the engine rpm float (fallback when Vehicle.CurrentRPM gives nothing)                 // vehicle light flags in game memory (0 = unknown game version)

        // draw side
        float shown; int lastDrawTick, lastDrawAt, firstShowAt, lastResW, lastResH; bool firstRefreshDone;
        Texture texArc, texRed, texNeedle;
        readonly Dictionary<char, Glyph> glyphs = new Dictionary<char, Glyph>();
        volatile bool glyphsDirty;
        volatile bool texturesDirty = true;

        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr hWnd, out int pid);
        static readonly int OwnPid = System.Diagnostics.Process.GetCurrentProcess().Id;

        public Tachometer()
        {
            string scripts = AppDomain.CurrentDomain.BaseDirectory;
            try { scripts = Path.Combine(Game.InstallFolder, "scripts"); } catch { }
            // .cfg, not .ini: ScriptHookDotNet (under IV-SDK .NET) treats "<script name>.ini" as its own settings file and
            // writes back the copy it read at start when the game closes, which threw away every change made in game.
            // An old IVTachometer.ini is copied over once, so updating keeps your settings.
            iniPath = Path.Combine(scripts, "IVTachometer.cfg");
            string oldIni = Path.Combine(scripts, "IVTachometer.ini");
            try { if (!File.Exists(iniPath) && File.Exists(oldIni)) File.Copy(oldIni, iniPath); } catch { }
            // the speedometer's settings: its .cfg, or its .ini (the original mod's name)
            speedoIniPath = Path.Combine(scripts, "SpeedoMeter.cfg");
            if (!File.Exists(speedoIniPath)) speedoIniPath = Path.Combine(scripts, "SpeedoMeter.ini");
            logPath = Path.Combine(scripts, "IVTachometer.log");
            artDir = Path.Combine(scripts, "IVTachometer");
            LoadSettings();
            // one line every start (a fresh log each time); Log=1 adds the details
            try { File.WriteAllText(logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  IV Tachometer " + Version + " loaded (Log=1 in IVTachometer.cfg for details)\r\n"); } catch { }
            Log("settings file: " + iniPath + "; pictures: " + artDir);
            LoadSpeedoLayout();
            DetectGame();
            this.Interval = 10;
            this.Tick += new EventHandler(OnTick);
            this.PerFrameDrawing += new GraphicsEventHandler(OnDraw);
        }

        void Log(string msg)
        {
            if (!S.Log) return;
            try { File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + msg + "\r\n"); }
            catch { }
        }

        // =========================================================================================
        //  Tick: script thread
        // =========================================================================================
        void OnTick(object sender, EventArgs e)
        {
            try { state = Update(); }
            catch (Exception ex) { state = null; Log("tick error: " + ex.Message); }
        }

        static bool GameHasFocus()
        {
            try { int pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid); return pid == OwnPid; }
            catch { return true; }
        }
        // a key, or a combo like Shift+F10 (its modifiers must be held; a plain key works with or without them)
        static bool Held(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }
        static bool Down(Keys k)
        {
            if (k == Keys.None) return false;
            Keys code = k & Keys.KeyCode;
            if (code == Keys.None || !Held((int)code)) return false;
            if ((k & Keys.Shift) != 0 && !Held((int)Keys.ShiftKey)) return false;
            if ((k & Keys.Control) != 0 && !Held((int)Keys.ControlKey)) return false;
            if ((k & Keys.Alt) != 0 && !Held((int)Keys.Menu)) return false;
            return true;
        }
        static string KeyText(Keys k)
        {
            if (k == Keys.None) return "None";
            string s = "";
            if ((k & Keys.Control) != 0) s += "Ctrl+";
            if ((k & Keys.Alt) != 0) s += "Alt+";
            if ((k & Keys.Shift) != 0) s += "Shift+";
            return s + (k & Keys.KeyCode);
        }

        State Update()
        {
            int t = Environment.TickCount;
            if (t - lastIniCheck > 2000)             // follow the speedometer's settings when they change
            {
                lastIniCheck = t;
                try
                {
                    string cfg = Path.Combine(Path.GetDirectoryName(speedoIniPath), "SpeedoMeter.cfg");
                    if (speedoIniPath != cfg && File.Exists(cfg)) { speedoIniPath = cfg; speedoIniTime = DateTime.MinValue; }
                    if (File.Exists(speedoIniPath) && File.GetLastWriteTimeUtc(speedoIniPath) != speedoIniTime) LoadSpeedoLayout();
                }
                catch { }
                // edits to IVTachometer.cfg made while the game runs are picked up (not while adjusting)
                try
                {
                    if (!adjusting && !settingsDirty && File.Exists(iniPath) && File.GetLastWriteTimeUtc(iniPath) != iniTime)
                    {
                        LoadSettings(); glyphsDirty = true; Log("IVTachometer.cfg reloaded");
                    }
                }
                catch { }
            }
            // adjustments are saved a second after the last change, so nothing is lost if you never press the adjust key again
            if (settingsDirty && t - lastSettingsChange > 1000) { settingsDirty = false; SaveSettings(); }

            bool focus = GameHasFocus();
            bool hideNow = focus && Down(S.HideKey);
            if (hideNow && !hideWas) hidden = !hidden;
            hideWas = hideNow;
            bool adjustNow = focus && Down(S.AdjustKey);
            // the adjust key only does anything while the gauge is up (in a vehicle you're driving); on foot it's left
            // to the game and other mods. Getting out while adjusting saves and leaves adjust mode.
            if (adjusting && !gaugeUp) { adjusting = false; adjustStep = 0; SaveSettings(); Notice("IV Tachometer: saved"); }
            if (adjustNow && !adjustWas && (adjusting || gaugeUp))
            {
                // AdjustKey (Shift+F10): gauge -> digital readout (if shown) -> save and leave
                if (!adjusting) { adjusting = true; adjustStep = 1; }
                else if (adjustStep == 1 && S.ShowDigital) adjustStep = 2;
                else { adjusting = false; adjustStep = 0; }
                if (adjusting && adjustStep == 1) Notice("IV Tachometer gauge: PgUp / PgDn = bigger / smaller, Left / Right = turn around the map (Shift = fine), End = reset, " + KeyText(S.AdjustKey) + " = next");
                else if (adjusting) Notice("IV Tachometer readout: arrows = move it, PgUp / PgDn = text size (Shift = fine), End = reset, " + KeyText(S.AdjustKey) + " = save");
                else { SaveSettings(); Notice("IV Tachometer: saved"); }
            }
            adjustWas = adjustNow;
            if (adjusting && focus) AdjustInput();

            Size res = Game.Resolution;
            State st = new State();
            st.ScreenW = res.Width; st.ScreenH = res.Height; st.Adjusting = adjusting; st.AdjustStep = adjustStep;
            st.Night = S.NightMode == "ON";
            if (!S.Enabled || (hidden && !adjusting)) { gaugeUp = false; return st; }

            Ped ped = this.Player.Character;
            Vehicle veh = null;
            if (ped != null && ped.Exists() && ped.isAlive && ped.isInVehicle()) veh = ped.CurrentVehicle;
            bool usable = veh != null && veh.Exists();
            if (usable)
            {
                Model m = veh.Model;
                if (m.isHelicopter || m.isPlane || m.isBoat) usable = false;
            }
            if (usable && !S.ShowForPassenger)
            {
                Ped driver = veh.GetPedOnSeat(VehicleSeat.Driver);
                usable = driver != null && driver.GetHashCode() == ped.GetHashCode();
            }
            if (usable) { try { usable = this.Player.CanControlCharacter; } catch { } }   // not in cutscenes
            gaugeUp = usable;
            if (!usable) { vehicleId = 0; return st; }

            int id = veh.GetHashCode();
            if (id != vehicleId) { vehicleId = id; vehicleSince = Environment.TickCount; }
            if (Environment.TickCount - vehicleSince < 1000) return st;                 // same 1 s wait as the speedometer

            float rpm = 0f;
            try { rpm = veh.CurrentRPM; }
            catch (Exception ex) { if (!rpmErrLogged) { rpmErrLogged = true; Log("engine speed: the game/IV-SDK .NET did not give it (" + ex.GetType().Name + ": " + ex.Message + ")"); } }
            // some script hosts (ScriptHookDotNet on the Complete Edition) give 0 here: read the engine's rpm from the
            // vehicle in game memory instead, where this game version is known (same place IV-SDK .NET reads it)
            string src = "CurrentRPM";
            if (rpm <= 0f && offRpm != 0)
            {
                long vp = 0; try { vp = (long)(uint)((GTA.@base.iAddressableObject)veh).MemoryAddress; } catch { }
                float mr = 0f; bool ok = vp != 0 && ReadFloat(vp + offRpm, out mr);
                if (ok && mr > 0f && mr < 1.5f) { rpm = mr; src = "memory"; if (!rpmMemLogged) { rpmMemLogged = true; Log("engine speed: reading it from game memory (+0x" + offRpm.ToString("X") + ")"); } }
                else if (S.Log && Environment.TickCount - rpmRawAt > 2000) { rpmRawAt = Environment.TickCount; Log("engine speed in memory: " + (vp == 0 ? "no vehicle address" : !ok ? "not readable at +0x" + offRpm.ToString("X") : "raw " + mr.ToString("0.###", CultureInfo.InvariantCulture) + " at +0x" + offRpm.ToString("X") + " (out of range, not used)")); }
            }
            if (S.Log && Environment.TickCount - rpmLogAt > 2000) { rpmLogAt = Environment.TickCount; Log("engine speed " + rpm.ToString("0.000", CultureInfo.InvariantCulture) + " (" + src + ")"); }
            st.Rpm = Math.Max(0f, Math.Min(1f, rpm / Math.Max(0.1f, S.FullRpm)));
            st.Night = IsNight(veh);
            st.Visible = true;
            return st;
        }

        // night = the vehicle's lights are on: read from the vehicle in game memory where this game version is
        // known (1.0.8.0, 1.2.0.59), otherwise from ScriptHookDotNet's Vehicle.LightsOn
        void DetectGame()
        {
            try
            {
                long b = System.Diagnostics.Process.GetCurrentProcess().MainModule.BaseAddress.ToInt64();
                int pe = System.Runtime.InteropServices.Marshal.ReadInt32(new IntPtr(b + 0x3C));
                uint stamp = (uint)System.Runtime.InteropServices.Marshal.ReadInt32(new IntPtr(b + pe + 8));
                if (stamp == 0x57C6FB75u) { offLightsB = 3941; offLightsC = 3945; offRpm = 0x688; }        // 1.0.8.0
                else if (stamp == 0x63D3E735u) { offLightsB = 3861; offLightsC = 3865; offRpm = 0x638; }   // 1.2.0.59 (0x50 lower)
                Log(offLightsB != 0 ? "night: vehicle light flags from game memory" : "night: unknown game version, using Vehicle.LightsOn");
            }
            catch { offLightsB = offLightsC = 0; }
        }

        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr address, byte[] buffer, int size, out int read);
        static readonly byte[] readBuf = new byte[1];   // reused: only the script thread reads memory
        static bool ReadByte(long address, out byte value)
        {
            value = 0;
            if (address < 0x10000 || address > 0xFFFFFFFFL) return false;
            try
            {
                byte[] buf = readBuf; int read;
                if (!ReadProcessMemory(GetCurrentProcess(), new IntPtr(unchecked((int)(uint)address)), buf, 1, out read) || read != 1) return false;
                value = buf[0]; return true;
            }
            catch { return false; }
        }

        static readonly byte[] readBuf4 = new byte[4];
        static bool ReadFloat(long address, out float value)
        {
            value = 0f;
            if (address < 0x10000 || address > 0xFFFFFFFFL) return false;
            try
            {
                int read;
                if (!ReadProcessMemory(GetCurrentProcess(), new IntPtr(unchecked((int)(uint)address)), readBuf4, 4, out read) || read != 4) return false;
                value = BitConverter.ToSingle(readBuf4, 0);
                return !float.IsNaN(value) && !float.IsInfinity(value);
            }
            catch { return false; }
        }

        bool lastNight; bool nightLogged;
        bool IsNight(Vehicle veh)
        {
            if (S.NightMode == "ON") return true;
            if (S.NightMode == "OFF") return false;
            bool night = false; int how = 0;   // 0 Vehicle.LightsOn, 1 memory, 2 memory not readable (text only built for the log)
            long p = 0; byte fb = 0, fc = 0;
            if (offLightsB != 0)
            {
                try { p = (long)(uint)((GTA.@base.iAddressableObject)veh).MemoryAddress; } catch { }
                if (p != 0 && ReadByte(p + offLightsB, out fb) && ReadByte(p + offLightsC, out fc))
                {
                    night = (fc & 2) != 0 || (fb & 1) != 0;
                    how = 1;
                }
                else { try { night = veh.LightsOn; } catch { } how = 2; }
            }
            else { try { night = veh.LightsOn; } catch { } }
            if (!nightLogged || night != lastNight)
            {
                nightLogged = true; lastNight = night;
                if (S.Log) Log("night colours " + (night ? "ON" : "off") + " (" + (how == 1 ? "memory, flags 0x" + fb.ToString("X2") + " 0x" + fc.ToString("X2") : how == 2 ? "Vehicle.LightsOn (memory not readable)" : "Vehicle.LightsOn") + ")");
            }
            return night;
        }

        void AdjustInput()
        {
            string before = AdjustState();
            AdjustInputCore();
            if (AdjustState() != before) { settingsDirty = true; lastSettingsChange = Environment.TickCount; }
        }
        string AdjustState()
        {
            return Fmt(S.Scale) + "|" + Fmt(S.Rotate) + "|" + Fmt(S.DigitalX) + "|" + Fmt(S.DigitalY) + "|" + Fmt(S.DigitalSize) + "|" + S.DigitalAnchor;
        }

        void AdjustInputCore()
        {
            int now = Environment.TickCount;
            float dt = Math.Min(0.1f, Math.Max(0f, (now - lastAdjustTick) / 1000f));
            lastAdjustTick = now;
            float fine = Down(Keys.ShiftKey) ? 0.2f : 1f;
            if (adjustStep == 2)
            {
                // arrows move it on screen; RADAR offsets are in ring radii, SCREEN ones in %
                float sp = (S.DigitalAnchor == "SCREEN" ? 12f : 0.8f) * fine * dt;
                if (Down(Keys.Left)) S.DigitalX -= sp;
                if (Down(Keys.Right)) S.DigitalX += sp;
                if (Down(Keys.Up)) S.DigitalY -= sp;
                if (Down(Keys.Down)) S.DigitalY += sp;
                if (Down(Keys.PageUp)) S.DigitalSize += 0.6f * fine * dt;
                if (Down(Keys.PageDown)) S.DigitalSize -= 0.6f * fine * dt;
                if (Down(Keys.End)) { TachSettings d = new TachSettings(); S.DigitalAnchor = d.DigitalAnchor; S.DigitalX = d.DigitalX; S.DigitalY = d.DigitalY; S.DigitalSize = d.DigitalSize; }
                if (S.DigitalAnchor == "SCREEN") { S.DigitalX = Math.Max(0f, Math.Min(100f, S.DigitalX)); S.DigitalY = Math.Max(0f, Math.Min(100f, S.DigitalY)); }
                else { S.DigitalX = Math.Max(-6f, Math.Min(12f, S.DigitalX)); S.DigitalY = Math.Max(-8f, Math.Min(4f, S.DigitalY)); }
                S.DigitalSize = Math.Max(0.5f, Math.Min(6f, S.DigitalSize));
                return;
            }
            if (Down(Keys.PageUp)) S.Scale += 0.25f * fine * dt;
            if (Down(Keys.PageDown)) S.Scale -= 0.25f * fine * dt;
            if (Down(Keys.Left)) S.Rotate += 30f * fine * dt;
            if (Down(Keys.Right)) S.Rotate -= 30f * fine * dt;
            if (Down(Keys.End)) { S.Scale = 1f; S.Rotate = 0f; }
            S.Scale = Math.Max(0.5f, Math.Min(2f, S.Scale));
            S.Rotate = Math.Max(-180f, Math.Min(180f, S.Rotate));
        }

        void Notice(string text) { try { Game.DisplayText(text, 4000); } catch { } }

        // =========================================================================================
        //  Draw: render thread
        // =========================================================================================
        void OnDraw(object sender, GraphicsEventArgs e)
        {
            try
            {
                int now = Environment.TickCount;
                float dt = lastDrawTick == 0 ? 0f : Math.Min(0.1f, Math.Max(0f, (now - lastDrawTick) / 1000f));
                lastDrawTick = now;

                // Direct3D textures can come back blank after the device was reset (alt-tab in fullscreen, display
                // changes) and sometimes the first time they are used: release them so they are made again.
                if (texturesDirty) { texturesDirty = false; LoadTextures(); }
                if (glyphsDirty) { glyphsDirty = false; ClearGlyphs(); }
                else if (lastDrawAt != 0 && now - lastDrawAt > 500) RefreshTextures();
                lastDrawAt = now;

                State st = state;
                if (st == null || st.ScreenW < 1f || st.ScreenH < 1f) return;
                if (lastResW != 0 && ((int)st.ScreenW != lastResW || (int)st.ScreenH != lastResH)) RefreshTextures();
                lastResW = (int)st.ScreenW; lastResH = (int)st.ScreenH;
                if (!st.Visible && !st.Adjusting) { shown = 0f; return; }
                if (firstShowAt == 0) firstShowAt = now == 0 ? 1 : now;
                else if (!firstRefreshDone && now - firstShowAt > 1500) { firstRefreshDone = true; RefreshTextures(); }

                float target = st.Visible ? st.Rpm : 0.5f;
                shown += (target - shown) * Math.Min(1f, dt * 14f);     // the needle follows the engine smoothly
                DrawGauge(e.Graphics, st);
            }
            catch { }
        }

        void LoadTextures()
        {
            ReleaseTexture(ref texArc); ReleaseTexture(ref texRed); ReleaseTexture(ref texNeedle);
            texArc = LoadTexture("tach_arc.png");
            texRed = LoadTexture("tach_arc_red.png");
            texNeedle = LoadTexture("tach_arc_needle.png");
        }
        Texture LoadTexture(string name)
        {
            string f = Path.Combine(artDir, name);
            try { if (File.Exists(f)) return new Texture(File.ReadAllBytes(f)); } catch (Exception ex) { Log("could not load " + name + ": " + ex.Message); }
            Log("missing " + f);
            return null;
        }
        void RefreshTextures()
        {
            Texture[] all = { texArc, texRed, texNeedle };
            foreach (Texture t in all)
                if (t != null) { try { ((GTA.@base.iD3DObject)t).Unload(false); } catch { } }
            foreach (Glyph gl in glyphs.Values)
                if (gl.Tex != null) { try { ((GTA.@base.iD3DObject)gl.Tex).Unload(false); } catch { } }
        }
        static void ReleaseTexture(ref Texture t)
        {
            if (t == null) return;
            try { ((GTA.@base.iD3DObject)t).Unload(true); } catch { }
            try { t.Dispose(); } catch { }
            t = null;
        }

        void DrawGauge(GTA.Graphics g, State st)
        {
            Ring r = ring;
            float w = st.ScreenW, h = st.ScreenH;
            float aspect = w / h;
            float aspectFix = aspect <= 1.33334f ? 1f : (1.3333333f / aspect);
            float cx = w * r.RadarX * aspectFix + (w * r.RadarW * aspectFix) / 2f + r.OffsetX;
            float cy = h * r.RadarY + (h * r.RadarH) / 2f + r.OffsetY;
            float R = h * r.ScaleRadius * S.Scale, sx = r.WidthStretch, sy = r.HeightStretch;
            float op = Math.Max(0.1f, Math.Min(1f, S.Opacity));
            Color arcTint = Fade(st.Night ? S.NightArcColor : S.ArcColor, op);
            Color redTint = Fade(st.Night ? S.NightRedZoneColor : S.RedZoneColor, op);
            Color needleTint = Fade(st.Night ? S.NightNeedleColor : S.NeedleColor, op);
            float rot = S.Rotate * (float)Math.PI / 180f;

            // the arc pictures, centred on the radar (turned with Rotate)
            float aw = ArtSpan * R * sx, ah = ArtSpan * R * sy;
            if (texArc != null) g.DrawSprite(texArc, cx, cy, aw, ah, -rot, arcTint);
            if (texRed != null && S.ShowRedZone) g.DrawSprite(texRed, cx, cy, aw, ah, -rot, redTint);

            // the needle: its picture points up with the inner (round) end at the bottom. Put that end at NeedleIn
            // ring radii from the centre, then turn it to the rpm's angle around the radar.
            if (texNeedle != null)
            {
                double deg = ArtStart + (ArtEnd - ArtStart) * shown + S.Rotate;
                float ang = (float)(deg * Math.PI / 180.0);
                float len = (NeedleOut - NeedleIn) * R, wid = len * NeedleAspect;
                Matrix m = Matrix.Translation(-0.5f, -1f, 0f) *                 // base centre to the origin, tip up
                           Matrix.Scaling(wid, len, 1f) *
                           Matrix.Translation(0f, -NeedleIn * R, 0f) *            // out to the arc
                           Matrix.RotationZ((float)Math.PI / 2f - ang) *          // "up" turns to the rpm's direction
                           Matrix.Scaling(sx, sy, 1f) *
                           Matrix.Translation(cx, cy, 0f);
                g.DrawSprite(texNeedle, m, needleTint);
            }

            if (S.ShowDigital) DrawDigital(g, st, cx, cy, R / S.Scale, sx, sy, op);

            // adjust mode: a thin outline around what is being moved
            if (st.Adjusting)
            {
                Color ac = Color.FromArgb(220, 80, 200, 255);
                if (st.AdjustStep == 1) for (int i = 0; i < 24; i++)
                {
                    double a0 = (ArtStart + (ArtEnd - ArtStart) * i / 24f + S.Rotate) * Math.PI / 180.0, a1 = (ArtStart + (ArtEnd - ArtStart) * (i + 1) / 24f + S.Rotate) * Math.PI / 180.0;
                    float rr = R * 1.32f;
                    g.DrawLine(cx + (float)Math.Cos(a0) * rr * sx, cy - (float)Math.Sin(a0) * rr * sy, cx + (float)Math.Cos(a1) * rr * sx, cy - (float)Math.Sin(a1) * rr * sy, 1.5f, ac);
                }
                else if (st.AdjustStep == 2 && lastDigitalRect.Width > 0f)
                {
                    RectangleF q = lastDigitalRect;
                    g.DrawLine(q.Left, q.Top, q.Right, q.Top, 1.5f, ac); g.DrawLine(q.Right, q.Top, q.Right, q.Bottom, 1.5f, ac);
                    g.DrawLine(q.Right, q.Bottom, q.Left, q.Bottom, 1.5f, ac); g.DrawLine(q.Left, q.Bottom, q.Left, q.Top, 1.5f, ac);
                }
            }
        }

        static Color Fade(Color c, float a) { return Color.FromArgb((int)Math.Max(0, Math.Min(255, c.A * a)), c.R, c.G, c.B); }

        // =========================================================================================
        //  Digital readout: each character is rendered once from a font into a small white picture,
        //  then drawn tinted. Render thread only.
        // =========================================================================================
        sealed class Glyph { public Texture Tex; public float Aspect; }
        const float GlyphEm = 64f;
        float glyphCellH = GlyphEm * 1.3f, glyphZeroW;
        bool glyphFailed;
        RectangleF lastDigitalRect;
        System.Drawing.Text.PrivateFontCollection fontFiles;

        void ClearGlyphs()
        {
            fontFiles = null;
            foreach (Glyph gl in glyphs.Values) if (gl.Tex != null) { Texture t = gl.Tex; ReleaseTexture(ref t); }
            glyphs.Clear(); glyphZeroW = 0f;
        }

        System.Drawing.Font MakeFont()
        {
            System.Drawing.FontStyle style = System.Drawing.FontStyle.Bold | (S.DigitalItalic ? System.Drawing.FontStyle.Italic : System.Drawing.FontStyle.Regular);
            if (!string.IsNullOrEmpty(S.DigitalFontFile))
            {
                try
                {
                    string path = S.DigitalFontFile;
                    if (!Path.IsPathRooted(path)) path = File.Exists(Path.Combine(artDir, path)) ? Path.Combine(artDir, path) : Path.Combine(Path.GetDirectoryName(iniPath), path);
                    if (fontFiles == null && File.Exists(path)) { fontFiles = new System.Drawing.Text.PrivateFontCollection(); fontFiles.AddFontFile(path); }
                    if (fontFiles != null && fontFiles.Families.Length > 0)
                    {
                        System.Drawing.FontFamily fam = fontFiles.Families[0];
                        System.Drawing.FontStyle[] order = { style, System.Drawing.FontStyle.Bold, System.Drawing.FontStyle.Regular, System.Drawing.FontStyle.Italic };
                        foreach (System.Drawing.FontStyle fs in order)
                            if (fam.IsStyleAvailable(fs)) return new System.Drawing.Font(fam, GlyphEm, fs, GraphicsUnit.Pixel);
                    }
                }
                catch (Exception ex) { Log("font file: " + ex.Message); }
            }
            return new System.Drawing.Font(S.DigitalFont, GlyphEm, style, GraphicsUnit.Pixel);
        }

        Glyph GetGlyph(char ch)
        {
            Glyph gl;
            if (glyphs.TryGetValue(ch, out gl)) return gl;
            gl = BuildGlyph(ch);
            glyphs[ch] = gl;
            return gl;
        }

        Glyph BuildGlyph(char ch)
        {
            try
            {
                using (System.Drawing.Font f = MakeFont())
                using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
                using (Bitmap probe = new Bitmap(1, 1))
                using (System.Drawing.Graphics pg = System.Drawing.Graphics.FromImage(probe))
                {
                    sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                    int pad = 6;
                    int cellH = (int)Math.Ceiling((double)f.Height) + pad * 2;
                    glyphCellH = cellH;
                    if (glyphZeroW <= 0f) glyphZeroW = pg.MeasureString("0", f, 1000, sf).Width;
                    if (ch == ' ') return new Glyph { Tex = null, Aspect = (GlyphEm * 0.28f) / cellH };
                    bool digit = ch >= '0' && ch <= '9';
                    float own = pg.MeasureString(ch.ToString(), f, 1000, sf).Width;
                    float w = digit ? glyphZeroW : own;                         // same width for every digit: no jitter
                    int cellW = (int)Math.Ceiling((double)w) + pad * 2 + (S.DigitalItalic ? 8 : 0);
                    float x0 = pad + (digit ? (glyphZeroW - own) * 0.5f : 0f);
                    using (Bitmap bmp = new Bitmap(cellW, cellH, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    using (System.Drawing.Graphics gr = System.Drawing.Graphics.FromImage(bmp))
                    {
                        gr.Clear(Color.Transparent);
                        gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                        using (Brush shadow = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                        {
                            gr.DrawString(ch.ToString(), f, shadow, x0 + 3f, pad + 3f, sf);
                            gr.DrawString(ch.ToString(), f, shadow, x0 + 2f, pad + 2f, sf);
                        }
                        gr.DrawString(ch.ToString(), f, Brushes.White, x0, pad, sf);
                        using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                        {
                            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            return new Glyph { Tex = new Texture(ms.ToArray()), Aspect = (float)cellW / cellH };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!glyphFailed) { glyphFailed = true; Log("digital readout: could not build the text: " + ex.Message); }
                return new Glyph { Tex = null, Aspect = 0.5f };
            }
        }

        Glyph[] digGlyphs; int digValue = int.MinValue; string digFormat;   // render thread only
        void DrawDigital(GTA.Graphics g, State st, float cx, float cy, float ringR, float sx, float sy, float op)
        {
            int step = Math.Max(1, S.DigitalRoundTo);
            int value = (int)Math.Round(shown * S.DigitalMaxRpm / step) * step;
            // the text and its pictures are only worked out again when the number (or the format, or the font) changes
            if (digGlyphs == null || value != digValue || S.DigitalFormat != digFormat || glyphs.Count == 0)
            {
                string text;
                try { text = string.Format(CultureInfo.InvariantCulture, S.DigitalFormat, value); }
                catch { text = value.ToString(CultureInfo.InvariantCulture); }
                Glyph[] g2 = new Glyph[text.Length];
                for (int i = 0; i < text.Length; i++) g2[i] = GetGlyph(text[i]);
                digGlyphs = g2; digValue = value; digFormat = S.DigitalFormat;
            }
            Glyph[] gl = digGlyphs;
            float cellH = S.DigitalSize / 100f * st.ScreenH * (glyphCellH / GlyphEm);
            float total = 0f;
            for (int i = 0; i < gl.Length; i++) total += gl[i].Aspect * cellH;

            float px, py;
            if (S.DigitalAnchor == "SCREEN") { px = S.DigitalX / 100f * st.ScreenW; py = S.DigitalY / 100f * st.ScreenH; }
            else { px = cx + S.DigitalX * ringR * sx; py = cy + S.DigitalY * ringR * sy; }
            float x = px - total * 0.5f, y = py - cellH * 0.5f;
            lastDigitalRect = new RectangleF(x, y, total, cellH);
            Color c = Fade(st.Night ? S.NightDigitalColor : S.DigitalColor, op);
            for (int i = 0; i < gl.Length; i++)
            {
                float gw = gl[i].Aspect * cellH;
                if (gl[i].Tex != null) g.DrawSprite(gl[i].Tex, new RectangleF(x, y, gw, cellH), c);
                x += gw;
            }
        }

        // =========================================================================================
        //  Settings
        // =========================================================================================
        static Dictionary<string, string> ReadIni(string path)
        {
            Dictionary<string, string> v = new Dictionary<string, string>();
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#' || line[0] == '[') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string val = line.Substring(eq + 1);
                int sc = val.IndexOf(';'); if (sc >= 0) val = val.Substring(0, sc);
                v[line.Substring(0, eq).Trim().ToLowerInvariant()] = val.Trim();
            }
            return v;
        }

        // the speedometer's position and size (its settings file is only read, never written)
        void LoadSpeedoLayout()
        {
            Ring r = new Ring();
            try
            {
                if (File.Exists(speedoIniPath))
                {
                    speedoIniTime = File.GetLastWriteTimeUtc(speedoIniPath);
                    Dictionary<string, string> v = ReadIni(speedoIniPath);
                    string x;
                    if (v.TryGetValue("offsetx", out x)) r.OffsetX = (int)Num(x, 0, -10000, 10000);
                    if (v.TryGetValue("offsety", out x)) r.OffsetY = (int)Num(x, 0, -10000, 10000);
                    if (v.TryGetValue("scaleradius", out x)) r.ScaleRadius = Num(x, r.ScaleRadius, 0.01f, 1f);
                    if (v.TryGetValue("widthstretch", out x)) r.WidthStretch = Num(x, r.WidthStretch, 0.1f, 5f);
                    if (v.TryGetValue("heightstretch", out x)) r.HeightStretch = Num(x, r.HeightStretch, 0.1f, 5f);
                    Log("following " + Path.GetFileName(speedoIniPath));
                }
            }
            catch (Exception ex) { Log("could not read " + Path.GetFileName(speedoIniPath) + ": " + ex.Message); }
            ring = r;
        }

        void LoadSettings()
        {
            TachSettings n = new TachSettings();
            if (!File.Exists(iniPath)) { S = n; SaveSettings(); return; }
            try
            {
                rawColours.Clear();
                foreach (KeyValuePair<string, string> kv in ReadIni(iniPath))
                {
                    string x = kv.Value;
                    if (kv.Key.EndsWith("color")) rawColours[kv.Key] = x;      // keep how you wrote it (e.g. a name)
                    switch (kv.Key)
                    {
                        case "enabled": n.Enabled = Bool(x, n.Enabled); break;
                        case "showforpassenger": n.ShowForPassenger = Bool(x, n.ShowForPassenger); break;
                        case "showredzone": n.ShowRedZone = Bool(x, n.ShowRedZone); break;
                        case "log": n.Log = Bool(x, n.Log); break;
                        case "hidekey": n.HideKey = KeyOf(x, n.HideKey); break;
                        case "adjustkey": n.AdjustKey = KeyOf(x, n.AdjustKey); break;
                        case "scale": n.Scale = Num(x, n.Scale, 0.5f, 2f); break;
                        case "rotate": n.Rotate = Num(x, n.Rotate, -180f, 180f); break;
                        case "opacity": n.Opacity = Num(x, n.Opacity, 0.1f, 1f); break;
                        case "fullrpm": n.FullRpm = Num(x, n.FullRpm, 0.1f, 2f); break;
                        case "arccolor": n.ArcColor = Col(x, n.ArcColor); break;
                        case "redzonecolor": n.RedZoneColor = Col(x, n.RedZoneColor); break;
                        case "needlecolor": n.NeedleColor = Col(x, n.NeedleColor); break;
                        case "mode": { string mm = x.Trim().ToUpperInvariant(); n.NightMode = (mm == "ON" || mm == "OFF") ? mm : "AUTO"; } break;
                        case "nightarccolor": n.NightArcColor = Col(x, n.NightArcColor); break;
                        case "nightredzonecolor": n.NightRedZoneColor = Col(x, n.NightRedZoneColor); break;
                        case "nightneedlecolor": n.NightNeedleColor = Col(x, n.NightNeedleColor); break;
                        case "showdigital": n.ShowDigital = Bool(x, n.ShowDigital); break;
                        case "format": if (x.Length > 0) n.DigitalFormat = x; break;
                        case "maxrpm": n.DigitalMaxRpm = Num(x, n.DigitalMaxRpm, 100f, 100000f); break;
                        case "roundto": n.DigitalRoundTo = (int)Num(x, n.DigitalRoundTo, 1f, 1000f); break;
                        case "size": n.DigitalSize = Num(x, n.DigitalSize, 0.5f, 6f); break;
                        case "anchor": n.DigitalAnchor = x.Trim().ToUpperInvariant() == "SCREEN" ? "SCREEN" : "RADAR"; break;
                        case "x": n.DigitalX = Num(x, n.DigitalX, -100f, 100f); break;
                        case "y": n.DigitalY = Num(x, n.DigitalY, -100f, 100f); break;
                        case "font": if (x.Length > 0) n.DigitalFont = x; break;
                        case "fontfile": n.DigitalFontFile = x.Trim('"'); break;
                        case "italic": n.DigitalItalic = Bool(x, n.DigitalItalic); break;
                        case "color": n.DigitalColor = Col(x, n.DigitalColor); break;
                        case "nightcolor": n.NightDigitalColor = Col(x, n.NightDigitalColor); break;
                    }
                }
            }
            catch { }
            try { iniTime = File.GetLastWriteTimeUtc(iniPath); } catch { }
            S = n;
        }

        void SaveSettings()
        {
            TachSettings s = S;
            string text =
"; IV Tachometer settings (IVTachometer.cfg). Edit it any time - changes made while the game runs are picked up -\r\n; or use the adjust key in game (it saves here).\r\n" +
"; The gauge wraps the minimap next to the SpeedoMeter mod and follows SpeedoMeter.cfg / .ini (position and size).\r\n" +
"[General]\r\n" +
"Enabled=" + (s.Enabled ? 1 : 0) + "\r\n" +
"; HideKey shows / hides the gauge (M: the speedometer's hide key too, and Liberty Tweaks' minimap toggle). AdjustKey = adjust mode (Shift+F10; a combo like Ctrl+F9 works too). None = off.\r\n" +
"HideKey=" + KeyText(s.HideKey) + "\r\nAdjustKey=" + KeyText(s.AdjustKey) + "\r\n" +
"ShowForPassenger=" + (s.ShowForPassenger ? 1 : 0) + "\r\n" +
"; 1 = write IVTachometer.log (for bug reports)\r\nLog=" + (s.Log ? 1 : 0) + "\r\n" +
"\r\n[Gauge]\r\n" +
"; Scale: 1 = sized to the speedometer's ring. Rotate: degrees to turn the whole gauge around the map.\r\n" +
"Scale=" + Fmt(s.Scale) + "\r\nRotate=" + Fmt(s.Rotate) + "\r\n" +
"; 0.1 - 1\r\nOpacity=" + Fmt(s.Opacity) + "\r\n" +
"ShowRedZone=" + (s.ShowRedZone ? 1 : 0) + "\r\n" +
"; The game reports engine speed from 0 to 1. FullRpm is the value that puts the needle at the end of the arc.\r\n" +
"FullRpm=" + Fmt(s.FullRpm) + "\r\n" +
"; Colours multiply the pictures (White = as drawn). Write them as a colour name, #RRGGBB, R,G,B or A,R,G,B (A = opacity).\r\n" +
"; Names: White, Black, Gray, Silver, LightGray, DarkGray, Red, DarkRed, Crimson, Orange, DarkOrange, Gold, Yellow,\r\n" +
";        Lime, Green, DarkGreen, Cyan, Aqua, SkyBlue, DeepSkyBlue, Blue, Navy, Purple, Violet, Magenta, Pink, HotPink,\r\n" +
";        Brown, Tan, Beige, Khaki, Coral, Tomato, Salmon, Turquoise, Teal, and every other .NET colour name.\r\n" +
"ArcColor=" + ColOut("arccolor", s.ArcColor) + "\r\nRedZoneColor=" + ColOut("redzonecolor", s.RedZoneColor) + "\r\nNeedleColor=" + ColOut("needlecolor", s.NeedleColor) + "\r\n" +
"\r\n[Night]\r\n" +
"; Night colours, used while the vehicle's lights are on. Mode: AUTO = with the lights, ON = always, OFF = never.\r\n" +
"Mode=" + s.NightMode + "\r\n" +
"NightArcColor=" + ColOut("nightarccolor", s.NightArcColor) + "\r\nNightRedZoneColor=" + ColOut("nightredzonecolor", s.NightRedZoneColor) + "\r\nNightNeedleColor=" + ColOut("nightneedlecolor", s.NightNeedleColor) + "\r\n" +
"\r\n[Digital]\r\n" +
"; The digital RPM readout. Move it in game with the adjust key (its second step).\r\n" +
"ShowDigital=" + (s.ShowDigital ? 1 : 0) + "\r\n" +
"; {0} is the number, e.g. {0} RPM, R: {0}, or just {0}\r\n" +
"Format=" + s.DigitalFormat + "\r\n" +
"; the number shown with the needle at the end of the arc, and what it is rounded to\r\n" +
"MaxRpm=" + Fmt(s.DigitalMaxRpm) + "\r\nRoundTo=" + s.DigitalRoundTo + "\r\n" +
"; text height in % of the screen height\r\nSize=" + Fmt(s.DigitalSize) + "\r\n" +
"; Anchor RADAR: moves with the minimap; X / Y = offset of its centre from the map's centre in ring radii (right / down).\r\n" +
"; Anchor SCREEN: X / Y = where its centre is, in % of the screen. The adjust key's second step moves it with the arrows.\r\n" +
"Anchor=" + s.DigitalAnchor + "\r\nX=" + Fmt(s.DigitalX) + "\r\nY=" + Fmt(s.DigitalY) + "\r\n" +
"; any font installed in Windows, or FontFile = a .ttf / .otf in the IVTachometer folder (no install needed)\r\n" +
"Font=" + s.DigitalFont + "\r\nFontFile=" + s.DigitalFontFile + "\r\nItalic=" + (s.DigitalItalic ? 1 : 0) + "\r\n" +
"Color=" + ColOut("color", s.DigitalColor) + "\r\nNightColor=" + ColOut("nightcolor", s.NightDigitalColor) + "\r\n";
            try { File.WriteAllText(iniPath, text); iniTime = File.GetLastWriteTimeUtc(iniPath); Log("settings saved to " + iniPath); }
            catch (Exception ex) { Log("could not save settings: " + ex.Message); }
        }

        static Color Col(string v, Color d)
        {
            v = v.Trim();
            if (v.Length == 0) return d;
            try
            {
                if (v[0] == '#' && (v.Length == 7 || v.Length == 9))
                {
                    int n = int.Parse(v.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    return v.Length == 7 ? Color.FromArgb(255, (n >> 16) & 255, (n >> 8) & 255, n & 255) : Color.FromArgb(n);
                }
                if (v.IndexOf(',') >= 0)
                {
                    string[] p = v.Split(',');
                    int[] c = new int[p.Length];
                    for (int i = 0; i < p.Length; i++) c[i] = Math.Max(0, Math.Min(255, int.Parse(p[i].Trim(), CultureInfo.InvariantCulture)));
                    if (c.Length == 4) return Color.FromArgb(c[0], c[1], c[2], c[3]);
                    if (c.Length == 3) return Color.FromArgb(255, c[0], c[1], c[2]);
                    return d;
                }
                Color k = Color.FromName(v.Replace(" ", ""));
                return k.IsKnownColor ? k : d;
            }
            catch { return d; }
        }
        readonly Dictionary<string, string> rawColours = new Dictionary<string, string>();
        // a colour is written back the way it was typed (a name stays a name) unless it was changed
        string ColOut(string key, Color c)
        {
            string raw;
            if (rawColours.TryGetValue(key, out raw) && Col(raw, Color.Empty).ToArgb() == c.ToArgb() && raw.Length > 0) return raw;
            return ColText(c);
        }
        static string ColText(Color c) { return c.A == 255 ? c.R + "," + c.G + "," + c.B : c.A + "," + c.R + "," + c.G + "," + c.B; }

        static string Fmt(float f) { return f.ToString("0.###", CultureInfo.InvariantCulture); }
        static bool Bool(string v, bool d)
        {
            v = v.ToLowerInvariant();
            if (v == "1" || v == "true" || v == "yes" || v == "on") return true;
            if (v == "0" || v == "false" || v == "no" || v == "off") return false;
            return d;
        }
        static float Num(string v, float d, float lo, float hi)
        {
            float f;
            if (!float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) return d;
            return Math.Max(lo, Math.Min(hi, f));
        }
        static Keys KeyOf(string v, Keys d)
        {
            v = (v ?? "").Trim();
            if (v.Equals("none", StringComparison.OrdinalIgnoreCase)) return Keys.None;
            Keys mods = Keys.None, code = Keys.None;
            foreach (string part0 in v.Split('+'))
            {
                string part = part0.Trim().ToLowerInvariant();
                if (part == "") continue;
                if (part == "shift") mods |= Keys.Shift;
                else if (part == "ctrl" || part == "control") mods |= Keys.Control;
                else if (part == "alt") mods |= Keys.Alt;
                else { Keys k; if (!Enum.TryParse<Keys>(part0.Trim(), true, out k)) return d; code = k; }
            }
            return code == Keys.None ? d : (code | mods);
        }
    }
}
