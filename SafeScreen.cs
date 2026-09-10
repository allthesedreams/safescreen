using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("AWAKE SafeScreen")]
[assembly: AssemblyDescription("Keeps damaged screen edges outside the Windows work area.")]
[assembly: AssemblyCompany("AWAKE")]
[assembly: AssemblyProduct("AWAKE SafeScreen")]
[assembly: AssemblyVersion("1.4.1.0")]

namespace Awake.SafeScreen
{
    internal static class SafeScreenSettings
    {
        internal const int DefaultLeftMargin = 80;
        internal const int DefaultTopMargin = 40;
        internal const int DefaultBottomMargin = 225;

        internal const int PanelWidth = 1920;
        internal const int PanelHeight = 1200;
        internal const int ReducedWidth = 1680;
        internal const int ReducedHeight = 1050;

        internal const string RunValueName = "AWAKE SafeScreen";
        internal const string RegistryPath = @"Software\AWAKE\SafeScreen";
        internal const string MutexName = @"Local\AWAKE.SafeScreen.v1";
        internal const string StopEventName = @"Local\AWAKE.SafeScreen.Stop.v1";
        internal const string ReloadEventName = @"Local\AWAKE.SafeScreen.Reload.v1";
        internal const string ShowSettingsEventName = @"Local\AWAKE.SafeScreen.ShowSettings.v1";
    }

    internal static class SafeScreenBrand
    {
        private static Icon appIcon;

        internal static Icon AppIcon
        {
            get
            {
                if (appIcon == null)
                {
                    try
                    {
                        Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                            "Awake.SafeScreen.SafeScreen.ico");
                        if (stream != null)
                        {
                            appIcon = new Icon(stream);
                        }
                    }
                    catch (Exception)
                    {
                        appIcon = null;
                    }

                    if (appIcon == null)
                    {
                        appIcon = SystemIcons.Application;
                    }
                }

                return appIcon;
            }
        }

