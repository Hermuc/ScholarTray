using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScholarTray.Core;

namespace ScholarTray.UI
{
    /// <summary>ApplicationContext：仅托盘，无主窗口；状态机与 UI 编排。</summary>
    internal sealed class TrayApp : ApplicationContext
    {
        private readonly AppConfig _cfg;
        private readonly Logger _log;
        private readonly StateProbe _probe;
        private readonly EmbeddedTool _embedded;
        private readonly ToolRunner _runner;
        private readonly WifiProbe _wifi;
        private readonly TrayMenu _menu;
        private readonly NotifyIcon _notify;
        private readonly Icon _iconOn;
        private readonly Icon _iconOff;
        private readonly Timer _poll;

        private ToolState _state = ToolState.Unknown;
        private WifiStatus _wifiStatus;
        private bool _busy;
        private bool _exiting;
        private bool _exitAfterAction;
        private bool _cleaned;
        private Task<object[]> _task;

        public TrayApp()
        {
            _cfg = AppConfig.Load();
            _log = new Logger(_cfg.LogEnabled, AppPaths.LogFile);
            _log.Info("=== ScholarTray 启动 ===");
            _probe = new StateProbe(_log);
            _embedded = new EmbeddedTool(_log);
            _runner = new ToolRunner(_cfg, _log, _embedded);
            _wifi = new WifiProbe(_log);
            _iconOn = TrayResources.Opened;
            _iconOff = TrayResources.Closed;

            _menu = new TrayMenu();
            _menu.ToggleItem.Click += delegate(object s, EventArgs e) { Toggle(); };
            _menu.RefreshItem.Click += delegate(object s, EventArgs e) { RefreshState(); };
            _menu.ExitItem.Click += delegate(object s, EventArgs e) { ExitApplication(); };

            _notify = new NotifyIcon();
            _notify.Icon = _iconOff;
            _notify.Text = "学术工具（检测中）";
            _notify.Visible = true;
            // 设置 ContextMenuStrip 后右键自动弹菜单，无需在 MouseClick 里手动 Show
            _notify.ContextMenuStrip = _menu.Menu;
            _notify.MouseClick += OnMouseClick;

            // 用 WinForms Timer 轮询后台任务结果，避免 Application.Run 之前
            // SynchronizationContext 尚未建立导致的 ContinueWith 调度失败
            _poll = new Timer();
            _poll.Interval = 300;
            _poll.Tick += OnPoll;

            RefreshState();
            if (_cfg.AutoOpenOnStart && _state == ToolState.Closed)
                RunAction(true, false);   // 内部会先过校园网网关
            else if (_state == ToolState.Unknown)
                Bubble("检测到半安装状态，建议点『修复：重新开启』或『关闭并退出』", ToolTipIcon.Warning);
        }

