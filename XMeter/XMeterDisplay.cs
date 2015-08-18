﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Management;
using Microsoft.Win32;

namespace XMeter
{
    public partial class XMeterDisplay : Form
    {
        private const double MaxSecondSpan = 3600;

        readonly LinkedList<Tuple<DateTime, ulong, ulong>> timeStamps = new LinkedList<Tuple<DateTime, ulong, ulong>>();

        ulong lastMinSpeed;
        ulong lastMaxSpeed;

        readonly Dictionary<string, ulong> prevLastSend = new Dictionary<string, ulong>();
        readonly Dictionary<string, ulong> prevLastRecv = new Dictionary<string, ulong>();
        readonly Dictionary<string, DateTime> prevLastStamp = new Dictionary<string, DateTime>();
        readonly ManagementObjectSearcher searcher =
            new ManagementObjectSearcher(
                "SELECT Name, BytesReceivedPerSec, BytesSentPerSec, Timestamp_Sys100NS"+
                " FROM Win32_PerfRawData_Tcpip_NetworkInterface");
        
        bool firstUpdate = true;

        DateTime lastCheck;

        bool startMinimized;
        bool startOnLogon;
        bool realClosing;

        public XMeterDisplay()
        {
            InitializeComponent();

            UpdateSpeeds();
            
            ReadSettings();
        }

        private void ReadSettings()
        {
            try
            {
                var value = (int)Registry.GetValue("HKEY_CURRENT_USER\\Software\\XMeter", "StartMinimized", -1);
                if (value < 0)
                    Registry.SetValue("HKEY_CURRENT_USER\\Software\\XMeter", "StartMinimized", 0);
                startMinimized = value > 0;
            }
            catch (Exception)
            {
                startMinimized = false;
                Registry.SetValue("HKEY_CURRENT_USER\\Software\\XMeter", "StartMinimized", 0);
            }

            try
            {
                var value = (int)Registry.GetValue("HKEY_CURRENT_USER\\Software\\XMeter", "WindowOpacity", -1);
                if (value < 0)
                {
                    Registry.SetValue("HKEY_CURRENT_USER\\Software\\XMeter", "WindowOpacity", 100);
                    value = 100;
                }
                Opacity = value / 100.0;
            }
            catch (Exception)
            {
                Opacity = 1.0;
                Registry.SetValue("HKEY_CURRENT_USER\\Software\\XMeter", "WindowOpacity", 100);
            }

            //object path = Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\Current Version\\Run","XMeter");

            //if (path != null)
            //{
            //    if(Application.ExecutablePath == (string)path)
            //        startOnLogon = true;
            //}
        }

        private void WriteSettings()
        {
            Registry.SetValue("HKEY_CURRENT_USER\\Software\\XMeter", "StartMinimized", startMinimized ? 1 : 0);
            Registry.SetValue("HKEY_CURRENT_USER\\Software\\XMeter", "WindowOpacity", (int)(Opacity*100));

            //object path = Registry.CurrentUser.GetValue("Software\\Microsoft\\Windows\\Current Version\\Run","XMeter");

            //if ((path != null) && !startOnLogon)
            //{
            //    Registry.DeleteValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\Current Version\\Run","XMeter");
            //}

            //if(startOnLogon)
            //    Registry.SetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\Current Version\\Run","XMeter", Application.ExecutablePath);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            lbMinSpeed.Text = "0 Bytes/s";
            lbMaxSpeed.Text = "0 Bytes/s";
            lbStartTime.Text = DateTime.Now.AddSeconds(-1).ToString("HH:mm:ss");
            lbEndTime.Text = DateTime.Now.ToString("HH:mm:ss");
            lbUpSpeed.Text = "0 Bytes/s";
            lbDownSpeed.Text = "0 Bytes/s";
            UpdateLayout();
            trayIcon.Icon = Properties.Resources.U0D0;
            trayIcon.Text = "Initializing...";
            startMinimizedToolStripMenuItem.Checked = startMinimized;
            startOnLogonToolStripMenuItem.Checked = startOnLogon;
            if (startMinimized)
                Visible = false;
        }

        private void UpdateLayout()
        {
            int lMargin = Math.Max(lbMaxSpeed.Width, lbMinSpeed.Width);
            int bMargin = Math.Max(Math.Max(lbUpSpeed.Height, lbDownSpeed.Height), Math.Max(lbStartTime.Height, lbEndTime.Height));

            int tSpace = ClientSize.Height - bMargin;

            lbMaxSpeed.Location = new Point(lMargin - lbMaxSpeed.Width, 0);
            lbMinSpeed.Location = new Point(lMargin - lbMinSpeed.Width, tSpace - lbMinSpeed.Height);

            lbStartTime.Location = new Point(lMargin, tSpace);
            lbEndTime.Location = new Point(ClientSize.Width - lbEndTime.Width, tSpace);

            int rSpace = ClientSize.Width - lMargin;

            picGraph.SetBounds(lMargin, 0, rSpace, tSpace);

            int middle = lMargin + rSpace / 2;

            lbUpSpeed.Location = new Point(middle - lbUpSpeed.Width, tSpace);
            lbDownSpeed.Location = new Point(middle + 1, tSpace);
        }