        internal static Bitmap LoadLogo()
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                    "Awake.SafeScreen.SafeScreenLogo.png"))
                {
                    if (stream != null)
                    {
                        using (Bitmap source = new Bitmap(stream))
                        {
                            return new Bitmap(source);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return SafeScreenBrand.AppIcon.ToBitmap();
        }
    }

    internal static class SafeScreenTheme
    {
        internal static readonly Color Canvas = Color.FromArgb(8, 10, 13);
        internal static readonly Color Panel = Color.FromArgb(20, 24, 29);
        internal static readonly Color PanelRaised = Color.FromArgb(28, 34, 41);
        internal static readonly Color Text = Color.FromArgb(246, 242, 233);
        internal static readonly Color Muted = Color.FromArgb(164, 174, 184);
        internal static readonly Color Cyan = Color.FromArgb(38, 205, 240);
        internal static readonly Color CyanBright = Color.FromArgb(143, 235, 252);
        internal static readonly Color CyanDeep = Color.FromArgb(16, 99, 120);
        internal static readonly Color Scarlet = Color.FromArgb(210, 45, 68);
        internal static readonly Color Burgundy = Color.FromArgb(92, 19, 34);
        internal static readonly Color Border = Color.FromArgb(62, 72, 82);

        internal static Font DisplayFont(float size, FontStyle style)
        {
            return new Font("Segoe UI Variable Display", size, style);
        }

        internal static Font BodyFont(float size, FontStyle style)
        {
            return new Font("Segoe UI", size, style);
        }
    }

    internal sealed class RoundedPanel : Panel
    {
        internal int CornerRadius { get; set; }

        internal RoundedPanel()
        {
            CornerRadius = 20;
            DoubleBuffered = true;
        }

        protected override void OnResize(EventArgs eventArgs)
        {
            base.OnResize(eventArgs);
            if (Width < 2 || Height < 2)
            {
                return;
            }

            int radius = Math.Max(4, Math.Min(CornerRadius, Math.Min(Width, Height) / 2));
            using (GraphicsPath path = new GraphicsPath())
            {
                int diameter = radius * 2;
                path.AddArc(0, 0, diameter, diameter, 180, 90);
                path.AddArc(Width - diameter - 1, 0, diameter, diameter, 270, 90);
                path.AddArc(Width - diameter - 1, Height - diameter - 1, diameter, diameter, 0, 90);
                path.AddArc(0, Height - diameter - 1, diameter, diameter, 90, 90);
                path.CloseFigure();
                Region oldRegion = Region;
                Region = new Region(path);
                if (oldRegion != null)
                {
                    oldRegion.Dispose();
                }
            }
        }
    }

    internal sealed class GuardSettings
    {
        internal GuardSettings(bool enabled, int left, int top, int bottom)
        {
            Enabled = enabled;
            Left = left;
            Top = top;
            Bottom = bottom;
        }

        internal bool Enabled { get; set; }
        internal int Left { get; set; }
        internal int Top { get; set; }
        internal int Bottom { get; set; }

        internal GuardSettings Clone()
        {
            return new GuardSettings(Enabled, Left, Top, Bottom);
        }

        internal bool HasSameMargins(GuardSettings other)
        {
            return other != null && Left == other.Left && Top == other.Top && Bottom == other.Bottom;
        }

        internal static GuardSettings Load()
        {
            GuardSettings defaults = new GuardSettings(
                true,
                SafeScreenSettings.DefaultLeftMargin,
                SafeScreenSettings.DefaultTopMargin,
                SafeScreenSettings.DefaultBottomMargin);

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    SafeScreenSettings.RegistryPath,
                    false))
                {
                    if (key == null)
                    {
                        return defaults;
                    }

                    return new GuardSettings(
                        ReadInteger(key, "Enabled", defaults.Enabled ? 1 : 0) != 0,
                        ReadInteger(key, "Left", defaults.Left),
                        ReadInteger(key, "Top", defaults.Top),
                        ReadInteger(key, "Bottom", defaults.Bottom));
                }
            }
            catch (Exception exception)
            {
                SafeScreenLog.Write("Settings load failed; defaults used: " + exception.Message);
                return defaults;
            }
        }

        internal void Save()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                SafeScreenSettings.RegistryPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Could not open the SafeScreen settings key.");
                }

                key.SetValue("Enabled", Enabled ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("Left", Left, RegistryValueKind.DWord);
                key.SetValue("Top", Top, RegistryValueKind.DWord);
                key.SetValue("Bottom", Bottom, RegistryValueKind.DWord);
            }
        }

        internal static GuardSettings Normalize(GuardSettings source)
        {
            Rectangle monitor = Screen.PrimaryScreen.Bounds;
            int minimumWidth = Math.Min(640, monitor.Width);
            int minimumHeight = Math.Min(480, monitor.Height);
            int left = Clamp(source.Left, 0, Math.Max(0, monitor.Width - minimumWidth));
            int top = Clamp(source.Top, 0, Math.Max(0, monitor.Height - minimumHeight));
            int bottom = Clamp(
                source.Bottom,
                0,
                Math.Max(0, monitor.Height - minimumHeight - top));
            return new GuardSettings(source.Enabled, left, top, bottom);
        }

        private static int ReadInteger(RegistryKey key, string name, int fallback)
        {
            object value = key.GetValue(name, fallback);
            try
            {
                return Convert.ToInt32(value);
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    internal sealed class GuardProfile
    {
        internal GuardProfile(string name, int left, int top, int bottom)
        {
            Name = name;
            Settings = new GuardSettings(true, left, top, bottom);
        }

        internal string Name { get; private set; }
        internal GuardSettings Settings { get; private set; }

        internal static readonly GuardProfile[] All = new GuardProfile[]
        {
            new GuardProfile("Мой экран — 80 / 40 / 225", 80, 40, 225),
            new GuardProfile("Только низ — 0 / 0 / 225", 0, 0, 225),
            new GuardProfile("Лёгкий — 40 / 20 / 150", 40, 20, 150),
            new GuardProfile("Строгий — 120 / 60 / 275", 120, 60, 275)
        };
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (HasArgument(args, "--install"))
                {
                    InstallAutostart();
                    return 0;
                }

                if (HasArgument(args, "--uninstall"))
                {
                    RemoveAutostart();
                    SignalRunningInstance();
                    return 0;
                }

                if (HasArgument(args, "--stop"))
                {
                    return SignalRunningInstance() ? 0 : 1;
                }

                bool showSettingsAtStart = HasArgument(args, "--show-settings");
                bool configurationChanged = false;
                GuardSettings requestedSettings = GuardSettings.Load();

                if (HasArgument(args, "--enable"))
                {
                    requestedSettings.Enabled = true;
                    configurationChanged = true;
                }
                else if (HasArgument(args, "--disable"))
                {
                    requestedSettings.Enabled = false;
                    configurationChanged = true;
                }

                GuardSettings commandSettings;
                if (TryReadSetCommand(args, requestedSettings.Enabled, out commandSettings))
                {
                    requestedSettings = commandSettings;
                    requestedSettings.Enabled = true;
                    configurationChanged = true;
                }

                requestedSettings = GuardSettings.Normalize(requestedSettings);
                if (configurationChanged)
                {
                    requestedSettings.Save();
                    if (SignalEvent(SafeScreenSettings.ReloadEventName))
                    {
                        return 0;
                    }
                }

                if (showSettingsAtStart && SignalEvent(SafeScreenSettings.ShowSettingsEventName))
                {
                    return 0;
                }

                NativeMethods.TryEnablePerMonitorDpiAwareness();

                bool createdNew;
                using (Mutex mutex = new Mutex(true, SafeScreenSettings.MutexName, out createdNew))
                {
                    if (!createdNew)
                    {
                        return 0;
                    }

                    if (requestedSettings.Enabled)
                    {
                        DisplayMode.TryPersistKnownPanelMode();
                    }

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    using (EventWaitHandle stopEvent = new EventWaitHandle(
                        false,
                        EventResetMode.AutoReset,
                        SafeScreenSettings.StopEventName))
                    using (EventWaitHandle reloadEvent = new EventWaitHandle(
                        false,
                        EventResetMode.AutoReset,
                        SafeScreenSettings.ReloadEventName))
                    using (EventWaitHandle showSettingsEvent = new EventWaitHandle(
                        false,
                        EventResetMode.AutoReset,
                        SafeScreenSettings.ShowSettingsEventName))
                    {
                        SafeScreenLog.Write("SafeScreen started.");
                        Application.Run(new SafeScreenContext(
                            stopEvent,
                            reloadEvent,
                            showSettingsEvent,
                            requestedSettings,
                            showSettingsAtStart));
                    }
                }

                SafeScreenLog.Write("SafeScreen stopped.");
                return 0;
            }
            catch (Exception exception)
            {
                SafeScreenLog.Write("Fatal: " + exception);
                return 2;
            }
        }

        private static bool HasArgument(string[] args, string expected)
        {
            int index;
            for (index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadSetCommand(
            string[] args,
            bool enabled,
            out GuardSettings settings)
        {
            settings = null;
            int index;
            for (index = 0; index < args.Length; index++)
            {
                if (!string.Equals(args[index], "--set", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index + 3 >= args.Length)
                {
                    throw new ArgumentException("--set requires left, top and bottom pixel values.");
                }

                int left;
                int top;
                int bottom;
                if (!int.TryParse(args[index + 1], out left) ||
                    !int.TryParse(args[index + 2], out top) ||
                    !int.TryParse(args[index + 3], out bottom) ||
                    left < 0 || top < 0 || bottom < 0)
                {
                    throw new ArgumentException("SafeScreen margins must be non-negative integers.");
                }

                settings = new GuardSettings(enabled, left, top, bottom);
                return true;
            }

            return false;
        }

        internal static bool IsAutostartEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (key == null)
                {
                    return false;
                }

                string current = key.GetValue(SafeScreenSettings.RunValueName) as string;
                string expected = "\"" + Assembly.GetExecutingAssembly().Location + "\"";
                return string.Equals(current, expected, StringComparison.OrdinalIgnoreCase);
            }
        }

        internal static void InstallAutostart()
        {
            string executable = Assembly.GetExecutingAssembly().Location;
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Could not open the current-user Run registry key.");
                }

                key.SetValue(
                    SafeScreenSettings.RunValueName,
                    "\"" + executable + "\"",
                    RegistryValueKind.String);
            }

            SafeScreenLog.Write("Autostart installed: " + executable);
        }

        internal static void RemoveAutostart()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run",
                true))
            {
                if (key != null)
                {
                    key.DeleteValue(SafeScreenSettings.RunValueName, false);
                }
            }

            SafeScreenLog.Write("Autostart removed.");
        }

        private static bool SignalRunningInstance()
        {
            return SignalEvent(SafeScreenSettings.StopEventName);
        }

        private static bool SignalEvent(string eventName)
        {
            try
            {
                using (EventWaitHandle commandEvent = EventWaitHandle.OpenExisting(eventName))
                {
                    commandEvent.Set();
                    return true;
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
        }
    }

    internal sealed class SafeScreenContext : ApplicationContext
    {
        private readonly EventWaitHandle stopEvent;
        private readonly EventWaitHandle reloadEvent;
        private readonly EventWaitHandle showSettingsEvent;
        private readonly EdgeBar[] bars;
        private readonly System.Windows.Forms.Timer layoutTimer;
        private readonly System.Windows.Forms.Timer watchdogTimer;
        private readonly NotifyIcon trayIcon;
        private readonly MenuItem enabledMenuItem;
        private readonly MenuItem autostartMenuItem;
        private readonly MenuItem statusMenuItem;
        private readonly MenuItem customMenuItem;
        private readonly MenuItem[] profileMenuItems;
        private GuardSettings settings;
        private bool layoutInProgress;
        private bool reRegisterRequested;
        private bool closing;

        internal SafeScreenContext(
            EventWaitHandle stopEvent,
            EventWaitHandle reloadEvent,
            EventWaitHandle showSettingsEvent,
            GuardSettings initialSettings,
            bool showSettingsAtStart)
        {
            this.stopEvent = stopEvent;
            this.reloadEvent = reloadEvent;
            this.showSettingsEvent = showSettingsEvent;
            settings = GuardSettings.Normalize(initialSettings);
            settings.Save();

            uint callbackMessage = NativeMethods.RegisterWindowMessage(
                "AWAKE.SafeScreen.AppBar.Callback.v1");
            uint taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");
            if (callbackMessage == 0 || taskbarCreatedMessage == 0)
            {
                throw new InvalidOperationException("Could not register SafeScreen window messages.");
            }

            layoutTimer = new System.Windows.Forms.Timer();
            layoutTimer.Interval = 120;
            layoutTimer.Tick += OnLayoutTimer;

            watchdogTimer = new System.Windows.Forms.Timer();
            watchdogTimer.Interval = 1000;
            watchdogTimer.Tick += OnWatchdogTimer;

            bars = new EdgeBar[]
            {
                new EdgeBar(AppBarEdge.Top, settings.Top, callbackMessage,
                    taskbarCreatedMessage, SchedulePosition, ScheduleReRegister),
                new EdgeBar(AppBarEdge.Left, settings.Left, callbackMessage,
                    taskbarCreatedMessage, SchedulePosition, ScheduleReRegister),
                new EdgeBar(AppBarEdge.Bottom, settings.Bottom, callbackMessage,
                    taskbarCreatedMessage, SchedulePosition, ScheduleReRegister)
            };

            ContextMenu trayMenu = new ContextMenu();
            enabledMenuItem = new MenuItem("Ограничения включены", OnToggleEnabled);
            statusMenuItem = new MenuItem();
            statusMenuItem.Enabled = false;
            trayMenu.MenuItems.Add(enabledMenuItem);
            trayMenu.MenuItems.Add(statusMenuItem);
            trayMenu.MenuItems.Add("-");

            profileMenuItems = new MenuItem[GuardProfile.All.Length];
            int index;
            for (index = 0; index < GuardProfile.All.Length; index++)
            {
                int profileIndex = index;
                MenuItem item = new MenuItem(
                    GuardProfile.All[index].Name,
                    delegate { SelectProfile(profileIndex); });
                item.RadioCheck = true;
                profileMenuItems[index] = item;
            }

            trayMenu.MenuItems.Add(new MenuItem("Профили", profileMenuItems));
            customMenuItem = new MenuItem("Выбрать область на полном экране…", OnShowSettings);
            customMenuItem.RadioCheck = true;
            trayMenu.MenuItems.Add(customMenuItem);
            trayMenu.MenuItems.Add(new MenuItem("Компактная настройка…", OnShowCompactSettings));
            trayMenu.MenuItems.Add("-");
            autostartMenuItem = new MenuItem("Запускать вместе с Windows", OnToggleAutostart);
            trayMenu.MenuItems.Add(autostartMenuItem);
            trayMenu.MenuItems.Add(new MenuItem(
                "Восстановить область сейчас",
                delegate { ReRegisterAndPosition(); }));
            trayMenu.MenuItems.Add(new MenuItem(
                "Выход до следующего входа",
                delegate { Shutdown(); }));

            trayIcon = new NotifyIcon();
            trayIcon.Icon = SafeScreenBrand.AppIcon;
            trayIcon.ContextMenu = trayMenu;
            trayIcon.DoubleClick += OnShowSettings;
            trayIcon.Visible = true;

            ApplySettings(settings, false);
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.SessionEnding += OnSessionEnding;
            watchdogTimer.Start();

            if (showSettingsAtStart)
            {
                ShowSettingsDialog();
            }
        }

        private void OnToggleEnabled(object sender, EventArgs eventArgs)
        {
            GuardSettings updated = settings.Clone();
            updated.Enabled = !settings.Enabled;
            ApplySettings(updated, true);
        }

        private void OnToggleAutostart(object sender, EventArgs eventArgs)
        {
            if (Program.IsAutostartEnabled())
            {
                Program.RemoveAutostart();
            }
            else
            {
                Program.InstallAutostart();
            }

            UpdateTrayMenu();
        }

        private void SelectProfile(int profileIndex)
        {
            GuardSettings updated = GuardProfile.All[profileIndex].Settings.Clone();
            updated.Enabled = true;
            ApplySettings(updated, true);
        }

        private void OnShowSettings(object sender, EventArgs eventArgs)
        {
            ShowSettingsDialog();
        }

        private void OnShowCompactSettings(object sender, EventArgs eventArgs)
        {
            ShowCompactSettingsDialog();
        }

        private void ShowSettingsDialog()
        {
            if (closing)
            {
                return;
            }

            try
            {
                using (FullScreenPickerForm dialog = new FullScreenPickerForm(settings))
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        ApplySettings(dialog.SelectedSettings, true);
                    }
                }
            }
            catch (Exception exception)
            {
                SafeScreenLog.Write("Full-screen picker failed: " + exception);
                ShowCompactSettingsDialog();
            }
        }

        private void ShowCompactSettingsDialog()
        {
            if (closing)
            {
                return;
            }

            using (SettingsForm dialog = new SettingsForm(settings))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    ApplySettings(dialog.SelectedSettings, true);
                }
            }
        }

        private void ApplySettings(GuardSettings requested, bool save)
        {
            GuardSettings normalized = GuardSettings.Normalize(requested);
            bool enabling = normalized.Enabled && (settings == null || !settings.Enabled);
            settings = normalized;
            if (save)
            {
                settings.Save();
            }

            if (settings.Enabled)
            {
                if (enabling)
                {
                    DisplayMode.TryPersistKnownPanelMode();
                }

                ReRegisterAndPosition();
            }
            else
            {
                DeactivateBars();
            }

            UpdateTrayMenu();
            SafeScreenLog.Write(
                "Settings applied: enabled=" + settings.Enabled +
                ", left=" + settings.Left +
                ", top=" + settings.Top +
                ", bottom=" + settings.Bottom + ".");
        }

        private void UpdateTrayMenu()
        {
            enabledMenuItem.Checked = settings.Enabled;
            autostartMenuItem.Checked = Program.IsAutostartEnabled();
            enabledMenuItem.Text = settings.Enabled
                ? "Ограничения включены"
                : "Ограничения выключены";
            statusMenuItem.Text = string.Format(
                settings.Enabled ? "Активно: Л {0} · В {1} · Н {2} px" : "Сохранено: Л {0} · В {1} · Н {2} px",
                settings.Left,
                settings.Top,
                settings.Bottom);

            bool matchedProfile = false;
            int index;
            for (index = 0; index < GuardProfile.All.Length; index++)
            {
                bool matched = settings.HasSameMargins(GuardProfile.All[index].Settings);
                profileMenuItems[index].Checked = matched;
                matchedProfile = matchedProfile || matched;
            }

            customMenuItem.Checked = !matchedProfile;
            trayIcon.Text = string.Format(
                "SafeScreen: {0}; Л{1} В{2} Н{3}",
                settings.Enabled ? "Вкл" : "Выкл",
                settings.Left,
                settings.Top,
                settings.Bottom);
        }

        private void OnLayoutTimer(object sender, EventArgs eventArgs)
        {
            layoutTimer.Stop();
            if (!settings.Enabled)
            {
                return;
            }

            if (reRegisterRequested)
            {
                reRegisterRequested = false;
                ReRegisterAndPosition();
            }
            else
            {
                PositionIfNeeded();
            }
        }

        private void OnWatchdogTimer(object sender, EventArgs eventArgs)
        {
            if (stopEvent.WaitOne(0))
            {
                Shutdown();
                return;
            }

            if (reloadEvent.WaitOne(0))
            {
                ApplySettings(GuardSettings.Load(), false);
            }

            if (showSettingsEvent.WaitOne(0))
            {
                ShowSettingsDialog();
            }

            if (settings.Enabled && !NativeMethods.IsPrimaryWorkAreaSafe(settings))
            {
                SafeScreenLog.Write("Watchdog detected an unsafe work area; repairing.");
                ReRegisterAndPosition();
            }
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs eventArgs)
        {
            if (settings.Enabled)
            {
                QueueOnUiThread(ScheduleReRegister);
            }
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs eventArgs)
        {
            QueueOnUiThread(Shutdown);
        }

        private void QueueOnUiThread(Action action)
        {
            if (closing || bars.Length == 0 || bars[0].IsDisposed)
            {
                return;
            }

            try
            {
                if (bars[0].InvokeRequired)
                {
                    bars[0].BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (InvalidOperationException)
            {
                // The UI thread is already shutting down.
            }
        }

        private void SchedulePosition()
        {
            if (closing || layoutInProgress || !settings.Enabled)
            {
                return;
            }

            layoutTimer.Stop();
            layoutTimer.Start();
        }

        private void ScheduleReRegister()
        {
            if (closing || layoutInProgress || !settings.Enabled)
            {
                return;
            }

            reRegisterRequested = true;
            layoutTimer.Stop();
            layoutTimer.Start();
        }

        private void PositionIfNeeded()
        {
            if (settings.Enabled && !NativeMethods.IsPrimaryWorkAreaSafe(settings))
            {
                PositionAllBars();
            }
        }

        private void ReRegisterAndPosition()
        {
            if (closing || layoutInProgress || !settings.Enabled)
            {
                return;
            }

            layoutInProgress = true;
            layoutTimer.Stop();
            try
            {
                ConfigureBarThicknesses();
                int index;
                for (index = bars.Length - 1; index >= 0; index--)
                {
                    bars[index].UnregisterAppBar();
                }

                for (index = 0; index < bars.Length; index++)
                {
                    if (bars[index].Thickness > 0)
                    {
                        bars[index].Show();
                        bars[index].RegisterAppBar();
                    }
                    else
                    {
                        bars[index].Hide();
                    }
                }

                PositionAllBarsCore();
            }
            finally
            {
                layoutInProgress = false;
            }
        }

        private void PositionAllBars()
        {
            if (closing || layoutInProgress || !settings.Enabled)
            {
                return;
            }

            layoutInProgress = true;
            layoutTimer.Stop();
            try
            {
                PositionAllBarsCore();
            }
            finally
            {
                layoutInProgress = false;
            }
        }

        private void PositionAllBarsCore()
        {
            int index;
            for (index = 0; index < bars.Length; index++)
            {
                if (bars[index].Thickness > 0)
                {
                    bars[index].PositionAppBar();
                }
            }
        }

        private void ConfigureBarThicknesses()
        {
            int index;
            for (index = 0; index < bars.Length; index++)
            {
                if (bars[index].Edge == AppBarEdge.Left)
                {
                    bars[index].SetThickness(settings.Left);
                }
                else if (bars[index].Edge == AppBarEdge.Top)
                {
                    bars[index].SetThickness(settings.Top);
                }
                else if (bars[index].Edge == AppBarEdge.Bottom)
                {
                    bars[index].SetThickness(settings.Bottom);
                }
            }
        }

        private void DeactivateBars()
        {
            layoutTimer.Stop();
            reRegisterRequested = false;
            bool previousLayoutState = layoutInProgress;
            layoutInProgress = true;
            try
            {
                int index;
                for (index = bars.Length - 1; index >= 0; index--)
                {
                    bars[index].UnregisterAppBar();
                }

                for (index = 0; index < bars.Length; index++)
                {
                    bars[index].Hide();
                }
            }
            finally
            {
                layoutInProgress = previousLayoutState;
            }
        }

        private void Shutdown()
        {
            if (closing)
            {
                return;
            }

            closing = true;
            layoutTimer.Stop();
            watchdogTimer.Stop();
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.SessionEnding -= OnSessionEnding;

            int index;
            for (index = bars.Length - 1; index >= 0; index--)
            {
                bars[index].UnregisterAppBar();
            }

            trayIcon.Visible = false;
            trayIcon.Dispose();

            for (index = 0; index < bars.Length; index++)
            {
                bars[index].Close();
                bars[index].Dispose();
            }

            layoutTimer.Dispose();
            watchdogTimer.Dispose();
            ExitThread();
        }
    }

    internal sealed class FullScreenPickerForm : Form
    {
        private const int HitDistance = 26;
        private const int Snap = 5;
        private readonly Rectangle monitor;
        private readonly RoundedPanel hud;
        private readonly Label areaLabel;
        private readonly Label edgeLabel;
        private readonly Button leftEdgeButton;
        private readonly Button topEdgeButton;
        private readonly Button bottomEdgeButton;
        private PickerEdge activeEdge;
        private bool dragging;
        private int leftMargin;
        private int topMargin;
        private int bottomMargin;

        internal FullScreenPickerForm(GuardSettings current)
        {
            monitor = Screen.PrimaryScreen.Bounds;
            leftMargin = current.Left;
            topMargin = current.Top;
            bottomMargin = current.Bottom;
            activeEdge = PickerEdge.Left;

            Text = "SafeScreen — полноэкранный выбор области";
            Icon = SafeScreenBrand.AppIcon;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = SafeScreenTheme.Canvas;
            ForeColor = SafeScreenTheme.Text;
            FormBorderStyle = FormBorderStyle.None;
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Bounds = monitor;
            Opacity = 0.94;
            DoubleBuffered = true;
            Cursor = Cursors.Cross;

            hud = new RoundedPanel();
            hud.Size = new Size(620, 196);
            hud.CornerRadius = 22;
            hud.BackColor = SafeScreenTheme.Panel;
            hud.Padding = new Padding(20, 14, 20, 14);
            Controls.Add(hud);

            PictureBox logo = new PictureBox();
            logo.Image = SafeScreenBrand.LoadLogo();
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.SetBounds(20, 17, 54, 54);
            hud.Controls.Add(logo);

            Label title = new Label();
            title.AutoSize = true;
            title.Font = SafeScreenTheme.DisplayFont(16, FontStyle.Bold);
            title.ForeColor = SafeScreenTheme.Text;
            title.Text = "Выбери видимую область";
            title.Location = new Point(86, 15);
            hud.Controls.Add(title);

            Label hint = new Label();
            hint.AutoSize = true;
            hint.Font = SafeScreenTheme.BodyFont(9, FontStyle.Regular);
            hint.ForeColor = SafeScreenTheme.Muted;
            hint.Text = "Тяни голубую рамку. Правый верхний угол закреплён.";
            hint.Location = new Point(88, 47);
            hud.Controls.Add(hint);

            areaLabel = new Label();
            areaLabel.AutoSize = true;
            areaLabel.Font = SafeScreenTheme.DisplayFont(13, FontStyle.Bold);
            areaLabel.ForeColor = SafeScreenTheme.CyanBright;
            areaLabel.Location = new Point(22, 86);
            hud.Controls.Add(areaLabel);

            edgeLabel = new Label();
            edgeLabel.AutoSize = true;
            edgeLabel.Font = SafeScreenTheme.BodyFont(8, FontStyle.Regular);
            edgeLabel.ForeColor = SafeScreenTheme.Muted;
            edgeLabel.Location = new Point(23, 116);
            hud.Controls.Add(edgeLabel);

            leftEdgeButton = CreateHudEdgeButton("Слева", PickerEdge.Left);
            leftEdgeButton.Location = new Point(22, 148);
            hud.Controls.Add(leftEdgeButton);
            topEdgeButton = CreateHudEdgeButton("Сверху", PickerEdge.Top);
            topEdgeButton.Location = new Point(130, 148);
            hud.Controls.Add(topEdgeButton);
            bottomEdgeButton = CreateHudEdgeButton("Снизу", PickerEdge.Bottom);
            bottomEdgeButton.Location = new Point(238, 148);
            hud.Controls.Add(bottomEdgeButton);

            Button save = CreateHudButton("Сохранить", true);
            save.Location = new Point(484, 86);
            save.Click += OnSave;
            hud.Controls.Add(save);

            Button cancel = CreateHudButton("Отмена", false);
            cancel.Location = new Point(484, 137);
            cancel.Click += OnCancel;
            hud.Controls.Add(cancel);

            Shown += OnShown;
            UpdateLabels();
        }

        internal GuardSettings SelectedSettings { get; private set; }

        protected override bool ShowWithoutActivation
        {
            get { return false; }
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            int step = (keyData & Keys.Shift) == Keys.Shift ? 20 : 5;
            if (key == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }
            if (key == Keys.Enter)
            {
                SaveSelection();
                return true;
            }
            if (key == Keys.Space)
            {
                activeEdge = activeEdge == PickerEdge.Left ? PickerEdge.Top :
                    activeEdge == PickerEdge.Top ? PickerEdge.Bottom : PickerEdge.Left;
                UpdateLabels();
                Invalidate();
                return true;
            }
            if (activeEdge == PickerEdge.Left && key == Keys.Left)
            {
                leftMargin -= step;
            }
            else if (activeEdge == PickerEdge.Left && key == Keys.Right)
            {
                leftMargin += step;
            }
            else if (activeEdge == PickerEdge.Top && key == Keys.Up)
            {
                topMargin -= step;
            }
            else if (activeEdge == PickerEdge.Top && key == Keys.Down)
            {
                topMargin += step;
            }
            else if (activeEdge == PickerEdge.Bottom && key == Keys.Up)
            {
                bottomMargin += step;
            }
            else if (activeEdge == PickerEdge.Bottom && key == Keys.Down)
            {
                bottomMargin -= step;
            }
            else
            {
                return base.ProcessCmdKey(ref message, keyData);
            }
            NormalizeMargins();
            UpdateLabels();
            UpdateHudBounds();
            Invalidate();
            return true;
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            Graphics graphics = eventArgs.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle full = ClientRectangle;
            Rectangle safe = GetSafeRectangle();

            using (SolidBrush excluded = new SolidBrush(SafeScreenTheme.Canvas))
            {
                graphics.FillRectangle(excluded, full);
            }
            DrawDamagePattern(graphics, full, safe);
            using (SolidBrush safeFill = new SolidBrush(Color.FromArgb(18, 28, 33)))
            {
                graphics.FillRectangle(safeFill, safe);
            }
            using (Pen border = new Pen(SafeScreenTheme.Cyan, 5))
            {
                graphics.DrawRectangle(border, safe.Left + 2, safe.Top + 2,
                    Math.Max(1, safe.Width - 5), Math.Max(1, safe.Height - 5));
            }

            DrawHandle(graphics, safe, PickerEdge.Left);
            DrawHandle(graphics, safe, PickerEdge.Top);
            DrawHandle(graphics, safe, PickerEdge.Bottom);

            DrawExcludedLabels(graphics, safe);

            using (Font labelFont = SafeScreenTheme.BodyFont(10, FontStyle.Bold))
            using (SolidBrush labelBrush = new SolidBrush(SafeScreenTheme.Text))
            {
                graphics.DrawString("ВИДИМАЯ РАБОЧАЯ ОБЛАСТЬ", labelFont, labelBrush,
                    safe.Left + 24, safe.Bottom - 48);
            }
        }

        protected override void OnMouseDown(MouseEventArgs eventArgs)
        {
            base.OnMouseDown(eventArgs);
            if (eventArgs.Button != MouseButtons.Left)
            {
                return;
            }
            PickerEdge? hit = HitTest(eventArgs.Location);
            if (!hit.HasValue)
            {
                return;
            }
            activeEdge = hit.Value;
            dragging = true;
            Capture = true;
            UpdateFromPoint(eventArgs.Location);
        }

        protected override void OnMouseMove(MouseEventArgs eventArgs)
        {
            base.OnMouseMove(eventArgs);
            if (dragging)
            {
                UpdateFromPoint(eventArgs.Location);
                return;
            }
            PickerEdge? hit = HitTest(eventArgs.Location);
            Cursor = !hit.HasValue ? Cursors.Cross :
                hit.Value == PickerEdge.Left ? Cursors.SizeWE : Cursors.SizeNS;
        }

        protected override void OnMouseUp(MouseEventArgs eventArgs)
        {
            base.OnMouseUp(eventArgs);
            dragging = false;
            Capture = false;
        }

        protected override void OnResize(EventArgs eventArgs)
        {
            base.OnResize(eventArgs);
            UpdateHudBounds();
        }

        private static Button CreateHudButton(string text, bool primary)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(112, 42);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Font = SafeScreenTheme.BodyFont(9, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            if (primary)
            {
                button.BackColor = SafeScreenTheme.Cyan;
                button.ForeColor = SafeScreenTheme.Canvas;
                button.FlatAppearance.BorderColor = SafeScreenTheme.CyanBright;
            }
            else
            {
                button.BackColor = SafeScreenTheme.PanelRaised;
                button.ForeColor = SafeScreenTheme.Text;
                button.FlatAppearance.BorderColor = SafeScreenTheme.Border;
            }
            return button;
        }

        private Button CreateHudEdgeButton(string text, PickerEdge edge)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(98, 32);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Font = SafeScreenTheme.BodyFont(8, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.Tag = edge;
            button.Click += delegate
            {
                activeEdge = edge;
                UpdateLabels();
                Invalidate();
                Focus();
            };
            return button;
        }

        private void OnShown(object sender, EventArgs eventArgs)
        {
            Bounds = monitor;
            UpdateHudBounds();
            Activate();
            Focus();
        }

        private void OnSave(object sender, EventArgs eventArgs)
        {
            SaveSelection();
        }

        private void OnCancel(object sender, EventArgs eventArgs)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void SaveSelection()
        {
            SelectedSettings = new GuardSettings(true, leftMargin, topMargin, bottomMargin);
            DialogResult = DialogResult.OK;
            Close();
        }

        private Rectangle GetSafeRectangle()
        {
            return Rectangle.FromLTRB(
                leftMargin,
                topMargin,
                monitor.Width,
                Math.Max(topMargin + 1, monitor.Height - bottomMargin));
        }

        private PickerEdge? HitTest(Point point)
        {
            Rectangle safe = GetSafeRectangle();
            if (point.Y >= safe.Top - HitDistance && point.Y <= safe.Bottom + HitDistance &&
                Math.Abs(point.X - safe.Left) <= HitDistance)
            {
                return PickerEdge.Left;
            }
            if (point.X >= safe.Left - HitDistance && point.X <= safe.Right &&
                Math.Abs(point.Y - safe.Top) <= HitDistance)
            {
                return PickerEdge.Top;
            }
            if (point.X >= safe.Left - HitDistance && point.X <= safe.Right &&
                Math.Abs(point.Y - safe.Bottom) <= HitDistance)
            {
                return PickerEdge.Bottom;
            }
            return null;
        }

        private void UpdateFromPoint(Point point)
        {
            if (activeEdge == PickerEdge.Left)
            {
                leftMargin = SnapValue(point.X);
            }
            else if (activeEdge == PickerEdge.Top)
            {
                topMargin = SnapValue(point.Y);
            }
            else
            {
                bottomMargin = SnapValue(monitor.Height - point.Y);
            }
            NormalizeMargins();
            UpdateLabels();
            UpdateHudBounds();
            Invalidate();
        }

        private void NormalizeMargins()
        {
            int minimumWidth = Math.Min(640, monitor.Width);
            int minimumHeight = Math.Min(480, monitor.Height);
            leftMargin = Math.Max(0, Math.Min(monitor.Width - minimumWidth, leftMargin));
            topMargin = Math.Max(0, Math.Min(monitor.Height - minimumHeight, topMargin));
            bottomMargin = Math.Max(0,
                Math.Min(monitor.Height - minimumHeight - topMargin, bottomMargin));
        }

        private static int SnapValue(int value)
        {
            return (int)Math.Round(value / (double)Snap) * Snap;
        }

        private void UpdateLabels()
        {
            areaLabel.Text = string.Format("{0} × {1} px",
                monitor.Width - leftMargin,
                monitor.Height - topMargin - bottomMargin);
            edgeLabel.Text = string.Format("Слева {0}  ·  Сверху {1}  ·  Снизу {2}  ·  стрелки: {3}",
                leftMargin,
                topMargin,
                bottomMargin,
                activeEdge == PickerEdge.Left ? "левая" :
                    activeEdge == PickerEdge.Top ? "верхняя" : "нижняя");
            StyleHudEdgeButton(leftEdgeButton, activeEdge == PickerEdge.Left);
            StyleHudEdgeButton(topEdgeButton, activeEdge == PickerEdge.Top);
            StyleHudEdgeButton(bottomEdgeButton, activeEdge == PickerEdge.Bottom);
        }

        private static void StyleHudEdgeButton(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }
            button.BackColor = active ? SafeScreenTheme.CyanDeep : SafeScreenTheme.PanelRaised;
            button.ForeColor = active ? SafeScreenTheme.CyanBright : SafeScreenTheme.Muted;
            button.FlatAppearance.BorderColor = active ? SafeScreenTheme.Cyan : SafeScreenTheme.Border;
        }

        private void UpdateHudBounds()
        {
            if (hud == null)
            {
                return;
            }
            Rectangle safe = GetSafeRectangle();
            int x = Math.Max(safe.Left + 28, safe.Right - hud.Width - 36);
            int y = Math.Max(safe.Top + 28, 28);
            x = Math.Min(Math.Max(12, Width - hud.Width - 12), x);
            y = Math.Min(Math.Max(12, Height - hud.Height - 12), y);
            hud.Location = new Point(x, y);
            hud.BringToFront();
        }

        private void DrawDamagePattern(Graphics graphics, Rectangle full, Rectangle safe)
        {
            GraphicsState state = graphics.Save();
            using (Region excludedRegion = new Region(full))
            {
                excludedRegion.Exclude(safe);
                graphics.SetClip(excludedRegion, CombineMode.Replace);
            }
            using (Pen pen = new Pen(Color.FromArgb(88, SafeScreenTheme.Scarlet), 1))
            {
                int offset;
                for (offset = -full.Height; offset < full.Width; offset += 24)
                {
                    graphics.DrawLine(pen, offset, full.Bottom, offset + full.Height, full.Top);
                }
            }
            graphics.Restore(state);
        }

        private void DrawExcludedLabels(Graphics graphics, Rectangle safe)
        {
            using (Font font = SafeScreenTheme.BodyFont(9, FontStyle.Bold))
            using (SolidBrush brush = new SolidBrush(SafeScreenTheme.Scarlet))
            {
                if (leftMargin >= 48)
                {
                    graphics.TranslateTransform(20, safe.Top + Math.Max(80, safe.Height / 2));
                    graphics.RotateTransform(-90);
                    graphics.DrawString("ИСКЛЮЧЕНО · " + leftMargin + " px", font, brush, 0, 0);
                    graphics.ResetTransform();
                }
                if (topMargin >= 32)
                {
                    graphics.DrawString("ИСКЛЮЧЕНО · " + topMargin + " px", font, brush,
                        safe.Left + 24, 11);
                }
                if (bottomMargin >= 32)
                {
                    graphics.DrawString("ИСКЛЮЧЕНО · " + bottomMargin + " px", font, brush,
                        safe.Left + 24, safe.Bottom + 14);
                }
            }
        }

        private void DrawHandle(Graphics graphics, Rectangle safe, PickerEdge edge)
        {
            bool active = edge == activeEdge;
            Color color = active ? SafeScreenTheme.CyanBright : SafeScreenTheme.Cyan;
            using (Pen pen = new Pen(color, active ? 8 : 5))
            using (SolidBrush brush = new SolidBrush(color))
            {
                if (edge == PickerEdge.Left)
                {
                    int y = safe.Top + safe.Height / 2;
                    graphics.DrawLine(pen, safe.Left, safe.Top + 10, safe.Left, safe.Bottom - 10);
                    graphics.FillEllipse(brush, safe.Left - 11, y - 28, 22, 56);
                }
                else
                {
                    int y = edge == PickerEdge.Top ? safe.Top : safe.Bottom;
                    int x = safe.Left + safe.Width / 2;
                    graphics.DrawLine(pen, safe.Left + 10, y, safe.Right - 10, y);
                    graphics.FillEllipse(brush, x - 28, y - 11, 56, 22);
                }
            }
        }
    }

    internal enum PickerEdge
    {
        Left,
        Top,
        Bottom
    }

    internal sealed class ScreenAreaPicker : Control
    {
        private const int HitDistance = 14;
        private const int Snap = 5;
        private readonly Rectangle monitor;
        private PickerEdge activeEdge;
        private bool dragging;
        private int leftMargin;
        private int topMargin;
        private int bottomMargin;

        internal ScreenAreaPicker(GuardSettings current)
        {
            monitor = Screen.PrimaryScreen.Bounds;
            leftMargin = current.Left;
            topMargin = current.Top;
            bottomMargin = current.Bottom;
            activeEdge = PickerEdge.Left;
            DoubleBuffered = true;
            TabStop = true;
            SetStyle(ControlStyles.Selectable, true);
            AccessibleName = "Выбор рабочей области экрана";
            AccessibleDescription = "Перетащи левую, верхнюю или нижнюю границу светлой области. Правый край закреплён.";
            BackColor = SafeScreenTheme.Panel;
            MinimumSize = new Size(420, 250);
        }

        internal event EventHandler MarginsChanged;

        internal int LeftMargin { get { return leftMargin; } }
        internal int TopMargin { get { return topMargin; } }
        internal int BottomMargin { get { return bottomMargin; } }

        internal PickerEdge ActiveEdge
        {
            get { return activeEdge; }
            set
            {
                activeEdge = value;
                Focus();
                Invalidate();
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            Graphics graphics = eventArgs.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(BackColor);

            Rectangle frame = GetMonitorRectangle();
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(65, 0, 0, 0)))
            {
                graphics.FillRectangle(shadow, new Rectangle(frame.X + 7, frame.Y + 8, frame.Width, frame.Height));
            }
            using (SolidBrush bezel = new SolidBrush(SafeScreenTheme.Border))
            {
                graphics.FillRectangle(bezel, frame);
            }

            Rectangle canvas = Rectangle.Inflate(frame, -5, -5);
            using (SolidBrush excluded = new SolidBrush(SafeScreenTheme.Canvas))
            {
                graphics.FillRectangle(excluded, canvas);
            }

            DrawDamagePattern(graphics, canvas);
            Rectangle safe = GetSafeRectangle(canvas);
            using (SolidBrush safeFill = new SolidBrush(Color.FromArgb(27, 61, 70)))
            {
                graphics.FillRectangle(safeFill, safe);
            }
            using (Pen safeBorder = new Pen(SafeScreenTheme.Cyan, 3))
            {
                graphics.DrawRectangle(safeBorder, safe);
            }

            DrawEdgeHandle(graphics, safe, PickerEdge.Left);
            DrawEdgeHandle(graphics, safe, PickerEdge.Top);
            DrawEdgeHandle(graphics, safe, PickerEdge.Bottom);

            using (Font titleFont = new Font(Font.FontFamily, 10, FontStyle.Bold))
            using (SolidBrush titleBrush = new SolidBrush(SafeScreenTheme.Text))
            using (SolidBrush detailBrush = new SolidBrush(SafeScreenTheme.Muted))
            {
                string title = "РАБОЧАЯ ОБЛАСТЬ";
                SizeF titleSize = graphics.MeasureString(title, titleFont);
                if (safe.Width > titleSize.Width + 20 && safe.Height > 58)
                {
                    graphics.DrawString(title, titleFont, titleBrush, safe.Left + 14, safe.Top + 14);
                    graphics.DrawString(
                        string.Format("{0} × {1} px", monitor.Width - leftMargin,
                            monitor.Height - topMargin - bottomMargin),
                        Font,
                        detailBrush,
                        safe.Left + 14,
                        safe.Top + 34);
                }
            }

            if (Focused)
            {
                ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(ClientRectangle, -2, -2));
            }
        }

        protected override void OnMouseDown(MouseEventArgs eventArgs)
        {
            base.OnMouseDown(eventArgs);
            if (eventArgs.Button != MouseButtons.Left)
            {
                return;
            }
            Focus();
            PickerEdge? hit = HitTest(eventArgs.Location);
            if (hit.HasValue)
            {
                activeEdge = hit.Value;
                dragging = true;
                Capture = true;
                UpdateFromPoint(eventArgs.Location);
            }
        }

        protected override void OnMouseMove(MouseEventArgs eventArgs)
        {
            base.OnMouseMove(eventArgs);
            if (dragging)
            {
                UpdateFromPoint(eventArgs.Location);
                return;
            }

            PickerEdge? hit = HitTest(eventArgs.Location);
            Cursor = !hit.HasValue ? Cursors.Default :
                hit.Value == PickerEdge.Left ? Cursors.SizeWE : Cursors.SizeNS;
        }

        protected override void OnMouseUp(MouseEventArgs eventArgs)
        {
            base.OnMouseUp(eventArgs);
            dragging = false;
            Capture = false;
        }

        protected override void OnKeyDown(KeyEventArgs eventArgs)
        {
            int step = eventArgs.Shift ? 20 : 5;
            bool changed = false;
            if (eventArgs.KeyCode == Keys.Space)
            {
                activeEdge = activeEdge == PickerEdge.Left ? PickerEdge.Top :
                    activeEdge == PickerEdge.Top ? PickerEdge.Bottom : PickerEdge.Left;
                changed = true;
            }
            else if (activeEdge == PickerEdge.Left && eventArgs.KeyCode == Keys.Left)
            {
                leftMargin -= step;
                changed = true;
            }
            else if (activeEdge == PickerEdge.Left && eventArgs.KeyCode == Keys.Right)
            {
                leftMargin += step;
                changed = true;
            }
            else if (activeEdge == PickerEdge.Top && eventArgs.KeyCode == Keys.Up)
            {
                topMargin -= step;
                changed = true;
            }
            else if (activeEdge == PickerEdge.Top && eventArgs.KeyCode == Keys.Down)
            {
                topMargin += step;
                changed = true;
            }
            else if (activeEdge == PickerEdge.Bottom && eventArgs.KeyCode == Keys.Up)
            {
                bottomMargin += step;
                changed = true;
            }
            else if (activeEdge == PickerEdge.Bottom && eventArgs.KeyCode == Keys.Down)
            {
                bottomMargin -= step;
                changed = true;
            }

            if (changed)
            {
                NormalizeMargins();
                eventArgs.Handled = true;
                eventArgs.SuppressKeyPress = true;
                NotifyChanged();
            }
            base.OnKeyDown(eventArgs);
        }

        private Rectangle GetMonitorRectangle()
        {
            Rectangle available = Rectangle.Inflate(ClientRectangle, -28, -24);
            float screenRatio = monitor.Width / (float)Math.Max(1, monitor.Height);
            int width = available.Width;
            int height = (int)Math.Round(width / screenRatio);
            if (height > available.Height)
            {
                height = available.Height;
                width = (int)Math.Round(height * screenRatio);
            }
            return new Rectangle(
                available.Left + (available.Width - width) / 2,
                available.Top + (available.Height - height) / 2,
                width,
                height);
        }

        private Rectangle GetSafeRectangle(Rectangle canvas)
        {
            int left = canvas.Left + Scale(leftMargin, monitor.Width, canvas.Width);
            int top = canvas.Top + Scale(topMargin, monitor.Height, canvas.Height);
            int bottom = canvas.Bottom - Scale(bottomMargin, monitor.Height, canvas.Height);
            return Rectangle.FromLTRB(left, top, canvas.Right, Math.Max(top + 1, bottom));
        }

        private static int Scale(int value, int sourceMaximum, int targetMaximum)
        {
            return (int)Math.Round(value * targetMaximum / (double)Math.Max(1, sourceMaximum));
        }

        private PickerEdge? HitTest(Point point)
        {
            Rectangle canvas = Rectangle.Inflate(GetMonitorRectangle(), -5, -5);
            Rectangle safe = GetSafeRectangle(canvas);
            if (point.Y >= safe.Top - HitDistance && point.Y <= safe.Bottom + HitDistance &&
                Math.Abs(point.X - safe.Left) <= HitDistance)
            {
                return PickerEdge.Left;
            }
            if (point.X >= safe.Left - HitDistance && point.X <= safe.Right + HitDistance &&
                Math.Abs(point.Y - safe.Top) <= HitDistance)
            {
                return PickerEdge.Top;
            }
            if (point.X >= safe.Left - HitDistance && point.X <= safe.Right + HitDistance &&
                Math.Abs(point.Y - safe.Bottom) <= HitDistance)
            {
                return PickerEdge.Bottom;
            }
            return null;
        }

        private void UpdateFromPoint(Point point)
        {
            Rectangle canvas = Rectangle.Inflate(GetMonitorRectangle(), -5, -5);
            if (activeEdge == PickerEdge.Left)
            {
                leftMargin = SnapValue((point.X - canvas.Left) * monitor.Width /
                    Math.Max(1, canvas.Width));
            }
            else if (activeEdge == PickerEdge.Top)
            {
                topMargin = SnapValue((point.Y - canvas.Top) * monitor.Height /
                    Math.Max(1, canvas.Height));
            }
            else
            {
                bottomMargin = SnapValue((canvas.Bottom - point.Y) * monitor.Height /
                    Math.Max(1, canvas.Height));
            }
            NormalizeMargins();
            NotifyChanged();
        }

        private void NormalizeMargins()
        {
            int minimumWidth = Math.Min(640, monitor.Width);
            int minimumHeight = Math.Min(480, monitor.Height);
            leftMargin = Math.Max(0, Math.Min(monitor.Width - minimumWidth, leftMargin));
            topMargin = Math.Max(0, Math.Min(monitor.Height - minimumHeight, topMargin));
            bottomMargin = Math.Max(
                0,
                Math.Min(monitor.Height - minimumHeight - topMargin, bottomMargin));
        }

        private static int SnapValue(int value)
        {
            return (int)Math.Round(value / (double)Snap) * Snap;
        }

        private void NotifyChanged()
        {
            Invalidate();
            if (MarginsChanged != null)
            {
                MarginsChanged(this, EventArgs.Empty);
            }
        }

        private void DrawDamagePattern(Graphics graphics, Rectangle canvas)
        {
            using (Pen pen = new Pen(Color.FromArgb(70, SafeScreenTheme.Scarlet), 1))
            {
                int offset;
                for (offset = -canvas.Height; offset < canvas.Width; offset += 18)
                {
                    graphics.DrawLine(pen, canvas.Left + offset, canvas.Bottom,
                        canvas.Left + offset + canvas.Height, canvas.Top);
                }
            }
        }

        private void DrawEdgeHandle(Graphics graphics, Rectangle safe, PickerEdge edge)
        {
            bool active = edge == activeEdge;
            Color color = active ? SafeScreenTheme.CyanBright : SafeScreenTheme.Cyan;
            float width = active ? 6 : 3;
            using (Pen pen = new Pen(color, width))
            using (SolidBrush brush = new SolidBrush(color))
            {
                if (edge == PickerEdge.Left)
                {
                    int y = safe.Top + safe.Height / 2;
                    graphics.DrawLine(pen, safe.Left, safe.Top + 6, safe.Left, safe.Bottom - 6);
                    graphics.FillEllipse(brush, safe.Left - 7, y - 15, 14, 30);
                }
                else
                {
                    int y = edge == PickerEdge.Top ? safe.Top : safe.Bottom;
                    int x = safe.Left + safe.Width / 2;
                    graphics.DrawLine(pen, safe.Left + 6, y, safe.Right - 6, y);
                    graphics.FillEllipse(brush, x - 15, y - 7, 30, 14);
                }
            }
        }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly ScreenAreaPicker picker;
        private readonly CheckBox enabledInput;
        private readonly Label areaLabel;
        private readonly Label marginsLabel;
        private readonly Button leftEdgeButton;
        private readonly Button topEdgeButton;
        private readonly Button bottomEdgeButton;

        internal SettingsForm(GuardSettings current)
        {
            Text = "SafeScreen — выбрать рабочую область";
            Icon = SafeScreenBrand.AppIcon;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = SafeScreenTheme.Canvas;
            ForeColor = SafeScreenTheme.Text;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ClientSize = new Size(650, 610);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(24, 22, 24, 20);
            layout.ColumnCount = 1;
            layout.RowCount = 7;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            Controls.Add(layout);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            PictureBox logo = new PictureBox();
            logo.Image = SafeScreenBrand.LoadLogo();
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.SetBounds(0, 0, 52, 52);
            header.Controls.Add(logo);

            Label title = CreateLabel("Выбери видимую область", 16, FontStyle.Bold,
                SafeScreenTheme.Text);
            title.Font = SafeScreenTheme.DisplayFont(16, FontStyle.Bold);
            title.SetBounds(66, 4, 500, 28);
            header.Controls.Add(title);
            Label subtitle = CreateLabel("Всё снаружи будет закрыто и недоступно окнам.", 9,
                FontStyle.Regular, SafeScreenTheme.Muted);
            subtitle.SetBounds(67, 34, 520, 22);
            header.Controls.Add(subtitle);
            layout.Controls.Add(header, 0, 0);

            Label instruction = CreateLabel(
                "Потяни мышкой левую, верхнюю или нижнюю границу голубой области. Правый край закреплён.",
                10, FontStyle.Regular, SafeScreenTheme.Text);
            instruction.AutoSize = true;
            instruction.MaximumSize = new Size(590, 0);
            instruction.Margin = new Padding(0, 4, 0, 10);
            layout.Controls.Add(instruction, 0, 1);

            picker = new ScreenAreaPicker(current);
            picker.Dock = DockStyle.Fill;
            picker.Margin = new Padding(0, 0, 0, 8);
            picker.MarginsChanged += OnPickerChanged;
            layout.Controls.Add(picker, 0, 2);

            RoundedPanel stats = new RoundedPanel();
            stats.Dock = DockStyle.Fill;
            stats.CornerRadius = 12;
            stats.BackColor = SafeScreenTheme.PanelRaised;
            stats.Padding = new Padding(14, 7, 14, 6);
            areaLabel = CreateLabel("", 10, FontStyle.Bold, SafeScreenTheme.CyanBright);
            areaLabel.SetBounds(14, 7, 260, 30);
            marginsLabel = CreateLabel("", 9, FontStyle.Regular, SafeScreenTheme.Muted);
            marginsLabel.SetBounds(270, 7, 300, 30);
            marginsLabel.TextAlign = ContentAlignment.MiddleRight;
            stats.Controls.Add(areaLabel);
            stats.Controls.Add(marginsLabel);
            layout.Controls.Add(stats, 0, 3);

            FlowLayoutPanel edgeButtons = new FlowLayoutPanel();
            edgeButtons.Dock = DockStyle.Fill;
            edgeButtons.FlowDirection = FlowDirection.LeftToRight;
            edgeButtons.WrapContents = false;
            edgeButtons.Padding = new Padding(0, 7, 0, 0);
            leftEdgeButton = CreateEdgeButton("↔ Левая граница", PickerEdge.Left);
            topEdgeButton = CreateEdgeButton("↕ Верхняя граница", PickerEdge.Top);
            bottomEdgeButton = CreateEdgeButton("↕ Нижняя граница", PickerEdge.Bottom);
            edgeButtons.Controls.Add(leftEdgeButton);
            edgeButtons.Controls.Add(topEdgeButton);
            edgeButtons.Controls.Add(bottomEdgeButton);
            layout.Controls.Add(edgeButtons, 0, 4);

            enabledInput = new CheckBox();
            enabledInput.AutoSize = true;
            enabledInput.Text = "Включить ограничения после сохранения";
            enabledInput.Checked = current.Enabled;
            enabledInput.ForeColor = SafeScreenTheme.Text;
            enabledInput.Margin = new Padding(2, 7, 0, 0);
            layout.Controls.Add(enabledInput, 0, 5);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Padding = new Padding(0, 8, 0, 0);

            Button applyButton = CreateActionButton("Сохранить область", true);
            applyButton.Click += OnApply;
            buttons.Controls.Add(applyButton);
            Button cancelButton = CreateActionButton("Отмена", false);
            cancelButton.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(cancelButton);
            Label keyboardHint = CreateLabel("Пробел — сменить грань · стрелки — точная настройка",
                8, FontStyle.Regular, SafeScreenTheme.Muted);
            keyboardHint.AutoSize = true;
            keyboardHint.Margin = new Padding(0, 8, 18, 0);
            buttons.Controls.Add(keyboardHint);
            layout.Controls.Add(buttons, 0, 6);

            AcceptButton = applyButton;
            CancelButton = cancelButton;
            Shown += OnShown;
            UpdatePreview();
            UpdateEdgeButtons();
        }

        internal GuardSettings SelectedSettings { get; private set; }

        private static Label CreateLabel(string text, float size, FontStyle style, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, size, style);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private Button CreateEdgeButton(string text, PickerEdge edge)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Height = 30;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Margin = new Padding(0, 0, 8, 0);
            button.Cursor = Cursors.Hand;
            button.Tag = edge;
            button.Click += OnEdgeButtonClick;
            return button;
        }

        private static Button CreateActionButton(string text, bool primary)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Height = 34;
            button.Padding = new Padding(12, 0, 12, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Cursor = Cursors.Hand;
            if (primary)
            {
                button.BackColor = SafeScreenTheme.Cyan;
                button.ForeColor = SafeScreenTheme.Canvas;
                button.FlatAppearance.BorderColor = SafeScreenTheme.CyanBright;
            }
            else
            {
                button.BackColor = SafeScreenTheme.PanelRaised;
                button.ForeColor = SafeScreenTheme.Text;
                button.FlatAppearance.BorderColor = SafeScreenTheme.Border;
            }
            return button;
        }

        private void OnShown(object sender, EventArgs eventArgs)
        {
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                Math.Max(work.Left, work.Right - Width - 24),
                Math.Max(work.Top, work.Top + 18));
            picker.Focus();
        }

        private void OnPickerChanged(object sender, EventArgs eventArgs)
        {
            UpdatePreview();
            UpdateEdgeButtons();
        }

        private void OnEdgeButtonClick(object sender, EventArgs eventArgs)
        {
            Button button = sender as Button;
            if (button != null && button.Tag is PickerEdge)
            {
                picker.ActiveEdge = (PickerEdge)button.Tag;
                UpdateEdgeButtons();
            }
        }

        private void UpdatePreview()
        {
            Rectangle monitor = Screen.PrimaryScreen.Bounds;
            areaLabel.Text = string.Format("Рабочая область  {0} × {1} px",
                monitor.Width - picker.LeftMargin,
                monitor.Height - picker.TopMargin - picker.BottomMargin);
            marginsLabel.Text = string.Format("Слева {0}  ·  Сверху {1}  ·  Снизу {2}",
                picker.LeftMargin, picker.TopMargin, picker.BottomMargin);
        }

        private void UpdateEdgeButtons()
        {
            StyleEdgeButton(leftEdgeButton, picker.ActiveEdge == PickerEdge.Left);
            StyleEdgeButton(topEdgeButton, picker.ActiveEdge == PickerEdge.Top);
            StyleEdgeButton(bottomEdgeButton, picker.ActiveEdge == PickerEdge.Bottom);
        }

        private static void StyleEdgeButton(Button button, bool active)
        {
            button.BackColor = active ? SafeScreenTheme.CyanDeep : SafeScreenTheme.PanelRaised;
            button.ForeColor = active ? SafeScreenTheme.CyanBright : SafeScreenTheme.Muted;
            button.FlatAppearance.BorderColor = active ? SafeScreenTheme.Cyan :
                SafeScreenTheme.Border;
        }

        private void OnApply(object sender, EventArgs eventArgs)
        {
            SelectedSettings = new GuardSettings(
                enabledInput.Checked,
                picker.LeftMargin,
                picker.TopMargin,
                picker.BottomMargin);
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal enum AppBarEdge : uint
    {
        Left = 0,
        Top = 1,
        Right = 2,
        Bottom = 3
    }

    internal sealed class EdgeBar : Form
    {
        private const int WindowStyleExToolWindow = 0x00000080;
        private const int WindowStyleExNoActivate = 0x08000000;
        private const int WindowMessageMouseActivate = 0x0021;
        private const int MouseActivateNoActivate = 3;

        private readonly uint callbackMessage;
        private readonly uint taskbarCreatedMessage;
        private readonly Action positionRequested;
        private readonly Action reRegisterRequested;
        private bool registered;

        internal EdgeBar(
            AppBarEdge edge,
            int thickness,
            uint callbackMessage,
            uint taskbarCreatedMessage,
            Action positionRequested,
            Action reRegisterRequested)
        {
            Edge = edge;
            Thickness = thickness;
            this.callbackMessage = callbackMessage;
            this.taskbarCreatedMessage = taskbarCreatedMessage;
            this.positionRequested = positionRequested;
            this.reRegisterRequested = reRegisterRequested;

            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Black;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Text = "AWAKE SafeScreen " + edge;
        }

        internal AppBarEdge Edge { get; private set; }

        internal int Thickness { get; private set; }

        internal void SetThickness(int thickness)
        {
            Thickness = Math.Max(0, thickness);
        }

        internal bool Registered
        {
            get { return registered; }
            set { registered = value; }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WindowStyleExToolWindow | WindowStyleExNoActivate;
                return parameters;
            }
        }

        internal void RegisterAppBar()
        {
            NativeMethods.RegisterAppBar(this, callbackMessage);
        }

        internal void UnregisterAppBar()
        {
            NativeMethods.UnregisterAppBar(this);
        }

        internal void PositionAppBar()
        {
            NativeMethods.PositionAppBar(this, callbackMessage);
        }

        internal void ApplyBounds(Rectangle rectangle)
        {
            NativeMethods.SetWindowPos(
                Handle,
                NativeMethods.WindowTopMost,
                rectangle.Left,
                rectangle.Top,
                rectangle.Width,
                rectangle.Height,
                NativeMethods.SetWindowPosNoActivate | NativeMethods.SetWindowPosShowWindow);
        }

        protected override void WndProc(ref Message message)
        {
            if ((uint)message.Msg == callbackMessage)
            {
                int notification = message.WParam.ToInt32();
                if (notification == NativeMethods.AppBarNotificationPositionChanged ||
                    notification == NativeMethods.AppBarNotificationStateChanged)
                {
                    positionRequested();
                }

                return;
            }

            if ((uint)message.Msg == taskbarCreatedMessage)
            {
                reRegisterRequested();
                return;
            }

            if (message.Msg == WindowMessageMouseActivate)
            {
                message.Result = new IntPtr(MouseActivateNoActivate);
                return;
            }

            base.WndProc(ref message);
        }
    }

    internal static class DisplayMode
    {
        internal static void TryPersistKnownPanelMode()
        {
            try
            {
                Screen[] screens = Screen.AllScreens;
                if (screens.Length != 1)
                {
                    SafeScreenLog.Write("Display mode unchanged: more than one monitor is active.");
                    return;
                }

                string deviceName = screens[0].DeviceName;
                if (!string.Equals(deviceName, @"\\.\DISPLAY1", StringComparison.OrdinalIgnoreCase))
                {
                    SafeScreenLog.Write("Display mode unchanged: the known internal panel is not DISPLAY1.");
                    return;
                }

                NativeMethods.DevMode current = NativeMethods.CreateDevMode();
                if (!NativeMethods.EnumDisplaySettings(
                    deviceName,
                    NativeMethods.EnumCurrentSettings,
                    ref current))
                {
                    SafeScreenLog.Write("Display mode unchanged: current mode could not be read.");
                    return;
                }

                bool knownCurrent =
                    (current.dmPelsWidth == SafeScreenSettings.PanelWidth &&
                     current.dmPelsHeight == SafeScreenSettings.PanelHeight) ||
                    (current.dmPelsWidth == SafeScreenSettings.ReducedWidth &&
                     current.dmPelsHeight == SafeScreenSettings.ReducedHeight);
                if (!knownCurrent)
                {
                    SafeScreenLog.Write(
                        "Display mode unchanged: unexpected mode " +
                        current.dmPelsWidth + "x" + current.dmPelsHeight + ".");
                    return;
                }

                NativeMethods.DevMode selected = current;
                if (current.dmPelsWidth != SafeScreenSettings.PanelWidth ||
                    current.dmPelsHeight != SafeScreenSettings.PanelHeight)
                {
                    bool found = false;
                    int modeIndex = 0;
                    NativeMethods.DevMode candidate;
                    while (true)
                    {
                        candidate = NativeMethods.CreateDevMode();
                        if (!NativeMethods.EnumDisplaySettings(deviceName, modeIndex, ref candidate))
                        {
                            break;
                        }

                        if (candidate.dmPelsWidth == SafeScreenSettings.PanelWidth &&
                            candidate.dmPelsHeight == SafeScreenSettings.PanelHeight &&
                            (!found || candidate.dmDisplayFrequency == current.dmDisplayFrequency))
                        {
                            selected = candidate;
                            found = true;
                            if (candidate.dmDisplayFrequency == current.dmDisplayFrequency)
                            {
                                break;
                            }
                        }

                        modeIndex++;
                    }

                    if (!found)
                    {
                        SafeScreenLog.Write("Display mode unchanged: 1920x1200 is not available.");
                        return;
                    }
                }

                selected.dmFields =
                    NativeMethods.DisplayModeBitsPerPixel |
                    NativeMethods.DisplayModePelsWidth |
                    NativeMethods.DisplayModePelsHeight |
                    NativeMethods.DisplayModeFrequency;

                int testResult = NativeMethods.ChangeDisplaySettingsEx(
                    deviceName,
                    ref selected,
                    IntPtr.Zero,
                    NativeMethods.ChangeDisplaySettingsTest,
                    IntPtr.Zero);
                if (testResult != NativeMethods.DisplayChangeSuccessful)
                {
                    SafeScreenLog.Write("Display mode test failed with code " + testResult + ".");
                    return;
                }

                int applyResult = NativeMethods.ChangeDisplaySettingsEx(
                    deviceName,
                    ref selected,
                    IntPtr.Zero,
                    NativeMethods.ChangeDisplaySettingsUpdateRegistry,
                    IntPtr.Zero);
                SafeScreenLog.Write(
                    "Display mode persistence result " + applyResult +
                    " for " + selected.dmPelsWidth + "x" + selected.dmPelsHeight + ".");
            }
            catch (Exception exception)
            {
                SafeScreenLog.Write("Display mode persistence skipped: " + exception.Message);
            }
        }
    }

    internal static class NativeMethods
    {
        internal const uint AppBarMessageNew = 0x00000000;
        internal const uint AppBarMessageRemove = 0x00000001;
        internal const uint AppBarMessageQueryPosition = 0x00000002;
        internal const uint AppBarMessageSetPosition = 0x00000003;
        internal const int AppBarNotificationStateChanged = 0;
        internal const int AppBarNotificationPositionChanged = 1;

        internal const int EnumCurrentSettings = -1;
        internal const uint ChangeDisplaySettingsUpdateRegistry = 0x00000001;
        internal const uint ChangeDisplaySettingsTest = 0x00000002;
        internal const int DisplayChangeSuccessful = 0;
        internal const int DisplayModeBitsPerPixel = 0x00040000;
        internal const int DisplayModePelsWidth = 0x00080000;
        internal const int DisplayModePelsHeight = 0x00100000;
        internal const int DisplayModeFrequency = 0x00400000;

        internal static readonly IntPtr WindowTopMost = new IntPtr(-1);
        internal const uint SetWindowPosNoActivate = 0x0010;
        internal const uint SetWindowPosShowWindow = 0x0040;

        private const uint SystemParametersGetWorkArea = 0x0030;

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;

            internal Rectangle ToRectangle()
            {
                return Rectangle.FromLTRB(Left, Top, Right, Bottom);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AppBarData
        {
            internal uint cbSize;
            internal IntPtr hWnd;
            internal uint uCallbackMessage;
            internal uint uEdge;
            internal Rect rc;
            internal IntPtr lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PointLong
        {
            internal int x;
            internal int y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            internal string dmDeviceName;
            internal short dmSpecVersion;
            internal short dmDriverVersion;
            internal short dmSize;
            internal short dmDriverExtra;
            internal int dmFields;
            internal PointLong dmPosition;
            internal int dmDisplayOrientation;
            internal int dmDisplayFixedOutput;
            internal short dmColor;
            internal short dmDuplex;
            internal short dmYResolution;
            internal short dmTTOption;
            internal short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            internal string dmFormName;
            internal short dmLogPixels;
            internal int dmBitsPerPel;
            internal int dmPelsWidth;
            internal int dmPelsHeight;
            internal int dmDisplayFlags;
            internal int dmDisplayFrequency;
            internal int dmICMMethod;
            internal int dmICMIntent;
            internal int dmMediaType;
            internal int dmDitherType;
            internal int dmReserved1;
            internal int dmReserved2;
            internal int dmPanningWidth;
            internal int dmPanningHeight;
        }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint RegisterWindowMessage(string messageName);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(
            uint action,
            uint parameter,
            ref Rect rectangle,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool EnumDisplaySettings(
            string deviceName,
            int modeNumber,
            ref DevMode devMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int ChangeDisplaySettingsEx(
            string deviceName,
            ref DevMode devMode,
            IntPtr window,
            uint flags,
            IntPtr parameter);

        internal static DevMode CreateDevMode()
        {
            DevMode mode = new DevMode();
            mode.dmDeviceName = new string('\0', 32);
            mode.dmFormName = new string('\0', 32);
            mode.dmSize = (short)Marshal.SizeOf(typeof(DevMode));
            return mode;
        }

        internal static void TryEnablePerMonitorDpiAwareness()
        {
            try
            {
                SetProcessDpiAwarenessContext(new IntPtr(-4));
            }
            catch (EntryPointNotFoundException)
            {
                // Windows 10 and 11 expose this API; the manifest remains the fallback.
            }
        }

        internal static void RegisterAppBar(EdgeBar bar, uint callbackMessage)
        {
            if (bar.Registered)
            {
                return;
            }

            AppBarData data = CreateAppBarData(bar, callbackMessage);
            UIntPtr result = SHAppBarMessage(AppBarMessageNew, ref data);
            bar.Registered = result != UIntPtr.Zero;
            if (!bar.Registered)
            {
                SafeScreenLog.Write("ABM_NEW failed for " + bar.Edge + ".");
            }
        }

        internal static void UnregisterAppBar(EdgeBar bar)
        {
            if (!bar.Registered || !bar.IsHandleCreated)
            {
                bar.Registered = false;
                return;
            }

            AppBarData data = CreateAppBarData(bar, 0);
            SHAppBarMessage(AppBarMessageRemove, ref data);
            bar.Registered = false;
        }

        internal static void PositionAppBar(EdgeBar bar, uint callbackMessage)
        {
            if (!bar.Registered)
            {
                RegisterAppBar(bar, callbackMessage);
            }

            Rectangle monitor = Screen.PrimaryScreen.Bounds;
            AppBarData data = CreateAppBarData(bar, callbackMessage);
            data.uEdge = (uint)bar.Edge;
            data.rc.Left = monitor.Left;
            data.rc.Top = monitor.Top;
            data.rc.Right = monitor.Right;
            data.rc.Bottom = monitor.Bottom;

            SHAppBarMessage(AppBarMessageQueryPosition, ref data);

            if (bar.Edge == AppBarEdge.Left)
            {
                int desiredRight = monitor.Left + bar.Thickness;
                data.rc.Right = Math.Max(data.rc.Left + 1, desiredRight);
            }
            else if (bar.Edge == AppBarEdge.Top)
            {
                int desiredBottom = monitor.Top + bar.Thickness;
                data.rc.Bottom = Math.Max(data.rc.Top + 1, desiredBottom);
            }
            else if (bar.Edge == AppBarEdge.Bottom)
            {
                int desiredTop = monitor.Bottom - bar.Thickness;
                data.rc.Top = Math.Min(data.rc.Bottom - 1, desiredTop);
            }

            SHAppBarMessage(AppBarMessageSetPosition, ref data);
            bar.ApplyBounds(data.rc.ToRectangle());
        }

        internal static bool IsPrimaryWorkAreaSafe(GuardSettings settings)
        {
            Rectangle monitor = Screen.PrimaryScreen.Bounds;
            Rect workArea = new Rect();
            if (!SystemParametersInfo(SystemParametersGetWorkArea, 0, ref workArea, 0))
            {
                return false;
            }

            return
                workArea.Left >= monitor.Left + settings.Left &&
                workArea.Top >= monitor.Top + settings.Top &&
                workArea.Right <= monitor.Right &&
                workArea.Bottom <= monitor.Bottom - settings.Bottom;
        }

        private static AppBarData CreateAppBarData(EdgeBar bar, uint callbackMessage)
        {
            AppBarData data = new AppBarData();
            data.cbSize = (uint)Marshal.SizeOf(typeof(AppBarData));
            data.hWnd = bar.Handle;
            data.uCallbackMessage = callbackMessage;
            data.uEdge = (uint)bar.Edge;
            return data;
        }
    }

    internal static class SafeScreenLog
    {
        private static readonly object Gate = new object();

        internal static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    string path = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "SafeScreen.log");
                    File.AppendAllText(
                        path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message +
                        Environment.NewLine);
                }
            }
            catch
            {
                // A screen guard must never fail because its diagnostic log is unavailable.
            }
        }
    }
}
