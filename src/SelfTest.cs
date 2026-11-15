using System;
using System.Collections.Generic;
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
            AppConfig cfg = AppConfig.Load();
            Logger log = new Logger(false, AppPaths.LogFile);
            StateProbe sp = new StateProbe(log);
            WifiProbe wp = new WifiProbe(log);

            Console.WriteLine("== ScholarTray self-test (read-only) ==");
            Console.WriteLine("BaseDirectory : " + AppPaths.BaseDirectory);
            Console.WriteLine("ConfigFile    : " + AppPaths.ConfigFile);
            Console.WriteLine("HostsFile     : " + AppPaths.HostsFile);
            Console.WriteLine("InstanceKey   : " + AppPaths.InstanceKey);
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
    }
}