        private void UpdateGraph()
        {
            picGraph.Refresh();
        }

        private string FormatUSize(ulong bytes)
        {
            double dbytes = bytes;

            if (bytes < 1024)
                return bytes.ToString() + " Bytes/s";

            dbytes /= 1024.0;

            if (dbytes < 1024)
                return dbytes.ToString("#0.00") + " KB/s";

            dbytes /= 1024.0;

            if (dbytes < 1024)
                return dbytes.ToString("#0.00") + " MBs/s";

            dbytes /= 1024.0;

            // Maybe... someday...
            return dbytes.ToString("#0.00") + " GBs/s";
        }

        private void tmrUpdate_Tick(object sender, EventArgs e)
        {
            DateTime currentCheck = DateTime.Now;
            TimeSpan timeDiff = (currentCheck - lastCheck);

            if (firstUpdate)
            {
                firstUpdate = false;

                if (startMinimized)
                    Visible = false;
            }

            if (timeDiff < TimeSpan.FromSeconds(1))
                return;

            UpdateSpeeds();

            double spanSeconds = (timeStamps.Last.Value.Item1 - timeStamps.First.Value.Item1).TotalSeconds;

            lbMinSpeed.Text = FormatUSize(lastMinSpeed);
            lbMaxSpeed.Text = FormatUSize(lastMaxSpeed);
            lbStartTime.Text = currentCheck.AddSeconds(-spanSeconds).ToString("HH:mm:ss");
            lbEndTime.Text = currentCheck.ToString("HH:mm:ss");
            lbUpSpeed.Text = FormatUSize(timeStamps.Last.Value.Item3);
            lbDownSpeed.Text = FormatUSize(timeStamps.Last.Value.Item2);

            UpdateLayout();
            UpdateGraph();

            bool sendActivity = (timeStamps.Last.Value.Item3 > 0);
            bool recvActivity = (timeStamps.Last.Value.Item2 > 0);

            if (sendActivity && recvActivity)
            {
                trayIcon.Icon = Properties.Resources.U1D1;
            }
            else if (sendActivity)
            {
                trayIcon.Icon = Properties.Resources.U1D0;
            }
            else if (recvActivity)
            {
                trayIcon.Icon = Properties.Resources.U0D1;
            }
            else
            {
                trayIcon.Icon = Properties.Resources.U0D0;
            }

            trayIcon.Text = "Send: " + lbUpSpeed.Text + "; Receive: " + lbDownSpeed.Text;

            lastCheck = currentCheck;
        }
        
        private void UpdateSpeeds()
        {
            DateTime maxStamp = DateTime.MinValue;

            ulong bytesReceivedPerSec = 0;
            ulong bytesSentPerSec = 0;

            foreach (ManagementObject adapter in searcher.Get())
            {
                var name = (string) adapter["Name"];
                var sent = adapter["BytesReceivedPerSec"];
                var recv = adapter["BytesSentPerSec"];
                var curStamp = DateTime.FromBinary((long)(ulong)adapter["Timestamp_Sys100NS"]).AddYears(1600);

                if(curStamp > maxStamp)
                    maxStamp = curStamp;

                // XP seems to have uint32's there, but win7 has uint64's
                var curRecv = recv is uint ? (uint) recv : (ulong) recv;
                var curSend = sent is uint ? (uint) sent : (ulong) sent;

                var lstRecv = curRecv;
                var lstSend = curSend;
                var lstStamp = curStamp;

                if (prevLastRecv.ContainsKey(name)) lstRecv = prevLastRecv[name];
                if (prevLastSend.ContainsKey(name)) lstSend = prevLastSend[name];
                if (prevLastStamp.ContainsKey(name)) lstStamp = prevLastStamp[name];

                var diffRecv = (curRecv - lstRecv);
                var diffSend = (curSend - lstSend);
                var diffStamp = (curStamp - lstStamp);

                prevLastRecv[name] = curRecv;
                prevLastSend[name] = curSend;
                prevLastStamp[name] = curStamp;

                if (diffStamp <= TimeSpan.Zero)
                    continue;

                double diffSeconds = diffStamp.TotalSeconds;

                if (diffSeconds > MaxSecondSpan)
                    continue;

                bytesReceivedPerSec += (ulong) (diffRecv/diffSeconds);
                bytesSentPerSec += (ulong) (diffSend/diffSeconds);
            }

            timeStamps.AddLast(new Tuple<DateTime, ulong, ulong>(maxStamp, bytesReceivedPerSec, bytesSentPerSec));

            var totalSpan = timeStamps.Last.Value.Item1 - timeStamps.First.Value.Item1;
            while (totalSpan.TotalSeconds > MaxSecondSpan && timeStamps.Count > 1)
            {
                timeStamps.RemoveFirst();
                totalSpan = timeStamps.Last.Value.Item1 - timeStamps.First.Value.Item1;
            }

            var minSpeed = Math.Min(timeStamps.First.Value.Item2, timeStamps.First.Value.Item3);
            var maxSpeed = Math.Max(timeStamps.First.Value.Item2, timeStamps.First.Value.Item3);

            foreach(var ts in timeStamps)
            {
                minSpeed = Math.Min(minSpeed, Math.Min(ts.Item2, ts.Item3));
                maxSpeed = Math.Max(maxSpeed, Math.Max(ts.Item2, ts.Item3));
            }

            lastMaxSpeed = maxSpeed;
            lastMinSpeed = minSpeed;
        }

