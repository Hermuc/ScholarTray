using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace ScholarTray.Core
{
    internal sealed class ToolResult
    {
        public bool Started;
        public bool Exited;
        public int ExitCode;
        public string Output = "";
        public string Error = "";
        public string FailureReason = "";
    }

    /// <summary>只负责调用 scholartools.exe，不碰 UI、不解释业务结果。</summary>
    internal sealed class ToolRunner
    {
        private readonly AppConfig _cfg;
        private readonly Logger _log;

        public ToolRunner(AppConfig cfg, Logger log) { _cfg = cfg; _log = log; }

        public ToolResult Run(bool open)
        {
            ToolResult r = new ToolResult();
            string exe = AppPaths.ResolveTool(_cfg.ToolPath);
            if (exe == null)
            {
                r.FailureReason = "找不到 scholartools.exe";
                _log.Error(r.FailureReason);
                return r;
            }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exe;
                psi.WorkingDirectory = AppPaths.BaseDirectory;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                if (_cfg.Mode == InvokeMode.CliArgs)
                    psi.Arguments = open ? "install" : "uninstall";

                _log.Info("调用: " + exe + " 动作=" + (open ? "开启" : "关闭") + " 模式=" + _cfg.Mode);
                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.Start();
                    r.Started = true;

                    // 默认复刻教程里的键盘操作：选 1/2 再补一个回车供 pause 使用。
                    // 多余的换行在 argv 模式下无害；stdin 立刻关闭，杜绝阻塞。
                    string input = (_cfg.Mode == InvokeMode.MenuInput)
                        ? (open ? "1\n\n" : "2\n\n")
                        : "\n\n";
                    try { p.StandardInput.Write(input); p.StandardInput.Close(); }
                    catch (Exception ex) { _log.Warn("写入 stdin 失败: " + ex.Message); }

                    StringBuilder so = new StringBuilder();
                    StringBuilder se = new StringBuilder();
                    Thread t1 = new Thread(delegate() { try { so.Append(p.StandardOutput.ReadToEnd()); } catch { } });
                    Thread t2 = new Thread(delegate() { try { se.Append(p.StandardError.ReadToEnd()); } catch { } });
                    t1.IsBackground = true;
                    t2.IsBackground = true;
                    t1.Start();
                    t2.Start();

                    r.Exited = p.WaitForExit(_cfg.TimeoutSeconds * 1000);
                    if (r.Exited)
                    {
                        r.ExitCode = p.ExitCode;
                        t1.Join(3000);
                        t2.Join(3000);
                    }
                    else
                    {
                        r.FailureReason = "执行超时（未强杀，避免留下半安装状态）";
                        _log.Warn(r.FailureReason);
                    }
                    r.Output = so.ToString();
                    r.Error = se.ToString();
                }
                // 退出码不可信：实测非管理员时 RC=0 却打印错误，故只记日志，成功与否由复检判定
                _log.Info("返回: started=" + r.Started + " exited=" + r.Exited + " rc=" + r.ExitCode);
                if (r.Output.Length > 0) _log.Info("stdout: " + Compact(r.Output));
                if (r.Error.Length > 0) _log.Warn("stderr: " + Compact(r.Error));
            }
            catch (Exception ex)
            {
                r.FailureReason = ex.Message;
                _log.Error("调用异常: " + ex.Message);
            }
            return r;
        }

        private static string Compact(string s)
        {
            string t = s.Replace("\r", " ").Replace("\n", " | ").Trim();
            return t.Length > 1200 ? t.Substring(t.Length - 1200) : t;
        }
    }
}
