using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using XMeter2.Annotations;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace XMeter2
{
    public partial class MainWindow : INotifyPropertyChanged
    {
        private readonly DispatcherTimer _timer = new DispatcherTimer();

        private readonly LinkedList<TimeEntry> _upPoints = new LinkedList<TimeEntry>();
        private readonly LinkedList<TimeEntry> _downPoints = new LinkedList<TimeEntry>();

        private ulong _lastMaxUp;
        private ulong _lastMaxDown;
        private Icon _icon;

        private string _upSpeed = "0 B/s";
        private string _downSpeed = "0 B/s";
        private string _toolTipText = "Initializing...";
        private string _startTime = DateTime.Now.AddSeconds(-1).ToString("HH:mm:ss");
        private string _endTime = DateTime.Now.ToString("HH:mm:ss");
        private Brush _popupBackground;
        private Brush _popupBorder;
        private bool _isPopupOpen;
        private Brush _popupPanel;
        private TaskbarIcon _notificationIcon;

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

        public string UpSpeed
        {
            get => _upSpeed;
            set
            {
                if (value == _upSpeed) return;
                _upSpeed = value;
                OnPropertyChanged();
            }
        }

        public string DownSpeed
        {
            get => _downSpeed;
            set
            {
                if (value == _downSpeed) return;
                _downSpeed = value;
                OnPropertyChanged();
            }
        }

        public string ToolTipText
        {
            get => _toolTipText;
            private set
            {
                if (value == _toolTipText) return;
                _toolTipText = value;
                OnPropertyChanged();
            }
        }

        public Icon TrayIcon
        {
            get => _icon;
            set
            {
                if (ReferenceEquals(_icon, value)) return;
                _icon = value;
                NotificationIcon.Icon = value;
                OnPropertyChanged();
            }
        }

        public Brush PopupBackground
        {
            get => _popupBackground;
            private set
            {
                if (value == _popupBackground) return;
                _popupBackground = value;
                OnPropertyChanged();
            }
        }

        public Brush PopupBorder
        {
            get => _popupBorder;
            set
            {
                if (Equals(value, _popupBorder)) return;
                _popupBorder = value;
                OnPropertyChanged();
            }
        }

        public Brush PopupPanel
        {
            get => _popupPanel;
            set
            {
                if (Equals(value, _popupPanel)) return;
                _popupPanel = value;
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

        public MainWindow()
        {
            InitializeComponent();
            
            //SettingsManager.ReadSettings();

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.IsEnabled = true;

            UpdateIcon(false, false);

            UpdateSpeeds();

            SystemEvents.UserPreferenceChanging += SystemEvents_UserPreferenceChanging;

            UpdateAccentColor();
        }

        private void UpdateAccentColor()
        {
            var c1 = AccentColorSet.ActiveSet["SystemAccent"];
            var c2 = AccentColorSet.ActiveSet["SystemAccentDark2"];
            var c3 = AccentColorSet.ActiveSet["SystemAccentLight1"];
            c2.A = 192;
            c3.A = 96;
            PopupBackground = new SolidColorBrush(c2);
            PopupBorder = new SolidColorBrush(c1);
            PopupPanel = new SolidColorBrush(c3);
        }

        private void Popup_OnOpened(object sender, EventArgs e)
        {
            Natives.EnableBlur((Popup)sender);
        }

        private void SystemEvents_UserPreferenceChanging(object sender, UserPreferenceChangingEventArgs e)
        {
            UpdateAccentColor();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

#if false
        public static void ReadSettings()
        {
            Application.Current.MainWindow.Width = (int)Registry.GetValue("HKEY_CURRENT_USER\\Software\\XMeter", "PreferredWidth", 384);
            Application.Current.MainWindow.Height = (int)Registry.GetValue("HKEY_CURRENT_USER\\Software\\XMeter", "PreferredHeight", 240);
        }

        public static void WriteSettings()
        {
            Registry.SetValue("HKEY_CURRENT_USER\\Software\\XMeter", "PreferredWidth", (int)Application.Current.MainWindow.Width);
            Registry.SetValue("HKEY_CURRENT_USER\\Software\\XMeter", "PreferredHeight", (int)Application.Current.MainWindow.Height);
        }
#endif

        private void NotificationIcon_OnTrayLeftMouseUp(object sender, RoutedEventArgs e)
        {
            var popup = (Popup)FindResource("Popup");
            popup.HorizontalOffset = SystemParameters.WorkArea.Width - popup.Width;
            popup.VerticalOffset = SystemParameters.WorkArea.Height - popup.Height;
            popup.PlacementRectangle = SystemParameters.WorkArea;
            popup.Visibility = Visibility.Visible;
            IsPopupOpen = true;
        }

        private void Timer_Tick(object o, EventArgs e)
        {
            UpdateSpeeds();

            var sendActivity = _upPoints.Last.Value.Bytes > 0;
            var recvActivity = _downPoints.Last.Value.Bytes > 0;
            UpdateIcon(sendActivity, recvActivity);

            UpSpeed = Util.FormatUSize(_upPoints.Last.Value.Bytes);
            DownSpeed = Util.FormatUSize(_downPoints.Last.Value.Bytes);

            ToolTipText = $"Send: {Util.FormatUSize(_upPoints.Last.Value.Bytes)}; Receive: {Util.FormatUSize(_downPoints.Last.Value.Bytes)}";

            if (IsPopupOpen)
            {
                var upTime = (_upPoints.Last.Value.TimeStamp - _upPoints.First.Value.TimeStamp).TotalSeconds;
                var downTime = (_downPoints.Last.Value.TimeStamp - _downPoints.First.Value.TimeStamp).TotalSeconds;
                var spanSeconds = Math.Max(upTime, downTime);

                var currentCheck = DateTime.Now;
                StartTime = currentCheck.AddSeconds(-spanSeconds).ToString("HH:mm:ss");
                EndTime = currentCheck.ToString("HH:mm:ss");

                UpdateGraph2();
            }
        }

        private void UpdateIcon(bool sendActivity, bool recvActivity)
        {
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
        }

        private void UpdateSpeeds()
        {
            var maxStamp = NetTracker.UpdateNetwork(out ulong bytesReceivedPerSec, out ulong bytesSentPerSec);

            AddData(_upPoints, maxStamp, bytesSentPerSec);
            AddData(_downPoints, maxStamp, bytesReceivedPerSec);

            _lastMaxDown = _downPoints.Select(s => s.Bytes).Max();
            _lastMaxUp = _upPoints.Select(s => s.Bytes).Max();
        }

        private static void AddData(LinkedList<TimeEntry> points, DateTime maxStamp, ulong bytesSentPerSec)
        {
            points.AddLast(new TimeEntry(maxStamp, bytesSentPerSec));

            var totalSpan = points.Last.Value.TimeStamp - points.First.Value.TimeStamp;
            while (totalSpan.TotalSeconds > NetTracker.MaxSecondSpan && points.Count > 1)
            {
                points.RemoveFirst();
                totalSpan = points.Last.Value.TimeStamp - points.First.Value.TimeStamp;
            }
        }

        private void UpdateGraph2()
        {
            PicGraph.Children.Clear();

            var max = Math.Max(_lastMaxDown, _lastMaxUp);

            BuildPolygon(_upPoints, max, 255, 24, 32, true);
            BuildPolygon(_downPoints, max, 48, 48, 255, false);
        }

        private void BuildPolygon(LinkedList<TimeEntry> points, ulong max, byte r, byte g, byte b, bool up)
        {
            if (points.Count == 0)
                return;

            var bottom = PicGraph.ActualHeight;
            var right = PicGraph.ActualWidth;

            var lastTime = points.Last.Value.TimeStamp;

            var elapsed = (lastTime - points.First.Value.TimeStamp).TotalSeconds;

            var scale = 1.0;
            if (elapsed > 0 && elapsed < PicGraph.ActualWidth)
                scale = PicGraph.ActualWidth / elapsed;

            var polygon = new Polygon();
            for (var current = points.Last; current != null; current = current.Previous)
            {
                var td = (lastTime - current.Value.TimeStamp).TotalSeconds;

                var xx = right - td * scale;
                var yy = current.Value.Bytes * PicGraph.ActualHeight / max;

                polygon.Points.Add(new Point(xx, up ? bottom - yy : yy));
            }

            polygon.Points.Add(new Point(right, up ? bottom : 0));

            polygon.Fill = new SolidColorBrush(Color.FromArgb(255, r, g, b));
            PicGraph.Children.Add(polygon);
        }

        private class TimeEntry
        {
            public readonly DateTime TimeStamp;
            public readonly ulong Bytes;

            public TimeEntry(DateTime t, ulong b)
            {
                TimeStamp = t;
                Bytes = b;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
