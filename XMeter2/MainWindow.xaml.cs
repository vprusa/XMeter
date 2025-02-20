﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using XMeter2.Annotations;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using DColor = System.Drawing.Color;
using DFont = System.Drawing.Font;
using DFontStyle = System.Drawing.FontStyle;
using DFontFamily = System.Drawing.FontFamily;
using DBrush = System.Drawing.Brush;

//using System.Drawing.FontFamily;


namespace XMeter2
{

    
    public static class PerformanceInfo
    {
        // https://stackoverflow.com/questions/10027341/c-sharp-get-used-memory-in

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetPerformanceInfo([Out] out PerformanceInformation PerformanceInformation, [In] int Size);

        [StructLayout(LayoutKind.Sequential)]
        public struct PerformanceInformation
        {
            public int Size;
            public IntPtr CommitTotal;
            public IntPtr CommitLimit;
            public IntPtr CommitPeak;
            public IntPtr PhysicalTotal;
            public IntPtr PhysicalAvailable;
            public IntPtr SystemCache;
            public IntPtr KernelTotal;
            public IntPtr KernelPaged;
            public IntPtr KernelNonPaged;
            public IntPtr PageSize;
            public int HandlesCount;
            public int ProcessCount;
            public int ThreadCount;
        }

        public static Int64 GetPhysicalAvailableMemoryInMiB()
        {
            PerformanceInformation pi = new PerformanceInformation();
            if (GetPerformanceInfo(out pi, Marshal.SizeOf(pi)))
            {
                return Convert.ToInt64((pi.PhysicalAvailable.ToInt64() * pi.PageSize.ToInt64() / 1048576));
            }
            else
            {
                return -1;
            }

        }

        public static Int64 GetTotalMemoryInMiB()
        {
            PerformanceInformation pi = new PerformanceInformation();
            if (GetPerformanceInfo(out pi, Marshal.SizeOf(pi)))
            {
                return Convert.ToInt64((pi.PhysicalTotal.ToInt64() * pi.PageSize.ToInt64() / 1048576));
            }
            else
            {
                return -1;
            }

        }
    }

    public partial class MainWindow : INotifyPropertyChanged
    {
        private readonly DispatcherTimer _timer = new DispatcherTimer();

        private static readonly TimeSpan ShowAnimationDelay = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan ShowAnimationDuration = TimeSpan.FromMilliseconds(200);
        private readonly DoubleAnimation _showOpacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = ShowAnimationDuration,
            DecelerationRatio = 1
        };
        private readonly DoubleAnimation _showTopAnimation = new DoubleAnimation
        {
            Duration = ShowAnimationDuration,
            DecelerationRatio = 1
        };

        private ulong _upSpeed;
        private ulong _downSpeed;
        private ulong _downSpeedMax;
        private ulong _upSpeedMax;
        private string _startTime;
        private string _endTime;
        private Brush _popupBackground;
        private Brush _accentBackground;
        private Color _popupBackgroundColor;
        private bool _isPopupOpen;
        private bool _opening;
        private bool _shown;
        private Icon _iconCPU;
        private Icon _iconRAM;
        private Brush _mainText;

        public string StartTime
        {
            get => _startTime;
            set
            {
                if (value == _startTime) return;
                _startTime = value;
                OnPropertyChanged();
            }
        }

        public string EndTime
        {
            get => _endTime;
            set
            {
                if (value == _endTime) return;
                _endTime = value;
                OnPropertyChanged();
            }
        }

        public ulong UpSpeed
        {
            get => _upSpeed;
            set
            {
                if (value == _upSpeed) return;
                _upSpeed = value;
                OnPropertyChanged();
            }
        }

        public ulong UpSpeedMax
        {
            get => _upSpeedMax;
            set
            {
                if (value == _upSpeedMax) return;
                _upSpeedMax = value;
                OnPropertyChanged();
            }
        }