        private void OnMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) Toggle();
        }

        private void Toggle()
        {
            if (_busy || _exiting) return;
            // 半安装态优先尝试重新开启做修复
            RunAction(_state != ToolState.Opened, false);
        }

        private void RefreshState()
        {
            if (_busy || _exiting) return;
            _state = _probe.Detect();
            if (_cfg.WifiCheckEnabled) _wifiStatus = _wifi.Query(_cfg.WifiAllowedSsid);
            Render();
        }

        /// <summary>
        /// 校园网网关：仅在「开启」时校验。关闭/卸载任何时候都允许，避免断网后无法关闭。
        /// 每次都重新探测，防止启动时缓存的网络状态过期（用户可能中途切换 WiFi）。
        /// 返回 true 表示允许继续。
        /// </summary>
        private bool PassWifiGate()
        {
            if (!_cfg.WifiCheckEnabled) return true;

            _wifiStatus = _wifi.Query(_cfg.WifiAllowedSsid);   // 实时探测，不用缓存
            Render();

            if (_wifiStatus.Allowed) return true;
            if (_cfg.WifiBypassWhenNotCampus)
            {
                _log.Warn("非校园网但已配置放行: " + _wifiStatus.Describe());
                return true;
            }

            _log.Warn("校园网检查未通过: " + _wifiStatus.Describe());
            string hint = "请连接校园网（" + string.Join(" / ", _cfg.WifiAllowedSsid.ToArray()) + "）后再试。";
            Bubble(hint, ToolTipIcon.Warning);
            try
            {
                MessageBox.Show(
                    hint + Environment.NewLine + Environment.NewLine +
                    "当前：" + _wifiStatus.Describe(),
                    "学术工具 · 需要连接校园网",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch { }
            return false;
        }

        /// <summary>返回是否成功启动了后台动作。</summary>
        private bool RunAction(bool open, bool exitAfter)
        {
            // 只挡并发：_exiting 由调用方守卫，退出流程本身仍需执行卸载动作
            if (_busy) return false;

            if (open && !PassWifiGate()) return false;

            _exitAfterAction = exitAfter;
            _busy = true;
            Render();
            _log.Info("开始动作: " + (open ? "开启" : "关闭"));
            _task = Task.Factory.StartNew(delegate()
            {
                ToolResult r = _runner.Run(open);
                ToolState after = _probe.Detect();   // 端到端复检，不依赖输出文案
                return new object[] { r, after, open };
            });
            _poll.Start();
            return true;
        }

        private void OnPoll(object sender, EventArgs e)
        {
            if (_task == null || !_task.IsCompleted) return;
            _poll.Stop();
            ToolResult r;
            ToolState after;
            bool open;
            try
            {
                object[] arr = _task.Result;
                r = (ToolResult)arr[0];
                after = (ToolState)arr[1];
                open = (bool)arr[2];
            }
            catch (Exception ex)
            {
                _log.Error("后台任务异常: " + ex.Message);
                r = new ToolResult();
                r.FailureReason = ex.Message;
                after = _probe.Detect();
                open = false;
            }
            _task = null;
            _busy = false;
            _state = after;

            ToolState expected = open ? ToolState.Opened : ToolState.Closed;
            string detail = r.FailureReason.Length > 0 ? "：" + r.FailureReason : "";
            if (after == expected)
                Bubble(open ? "学术工具已开启" : "学术工具已关闭", ToolTipIcon.Info);
            else
                Bubble("动作未完全生效（" + StateProbe.Name(after) + "）" + detail, ToolTipIcon.Warning);
            Render();

            if (_exitAfterAction) { _exitAfterAction = false; DoExit(); }
        }

        private void ExitApplication()
        {
            if (_exiting) return;
            _exiting = true;
            _log.Info("退出流程：先关闭学术工具");
            if (_state == ToolState.Closed) { DoExit(); return; }
            if (!RunAction(false, true)) DoExit();   // busy 中无法执行时仍干净退出
        }

        private void DoExit() { ExitThread(); }

        private void Render()
        {
            bool on = (_state == ToolState.Opened);
            _notify.Icon = on ? _iconOn : _iconOff;
            _notify.Text = "学术工具：" + StateProbe.Name(_state);
            _menu.Render(_state, _busy, _wifiStatus, _cfg.WifiCheckEnabled);
        }

        private void Bubble(string text, ToolTipIcon icon)
        {
            try
            {
                if (_notify != null && _notify.Visible)
                    _notify.ShowBalloonTip(3000, "学术工具", text, icon);
            }
            catch { }
        }

        protected override void ExitThreadCore()
        {
            Cleanup();
            base.ExitThreadCore();
        }

        private void Cleanup()
        {
            if (_cleaned) return;
            _cleaned = true;
            _log.Info("=== ScholarTray 退出，清理托盘 ===");
            try { if (_embedded != null) _embedded.Dispose(); } catch { }
            try { if (_poll != null) { _poll.Stop(); _poll.Dispose(); } } catch { }
            try { if (_notify != null) { _notify.Visible = false; _notify.Dispose(); } } catch { }
            try { if (_menu != null) _menu.Menu.Dispose(); } catch { }
            try { if (_iconOn != null) _iconOn.Dispose(); } catch { }
            try { if (_iconOff != null) _iconOff.Dispose(); } catch { }
        }
    }
}
