using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ScholarTray.Core
{
    /// <summary>所有路径的唯一来源：只依赖 exe 自身目录与系统标准目录，不硬编码盘符。</summary>
    internal static class AppPaths
    {
        private static readonly string _base = Normalize(AppDomain.CurrentDomain.BaseDirectory);

        public static string BaseDirectory { get { return _base; } }
        public static string ConfigFile { get { return Path.Combine(_base, "ScholarTray.ini"); } }
        public static string LogFile { get { return Path.Combine(_base, "ScholarTray.log"); } }

        public static string HostsFile
        {
            get
            {
                try
                {
                    string sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string a = Path.Combine(sys, "drivers", "etc", "hosts");
                    if (File.Exists(a)) return a;
                }
                catch { }
                string root = Environment.GetEnvironmentVariable("SystemRoot");
                if (!string.IsNullOrEmpty(root))
                    return Path.Combine(root, "System32", "drivers", "etc", "hosts");
                return @"C:\Windows\System32\drivers\etc\hosts";
            }
        }

        /// <summary>按目录派生实例名，避免字符串哈希随机化，且支持多副本共存。</summary>
        public static string InstanceKey
        {
            get
            {
                byte[] b = Encoding.UTF8.GetBytes(_base.ToLowerInvariant());
                using (MD5 md5 = MD5.Create())
                {
                    byte[] h = md5.ComputeHash(b);
                    StringBuilder sb = new StringBuilder(8);
                    for (int i = 0; i < 4; i++) sb.Append(h[i].ToString("x2"));
                    return sb.ToString();
                }
            }
        }

        /// <summary>
        /// 工具位置候选，按优先级：显式配置 → exe 同目录 → 上级目录 → 用户下载目录 → 各固定盘常见目录。
        /// 全部基于运行时环境推导，不写死任何盘符或用户名。
        /// </summary>
        public static IEnumerable<string> ToolCandidates(string configured)
        {
            if (!string.IsNullOrEmpty(configured)) yield return configured;
            yield return Path.Combine(_base, "scholartools.exe");
            yield return Path.Combine(_base, "学术工具", "scholartools.exe");

            string parent = Path.GetDirectoryName(_base);
            if (!string.IsNullOrEmpty(parent))
            {
                yield return Path.Combine(parent, "scholartools.exe");
                yield return Path.Combine(parent, "学术工具", "scholartools.exe");
            }

            string profile = Environment.GetEnvironmentVariable("USERPROFILE");
            if (!string.IsNullOrEmpty(profile))
            {
                yield return Path.Combine(profile, "Downloads", "scholartools.exe");
                yield return Path.Combine(profile, "Downloads", "学术工具", "scholartools.exe");
            }

            // 迭代器内不能在带 catch 的 try 中 yield，故先收集再产出
            List<string> roots = new List<string>();
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach (DriveInfo di in drives)
                {
                    if (di.DriveType == DriveType.Fixed) roots.Add(di.RootDirectory.FullName);
                }
            }
            catch { }
            foreach (string root in roots)
            {
                yield return Path.Combine(root, "UserData", "Downloads", "学术工具", "scholartools.exe");
            }
        }

        public static string ResolveTool(string configured)
        {
            foreach (string c in ToolCandidates(configured))
            {
                try { if (File.Exists(c)) return Path.GetFullPath(c); } catch { }
            }
            return null;
        }

        private static string Normalize(string p)
        {
            try { return Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch { return p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        }
    }
}