        public ulong DownSpeed
        {
            get => _downSpeed;
            set
            {
                if (value == _downSpeed) return;
                _downSpeed = value;
                OnPropertyChanged();
            }
        }

        public ulong DownSpeedMax
        {
            get => _downSpeedMax;
            set
            {
                if (value == _downSpeedMax) return;
                _downSpeedMax = value;
                OnPropertyChanged();
            }
        }

        public Icon TrayIconCPU
        {
            get => _iconCPU;
            set
            {
                if (ReferenceEquals(_iconCPU, value)) return;
                _iconCPU = value;
                NotificationIconCPU.Icon = value;
                // NotificationIconCPU.MinWidth = 100;
                // NotificationIconCPU.Width = 100;
                OnPropertyChanged();
            }
        }


        public Icon TrayIconRAM
        {
            get => _iconRAM;
            set
            {
                if (ReferenceEquals(_iconRAM, value)) return;
                _iconRAM = value;
                NotificationIconRAM.Icon = value;
                NotificationIconRAM.MinWidth = 30;
                NotificationIconRAM.Width = 30;
                OnPropertyChanged();
            }
        }

        public Brush PopupBackground
        {
            get => _popupBackground;
            private set
            {
                if (Equals(value, _popupBackground)) return;
                _popupBackground = value;
                OnPropertyChanged();
            }
        }

        public Brush AccentBackground
        {
            get => _accentBackground;
            private set
            {
                if (Equals(value, _accentBackground)) return;
                _accentBackground = value;
                OnPropertyChanged();
            }
        }

        public Color TextShadow
        {
            get => _popupBackgroundColor;
            private set
            {
                if (Equals(value, _popupBackgroundColor)) return;
                _popupBackgroundColor = value;
                OnPropertyChanged();
            }
        }

        public Brush TextColor
        {
            get => _mainText;
            set
            {
                if (Equals(value, _mainText)) return;
                _mainText = value;
                OnPropertyChanged();
            }
        }

        public bool IsPopupOpen
        {
            get => _isPopupOpen;
            set
            {
                if (value == _isPopupOpen) return;
                _isPopupOpen = value;
                OnPropertyChanged();
            }
        }

        public string MenuTitle { get; }