        private void picGraph_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var gSize = picGraph.ClientSize;

            if (lastMaxSpeed <= lastMinSpeed)
                return;

            if (timeStamps.Count == 0)
                return;

            var pb = new SolidBrush(Color.FromArgb(255, 48, 48, 255));
            var pg = new SolidBrush(Color.FromArgb(255, 32, 255, 64));
            var pr = new SolidBrush(Color.FromArgb(255, 255, 24, 32));

            var tt = (lastMaxSpeed - lastMinSpeed);

            const int top = 0;
            int bottom = gSize.Height - 1;

            var xStart = gSize.Width - 1;
            var tStart = timeStamps.Last.Value.Item1;
            var xLast = xStart + 1;
            ulong iMaxSend = timeStamps.Last.Value.Item3;
            ulong iMaxRecv = timeStamps.Last.Value.Item2;

            for (var current = timeStamps.Last; current != null; current = current.Previous)
            {
                var td = Math.Round((tStart - current.Value.Item1).TotalSeconds);
                var xCurrent = (int) Math.Round(xStart - td, 0);
                if (xCurrent < 0)
                    break;

                iMaxSend = Math.Max(current.Value.Item3, iMaxSend);
                iMaxRecv = Math.Max(current.Value.Item2, iMaxRecv);

                if(xCurrent == xLast)
                    continue;

                var midBottom = bottom - (int)(iMaxSend * (uint)gSize.Height / tt);
                var midTop = top + (int)(iMaxRecv * (uint)gSize.Height / tt);

                if (midBottom < midTop)
                {
                    g.FillRectangle(pg, new Rectangle(
                                   new Point(xCurrent, midTop),
                                   new Size(xLast - xCurrent, midBottom - midTop)));
                    
                    int t = midBottom;
                    midBottom = midTop;
                    midTop = t;
                }
                
                g.FillRectangle(pb, new Rectangle(
                    new Point(xCurrent, top),
                    new Size(xLast - xCurrent, midTop - top)));

                g.FillRectangle(pr, new Rectangle(
                    new Point(xCurrent, midBottom),
                    new Size(xLast - xCurrent, bottom - midBottom)));

                iMaxSend = current.Value.Item3;
                iMaxRecv = current.Value.Item2;

                xLast = xCurrent;
            }

            pr.Dispose();
            pg.Dispose();
            pb.Dispose();
        }

        private void XMeterDisplay_Resize(object sender, EventArgs e)
        {
            UpdateLayout();
            UpdateGraph();
        }

        private void toggleDisplayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Visible = !Visible;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            realClosing = true;
            Close();
        }

        private void startMinimizedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            startMinimized = !startMinimized;
            startMinimizedToolStripMenuItem.Checked = startMinimized;
            WriteSettings();
        }

        private void trayIcon_DoubleClick(object sender, EventArgs e)
        {
            Visible = !Visible;
        }

        private void startOnLogonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            startOnLogon = !startOnLogon;
            startOnLogonToolStripMenuItem.Checked = startOnLogon;
            WriteSettings();
        }

        private void XMeterDisplay_FormClosing(object sender, FormClosingEventArgs e)
        {
            if ((e.CloseReason != CloseReason.UserClosing) || realClosing) 
                return;

            e.Cancel = true;
            Visible = false;
        }

        private void nearlyInvisibleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Opacity = 0.10;
            WriteSettings();
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            Opacity = 0.30;
            WriteSettings();
        }

        private void seethroughToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Opacity = 0.50;
            WriteSettings();
        }

        private void overlayedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Opacity = 0.90;
            WriteSettings();
        }

        private void opaqueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Opacity = 1.00;
            WriteSettings();
        }
    }
}
