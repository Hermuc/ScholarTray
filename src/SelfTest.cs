using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ScholarTray.Core;

namespace ScholarTray
{
    /// <summary>只读自检：不带管理员 manifest，用于验证路径解析、状态与网络探测。</summary>
    internal static class SelfTest
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            if (args != null && args.Length > 0 && args[0] == "--probe-tool") return ProbeTool();
            AppConfig cfg = AppConfig.Load();
            Logger log = new Logger(false, AppPaths.LogFile);
            StateProbe sp = new StateProbe(log);
            WifiProbe wp = new WifiProbe(log);

            Console.WriteLine("== ScholarTray self-test (read-only) ==");
            Console.WriteLine("BaseDirectory : " + AppPaths.BaseDirectory);
            Console.WriteLine("ConfigFile    : " + AppPaths.ConfigFile);
            Console.WriteLine("HostsFile     : " + AppPaths.HostsFile);
            Console.WriteLine("InstanceKey   : " + AppPaths.InstanceKey);
            Console.WriteLine("EmbeddedTool  : " + (EmbeddedTool.IsEmbedded ? ("yes (" + EmbeddedTool.EmbeddedSize + " B)") : "NO"));
            Console.WriteLine("UseEmbedded   : " + cfg.UseEmbeddedTool);
            Console.WriteLine("ToolPath      : " + (AppPaths.ResolveTool(cfg.ToolPath) ?? "(not found)"));
            Console.WriteLine("InvokeMode    : " + cfg.Mode);
            Console.WriteLine("Timeout(s)    : " + cfg.TimeoutSeconds);
            Console.WriteLine("AutoOpen      : " + cfg.AutoOpenOnStart);
            Console.WriteLine("LogEnabled    : " + cfg.LogEnabled);
            Console.WriteLine("WifiCheck     : " + cfg.WifiCheckEnabled);
            Console.WriteLine("WifiAllow     : " + string.Join(", ", cfg.WifiAllowedSsid.ToArray()));
            Console.WriteLine("WifiBypass    : " + cfg.WifiBypassWhenNotCampus);
            Console.WriteLine("RootCert      : " + sp.HasRootCertificate());
            Console.WriteLine("HostsBlock    : " + sp.HasHostsBlock());
            Console.WriteLine("Detect        : " + StateProbe.Name(sp.Detect()));

            Console.WriteLine("-- wifi probe --");
            WifiStatus st = wp.Query(cfg.WifiAllowedSsid);
            Console.WriteLine("WifiGate      : " + st.Gate);
            Console.WriteLine("WifiSsid      : [" + st.Ssid + "]");
            Console.WriteLine("WifiSource    : " + st.Source);
            Console.WriteLine("WifiAllowed   : " + st.Allowed);
            Console.WriteLine("WifiDescribe  : " + st.Describe());

            Console.WriteLine("-- match rules (NCWU) --");
            List<string> allowed = new List<string>();
            allowed.Add("NCWU");
            allowed.Add("ncwu");
            string[] cases = { "NCWU", "ncwu", "NCWU 2", "Ncwu", "NCWU-5G", "NCWUX", "NCWU2", "EDU", "" };
            foreach (string c in cases)
                Console.WriteLine(string.Format("  [{0,-8}] -> {1}", c, WifiProbe.Match(c, allowed)));

            Console.WriteLine("-- embedded resources --");
            string[] names = Assembly.GetExecutingAssembly().GetManifestResourceNames();
            foreach (string n in names) Console.WriteLine("  " + n);
            Console.WriteLine("ResourceCount : " + names.Length);
            return 0;
        }

        /// <summary>
        /// 端到端探针：把内嵌工具真实释放到临时目录、校验字节数、试运行读回提示，最后清理。
        /// 只读：不会真正安装证书或改写 hosts（非管理员时工具自身会拒绝执行）。
        /// </summary>
        private static int ProbeTool()
        {
            Console.WriteLine("== embedded tool probe ==");
            Console.WriteLine("EmbeddedTool : " + (EmbeddedTool.IsEmbedded ? ("yes (" + EmbeddedTool.EmbeddedSize + " B)") : "NO"));
            Logger log = new Logger(false, AppPaths.LogFile);
            EmbeddedTool et = new EmbeddedTool(log);
            string exe = et.EnsureExtracted();
            if (exe == null) { Console.WriteLine("Extract      : FAILED"); return 2; }
            FileInfo fi = new FileInfo(exe);
            Console.WriteLine("ExtractPath  : " + exe);
            Console.WriteLine("ExtractSize  : " + fi.Length);
            Console.WriteLine("SizeMatch    : " + (fi.Length == EmbeddedTool.EmbeddedSize));
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = exe;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = System.Text.Encoding.UTF8;
                psi.StandardErrorEncoding = System.Text.Encoding.UTF8;
                using (System.Diagnostics.Process p = new System.Diagnostics.Process())
                {
                    p.StartInfo = psi;
                    p.Start();
                    try { p.StandardInput.Close(); } catch { }
                    bool exited = p.WaitForExit(15000);
                    Console.WriteLine("Exited       : " + exited);
                    if (exited) Console.WriteLine("ExitCode     : " + p.ExitCode);
                    string so = p.StandardOutput.ReadToEnd();
                    string se = p.StandardError.ReadToEnd();
                    Console.WriteLine("StdoutHead   : " + Head(so));
                    if (se.Length > 0) Console.WriteLine("StderrHead   : " + Head(se));
                }
            }
            catch (Exception ex) { Console.WriteLine("RunError     : " + ex.Message); }
            et.TryRemoveExtracted();
            Console.WriteLine("RemovedOnRun : " + (!File.Exists(exe)));
            et.Dispose();
            Console.WriteLine("CleanupDone  : " + (!Directory.Exists(Path.Combine(Path.GetTempPath(), "ScholarTray-" + AppPaths.InstanceKey))));
            return 0;
        }

        private static string Head(string s)
        {
            string t = (s ?? "").Replace("\r", " ").Replace("\n", " | ").Trim();
            return t.Length > 220 ? t.Substring(0, 220) : t;
        }
    }
}
