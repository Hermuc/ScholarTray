using System;
using System.IO;
using System.Reflection;

namespace ScholarTray.Core
{
    /// <summary>
    /// 把编译期嵌入到程序集里的 scholartools.exe 释放到一个进程私有临时目录，
    /// 供 ToolRunner 以子进程方式执行；用完即删、退出清理，尽量不留痕迹。
    /// 这样交付物只需一个 ScholarTray.exe，无需在用户目录旁另放工具本体。
    /// </summary>
    internal sealed class EmbeddedTool : IDisposable
    {
        /// <summary>嵌入资源名（与 build.cmd 中 /resource 的第二段一致，区分大小写）。</summary>
        public const string ResourceName = "ScholarTray.EmbeddedTools.scholartools.exe";
        private const string ToolFileName = "scholartools.exe";

        private readonly Logger _log;
        private string _tempDir;
        private string _exePath;

        public EmbeddedTool(Logger log) { _log = log; }

        /// <summary>程序集里是否确实嵌入了工具本体。</summary>
        public static bool IsEmbedded
        {
            get
            {
                try
                {
                    return Assembly.GetExecutingAssembly().GetManifestResourceInfo(ResourceName) != null;
                }
                catch { return false; }
            }
        }

        /// <summary>嵌入资源的字节数；未嵌入返回 -1。</summary>
        public static long EmbeddedSize
        {
            get
            {
                try
                {
                    using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
                    {
                        if (s == null) return -1;
                        return s.Length;
                    }
                }
                catch { return -1; }
            }
        }

        /// <summary>
        /// 确保工具已释放到临时目录并返回可执行文件全路径；失败返回 null。
        /// 幂等：同一进程内只释放一次。
        /// </summary>
        public string EnsureExtracted()
        {
            if (_exePath != null && File.Exists(_exePath)) return _exePath;
            try
            {
                _tempDir = Path.Combine(Path.GetTempPath(), "ScholarTray-" + AppPaths.InstanceKey);
                Directory.CreateDirectory(_tempDir);
                string target = Path.Combine(_tempDir, ToolFileName);

                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
                {
                    if (s == null) { _log.Error("嵌入资源缺失: " + ResourceName); return null; }
                    using (FileStream fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        s.CopyTo(fs, 81920);
                    }
                }
                _exePath = target;
                _log.Info("已释放内嵌工具: " + target + " (" + new FileInfo(target).Length + " B)");
                return target;
            }
            catch (Exception ex)
            {
                _log.Error("释放内嵌工具失败: " + ex.Message);
                return null;
            }
        }

        /// <summary>尽力删除已释放的工具本体（进程退出后系统会自动解锁，故不抛错）。</summary>
        public void TryRemoveExtracted()
        {
            try
            {
                if (_exePath != null && File.Exists(_exePath)) File.Delete(_exePath);
            }
            catch { }
        }

        /// <summary>退出时清理整个临时目录。可在工具仍在运行时被调用（静默失败，交由系统清理 %TEMP%）。</summary>
        public void Dispose()
        {
            try
            {
                if (_tempDir != null && Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                    _log.Info("已清理临时目录: " + _tempDir);
                }
            }
            catch (Exception ex) { _log.Warn("清理临时目录失败（可忽略）: " + ex.Message); }
        }
    }
}
