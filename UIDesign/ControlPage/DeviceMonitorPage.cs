using AdaWeldSystem.MainDeviceControl.DeviceWorkflow;
using AdaWeldSystem.MotionControl;
using System;
using System.Globalization;
using System.Windows.Forms;

namespace AdaWeldSystem.UIDesign.ControlPage
{
    /// <summary>设备监控页，展示焊接头实时温度与气压。</summary>
    public partial class DeviceMonitorPage : UserControl
    {
        private const int RefreshIntervalMs = 500;

        public DeviceMonitorPage()
        {
            InitializeComponent();
            timerRefresh.Interval = RefreshIntervalMs;
            timerRefresh.Tick += TimerRefresh_Tick;
            HandleCreated += DeviceMonitorPage_HandleCreated;
            HandleDestroyed += DeviceMonitorPage_HandleDestroyed;
        }

        private void DeviceMonitorPage_HandleCreated(object sender, EventArgs e)
        {
            if (!timerRefresh.Enabled) timerRefresh.Start();
        }

        private void DeviceMonitorPage_HandleDestroyed(object sender, EventArgs e)
        {
            HandleCreated -= DeviceMonitorPage_HandleCreated;
            timerRefresh.Stop();
            timerRefresh.Tick -= TimerRefresh_Tick;
        }

        /// <summary>刷新温度显示：采集与贴值同步在 UI 线程完成。</summary>
        public void RefreshTemperature()
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            ApplyTemperature(CollectTemperature());
        }

        private void TimerRefresh_Tick(object sender, EventArgs e)
        {
            if (!Visible) return;
            RefreshTemperature();
        }

        /// <summary>采集温度与气压快照。</summary>
        private static TemperatureSnapshot CollectTemperature()
        {
            LaserHeadTemperatureData temp = LaserWeldHeadWorkflow.Instance.LastTemperature;
            HeadAlarmStatus alarm = LaserWeldHeadWorkflow.Instance.LastAlarmStatus;

            MontionManager motion = MontionManager.Instance;
            WeldHeadSlaveDevice head = motion?.WeldHead;
            if (temp == null && head != null && head.IsInitialized)
            {
                temp = head.CurrentTemperature;
                alarm = head.CurrentAlarmStatus;
            }

            TemperatureSnapshot snapshot = new TemperatureSnapshot();
            if (temp == null)
            {
                snapshot.Lens = "—";
                snapshot.Ambient = "—";
                snapshot.Rise = "—";
                snapshot.Pressure = "—";
            }
            else
            {
                snapshot.Lens = temp.ProtectiveLensTemp.ToString("0.0", CultureInfo.InvariantCulture);
                snapshot.Ambient = temp.ProtectiveLensAmbientTemp.ToString("0.0", CultureInfo.InvariantCulture);
                snapshot.Rise = temp.ProtectiveLensRiseTemp.ToString("0.0", CultureInfo.InvariantCulture);
                snapshot.Pressure = temp.CuttingAirPressure.ToString("0.00", CultureInfo.InvariantCulture);
            }

            if (alarm == null)
            {
                snapshot.State = "报警状态：—";
            }
            else if (!alarm.HasAlarm)
            {
                snapshot.State = "报警状态：正常";
            }
            else
            {
                snapshot.State = "报警状态：" + alarm.Describe();
            }
            return snapshot;
        }

        /// <summary>把温度快照贴到标签。</summary>
        private void ApplyTemperature(TemperatureSnapshot snapshot)
        {
            if (snapshot == null) return;
            lblTempLensValue.Text = snapshot.Lens;
            lblTempAmbientValue.Text = snapshot.Ambient;
            lblTempRiseValue.Text = snapshot.Rise;
            lblTempPressureValue.Text = snapshot.Pressure;
            lblTempState.Text = snapshot.State;
        }

        private sealed class TemperatureSnapshot
        {
            public string Lens;
            public string Ambient;
            public string Rise;
            public string Pressure;
            public string State;
        }
    }
}
