using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScholarTray.Core
{
    internal enum InvokeMode { MenuInput, CliArgs }

    /// <summary>可选 INI 配置；文件不存在时全部走内置默认值，零配置即可运行。</summary>
    internal sealed class AppConfig
    {
        public string ToolPath { get; private set; }
        public InvokeMode Mode { get; private set; }
        public bool LogEnabled { get; private set; }
        public int TimeoutSeconds { get; private set; }
        public bool AutoOpenOnStart { get; private set; }

        /// <summary>是否启用校园网检测（默认开启）。</summary>
        public bool WifiCheckEnabled { get; private set; }

        /// <summary>允许的校园网 SSID 列表；匹配时忽略大小写，并容忍 "NCWU 2" 这类后缀。</summary>
        public List<string> WifiAllowedSsid { get; private set; }

        /// <summary>不在校园网时是否仍然放行（默认 false，即拦截并提示）。</summary>
        public bool WifiBypassWhenNotCampus { get; private set; }

        public static AppConfig Load()
        {
            AppConfig c = new AppConfig();
            c.ToolPath = null;
            c.Mode = InvokeMode.MenuInput;
            c.LogEnabled = false;          // 绿色：默认不写任何日志
            c.TimeoutSeconds = 180;
            c.AutoOpenOnStart = true;
            c.WifiCheckEnabled = true;
            c.WifiBypassWhenNotCampus = false;
            c.WifiAllowedSsid = new List<string>();
            c.WifiAllowedSsid.Add("NCWU");   // 默认校园网；实际匹配忽略大小写，故 NCWU/ncwu 同时生效
            try
            {
                if (!File.Exists(AppPaths.ConfigFile)) return c;
                string[] lines = File.ReadAllLines(AppPaths.ConfigFile, Encoding.UTF8);
                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1).Trim();
                    switch (k)
                    {
                        case "toolpath": c.ToolPath = v; break;
                        case "invokemode":
                            c.Mode = (v.ToLowerInvariant() == "cli") ? InvokeMode.CliArgs : InvokeMode.MenuInput;
                            break;
                        case "logenabled": c.LogEnabled = IsTrue(v, false); break;
                        case "timeoutseconds":
                            int t;
                            if (int.TryParse(v, out t) && t >= 10 && t <= 900) c.TimeoutSeconds = t;
                            break;
                        case "autoopenonstart": c.AutoOpenOnStart = IsTrue(v, true); break;
                        case "wificheckenabled": c.WifiCheckEnabled = IsTrue(v, true); break;
                        case "wifibypasswhennotcampus": c.WifiBypassWhenNotCampus = IsTrue(v, false); break;
                        case "wifiallowedsid":
                            if (v.Length > 0) c.WifiAllowedSsid = SplitList(v);
                            break;
                    }
                }
            }
            catch { /* 配置损坏不应阻止启动 */ }
            return c;
        }

        /// <summary>支持逗号、分号、竖线分隔，便于一行写多个 SSID。</summary>
        private static List<string> SplitList(string v)
        {
            List<string> list = new List<string>();
            string[] parts = v.Split(new char[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in parts)
            {
                string s = p.Trim();
                if (s.Length > 0 && !list.Contains(s)) list.Add(s);
            }
            if (list.Count == 0) list.Add("NCWU");
            return list;
        }

        private static bool IsTrue(string v, bool fallback)
        {
            string s = v.ToLowerInvariant();
            if (v == "1" || s == "true" || s == "yes" || s == "on") return true;
            if (v == "0" || s == "false" || s == "no" || s == "off") return false;
            return fallback;
        }
    }
}