        public MainWindow()
        {
            var ass = Application.Current.MainWindow.GetType().Assembly.GetName();
            MenuTitle = $"XMeter v{ass.Version}";

            InitializeComponent();


            SettingsManager.ReadSettings();

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.IsEnabled = true;

            SystemEvents.UserPreferenceChanging += SystemEvents_UserPreferenceChanging;

            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => {

                UpdateAccentColor();

                PerformUpdate();
                Hide();
            }));
        }

        private void UpdateAccentColor()
        {
#if false
            File.WriteAllLines(@"F:\Accents.txt", AccentColorSet.ActiveSet.GetAllColorNames().Select(s => {
                var c = AccentColorSet.ActiveSet[s];
                return $"{s}: {c}";
            }));
#endif
            var background = AccentColorSet.ActiveSet["SystemBackground"];
            var backgroundDark = AccentColorSet.ActiveSet["SystemBackgroundDarkTheme"];
            var shadow = background;
            var text = AccentColorSet.ActiveSet["SystemText"];
            var accent = AccentColorSet.ActiveSet["SystemAccentLight3"];
            //if (background != backgroundDark)
            //{
            //    accent = AccentColorSet.ActiveSet["SystemAccentDark3"];
            //}
            accent.A = 128;
            background.A = 160;

            if (Natives.EnableBlur(this, background))
            {
                background = Colors.Transparent;
            }
            PopupBackground = new SolidColorBrush(background);
            AccentBackground = new SolidColorBrush(accent);
            TextColor = new SolidColorBrush(text);
            TextShadow = shadow;
        }

        private void SystemEvents_UserPreferenceChanging(object sender, UserPreferenceChangingEventArgs e)
        {
            UpdateAccentColor();
        }

        private void NotificationIcon_OnMouseLeftButtonUp(object sender, RoutedEventArgs routedEventArgs)
        {
            Popup();
        }

        private void Popup()
        {
            UpdateGraphUI();
            _opening = true;
            DelayInvoke(250, () => {
                _opening = false;
                UpdateGraphUI();
            });

            BeginAnimation(OpacityProperty, null);
            BeginAnimation(TopProperty, null);
            Left = SystemParameters.WorkArea.Width - Width;
            Top = SystemParameters.WorkArea.Height;
            Opacity = 0;

            _shown = true;
            Dispatcher.BeginInvoke(new Action(Show));
        }

        private void MainWindow_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!IsVisible || !_shown)
            {
                Opacity = 0;
                return;
            }

            _shown = false;
            Dispatcher.BeginInvoke(new Action(() => Activate()));

            _showTopAnimation.From = SystemParameters.WorkArea.Height;
            _showTopAnimation.To = SystemParameters.WorkArea.Height - Height;

            DelayInvoke(ShowAnimationDelay, () => {
                BeginAnimation(OpacityProperty, _showOpacityAnimation);
                BeginAnimation(TopProperty, _showTopAnimation);
            });
        }

        private void DelayInvoke(uint ms, Action callback)
        {
            DelayInvoke(TimeSpan.FromMilliseconds(ms), callback);
        }

        private void DelayInvoke(TimeSpan time, Action callback)
        {
            if (time.TotalSeconds < float.Epsilon)
            {
                Dispatcher.BeginInvoke(callback);
                return;
            }

            var timer = new DispatcherTimer
            {
                Interval = time
            };
            timer.Tick += (_, __) =>
            {
                timer.Stop();
                Dispatcher.Invoke(callback);
            };
            timer.Start();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Hide();
            Opacity = 0;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SettingsManager.WriteSettings();

            UpdateTime();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Timer_Tick(object o, EventArgs e)
        {
            PerformUpdate();
        }

        private void PerformUpdate()
        {
            DataTracker.Instance.FetchData();

            var (sendSpeed, recvSpeed) = DataTracker.Instance.CurrentSpeed;
            UpSpeed = sendSpeed;
            DownSpeed = recvSpeed;

            var (sendMax, recvMax) = DataTracker.Instance.MaxSpeed;
            UpSpeedMax = sendMax;
            DownSpeedMax = recvMax;

            UpdateIconCPU();
            UpdateIconRAM();

            if (!IsVisible || _opening)
                return;

            UpdateTime();

            UpdateGraphUI();
        }

        private void UpdateTime()
        {
            var (sendTimeLast, recvTimeLast) = DataTracker.Instance.CurrentTime;
            var (sendTimeFirst, recvTimeFirst) = DataTracker.Instance.FirstTime;

            var upTime = (sendTimeLast - sendTimeFirst).TotalSeconds;
            var downTime = (recvTimeLast - recvTimeFirst).TotalSeconds;
            var spanSeconds = Math.Min(Graph.ActualWidth, Math.Max(upTime, downTime));

            var currentCheck = DateTime.Now;
            StartTime = currentCheck.AddSeconds(-spanSeconds).ToString("HH:mm:ss", CultureInfo.CurrentUICulture);
            EndTime = currentCheck.ToString("HH:mm:ss", CultureInfo.CurrentUICulture);
        }



        private void UpdateIconRAM()
        {
            var (sendSpeed, recvSpeed) = DataTracker.Instance.CurrentSpeed;
            var sendActivity = sendSpeed > 0;
            var recvActivity = recvSpeed > 0;

            /*
            if (sendActivity && recvActivity)
            {
                TrayIcon = Properties.Resources.U1D1;
            }
            else if (sendActivity)
            {
                TrayIcon = Properties.Resources.U1D0;
            }
            else if (recvActivity)
            {
                TrayIcon = Properties.Resources.U0D1;
            }
            else
            {
                TrayIcon = Properties.Resources.U0D0;
            }
            */
            //Font fontToUse = new Font("Microsoft Sans Serif", 16, FontStyle.Normal, GraphicsUnit.Pixel);
            Font fontToUse = new Font("Microsoft Sans Serif", 11, DFontStyle.Regular, GraphicsUnit.Pixel);
            DBrush brushToUse = new SolidBrush(DColor.White);
            Bitmap bitmapText = new Bitmap(16, 16);
            Graphics g = System.Drawing.Graphics.FromImage(bitmapText);

            IntPtr hIcon;
            Int64 phav = PerformanceInfo.GetPhysicalAvailableMemoryInMiB();
            Int64 tot = PerformanceInfo.GetTotalMemoryInMiB();
            decimal percentFree = ((decimal)phav / (decimal)tot) * 100;
            decimal percentOccupied = 100 - percentFree;

            string icon_text = "" + percentOccupied + "";
            // g.Clear(DColor.Transparent);
            g.Clear(DColor.DarkGreen);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            //g.DrawString(str, fontToUse, brushToUse, -4, -2);
            g.DrawString(icon_text, fontToUse, brushToUse, -2, 2);
            hIcon = (bitmapText.GetHicon());
            TrayIconCPU = System.Drawing.Icon.FromHandle(hIcon);
        }
        /*
        private void UpdateIconRAM()
        {
            var (sendSpeed, recvSpeed) = DataTracker.Instance.CurrentSpeed;
            var sendActivity = sendSpeed > 0;
            var recvActivity = recvSpeed > 0;

            //Font fontToUse = new Font("Microsoft Sans Serif", 16, FontStyle.Normal, GraphicsUnit.Pixel);
            Font fontToUse = new Font("Microsoft Sans Serif", 8, DFontStyle.Regular, GraphicsUnit.Pixel);
            DBrush brushToUse = new SolidBrush(DColor.White);
            Bitmap bitmapText = new Bitmap(24, 24);
            Graphics g = System.Drawing.Graphics.FromImage(bitmapText);

            IntPtr hIcon;
            Int64 phav = PerformanceInfo.GetPhysicalAvailableMemoryInMiB();
            Int64 tot = PerformanceInfo.GetTotalMemoryInMiB();
            decimal percentFree = ((decimal)phav / (decimal)tot) * 100;
            decimal percentOccupied = 100 - percentFree;
            string icon_text = "" + percentOccupied + "%";

            //String str = "X x O o";
            g.Clear(DColor.Red);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            //g.DrawString(icon_text, fontToUse, brushToUse, -1, -0);
            //g.DrawString(icon_text, fontToUse, brushToUse, -1, 8);
            g.DrawString(icon_text, fontToUse, brushToUse, -1, -);
            hIcon = (bitmapText.GetHicon());
            TrayIconRAM = System.Drawing.Icon.FromHandle(hIcon);
        }
        */

        //private System.Diagnostics.PerformanceCounter cpuCounter;
        //cpuCounter = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total");
        private System.Diagnostics.PerformanceCounter cpuCounter = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total");

        private void UpdateIconCPU()
        {
            var (sendSpeed, recvSpeed) = DataTracker.Instance.CurrentSpeed;
            var sendActivity = sendSpeed > 0;
            var recvActivity = recvSpeed > 0;

            //Font fontToUse = new Font("Microsoft Sans Serif", 16, FontStyle.Normal, GraphicsUnit.Pixel);
            Font fontToUse = new Font("Microsoft Sans Serif", 11, DFontStyle.Regular, GraphicsUnit.Pixel);
            DBrush brushToUse = new SolidBrush(DColor.White);
            Bitmap bitmapText = new Bitmap(16, 16);
            Graphics g = System.Drawing.Graphics.FromImage(bitmapText);

            IntPtr hIcon;

            // https://stackoverflow.com/questions/278071/how-to-get-the-cpu-usage-in-c
            // TODO who usage per core as colored lines e.g. from left to right
            // TODO show progress bar or counter from left to right when next average of CPU usage will be calculated
             string icon_text = "" + cpuCounter.NextValue() + "";


            //System.Diagnostics.PerformanceCounter ramCounter;
            //ramCounter = new System.Diagnostics.PerformanceCounter("Memory", "Available MBytes");\
            //string icon_text = "" + ramCounter.NextValue() + "";

            // g.Clear(DColor.Transparent);
            g.Clear(DColor.DarkRed);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            g.DrawString(icon_text, fontToUse, brushToUse, -2, 2);
            hIcon = (bitmapText.GetHicon());
            TrayIconRAM = System.Drawing.Icon.FromHandle(hIcon);
        }

        private void UpdateGraphUI()
        {
            Graph.Children.Clear();

            int maxPoints = (int)(Graph.ActualWidth / 10);
            //DataTracker copy = DataTracker.Instance.Simplify(maxPoints);
            //DataTracker copy = DataTracker.Instance.Simplify(4);
            var copy = DataTracker.Instance;

            var (sendSpeedMax, recvSpeedMax) = copy.MaxSpeed;

            var sqUp = Math.Max(32, Math.Sqrt(sendSpeedMax));
            var sqDown = Math.Max(32, Math.Sqrt(recvSpeedMax));
            var max2 = sqDown + sqUp;
            var maxUp = max2 * sendSpeedMax / sqUp;
            var maxDown = max2 * recvSpeedMax / sqDown;

            BuildPolygon(copy.SendPoints, (ulong) maxUp, 255, 24, 32, true);
            BuildPolygon(copy.RecvPoints, (ulong) maxDown, 48, 48, 255,  false);

            var yy = sqDown * Graph.ActualHeight / max2;
            var line = new Line
            {
                X1 = 0,
                X2 = Graph.ActualWidth,
                Y1 = yy,
                Y2 = yy,
                Stroke = TextColor,
                Opacity = .6,
                StrokeDashArray = new DoubleCollection(new[] {1.0, 2.0}),
                StrokeDashCap = PenLineCap.Flat
            };
            Graph.Children.Add(line);

            GraphDown.Margin = new Thickness(0, 0, 0, Graph.ActualHeight - yy);
            GraphUp.Margin = new Thickness(0, yy, 0, 0);
        }

        private void BuildPolygon(LinkedList<(DateTime TimeStamp, ulong Bytes)> points, ulong max, byte r, byte g, byte b, bool up)
        {
            if (points.Count == 0)
                return;

            var bottom = Graph.ActualHeight;
            var right = Graph.ActualWidth;

            var lastTime = points.Last.Value.TimeStamp;

            var elapsed = (lastTime - points.First.Value.TimeStamp).TotalSeconds;

            var scale = 1.0;
            if (elapsed > 0 && elapsed < Graph.ActualWidth)
                scale = Graph.ActualWidth / elapsed;

            var polygon = new Polyline();
            polygon.Points.Add(new Point(right, up ? bottom : 0));

            for (var current = points.Last; current != null; current = current.Previous)
            {
                var td = (lastTime - current.Value.TimeStamp).TotalSeconds;

                var xx = right - td * scale;
                var yy = current.Value.Bytes * Graph.ActualHeight / max;

                polygon.Points.Add(new Point(xx, up ? bottom - yy : yy));
            }

            polygon.Points.Add(new Point(right - elapsed * scale, up ? bottom : 0));

            polygon.Stroke = new SolidColorBrush(Color.FromArgb(160, r, g, b));
            polygon.StrokeThickness = 2;
            polygon.Fill = new SolidColorBrush(Color.FromArgb(64, r, g, b));
            Graph.Children.Add(polygon);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
