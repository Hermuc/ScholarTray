using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ScholarTray.Core
{
    internal enum ToolState { Opened, Closed, Unknown }

    /// <summary>只读探测真实系统状态，不维护自有状态文件，避免与手工操作失配。</summary>
    internal sealed class StateProbe
    {
        private readonly Logger _log;
        public StateProbe(Logger log) { _log = log; }

        public bool HasRootCertificate()
        {
            X509Store store = null;
            try
            {
                store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                foreach (X509Certificate2 c in store.Certificates)
                {
                    if (c.Subject != null &&
                        c.Subject.IndexOf("Scholar Root CA", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _log.Warn("读取根证书存储失败: " + ex.Message);
                return false;
            }
            finally
            {
                if (store != null) { try { store.Close(); } catch { } }
            }
        }

        public bool HasHostsBlock()
        {
            try
            {
                string p = AppPaths.HostsFile;
                if (!File.Exists(p)) { _log.Warn("hosts 不存在: " + p); return false; }
                // 标记是纯 ASCII，用 ASCII 读可避开中文系统代码页差异
                string text = File.ReadAllText(p, Encoding.ASCII);
                return text.IndexOf("# START OF SCHOLAR TOOLS ENTRIES", StringComparison.OrdinalIgnoreCase) >= 0
                    && text.IndexOf("# END OF SCHOLAR TOOLS ENTRIES", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch (Exception ex)
            {
                _log.Warn("读取 hosts 失败: " + ex.Message);
                return false;
            }
        }

        public ToolState Detect()
        {
            bool cert = HasRootCertificate();
            bool hosts = HasHostsBlock();
            _log.Info("状态探测: 根证书=" + cert + " hosts标记=" + hosts);
            if (cert && hosts) return ToolState.Opened;
            if (!cert && !hosts) return ToolState.Closed;
            return ToolState.Unknown;   // 半安装状态，不猜
        }

        public static string Name(ToolState s)
        {
            switch (s)
            {
                case ToolState.Opened: return "已开启";
                case ToolState.Closed: return "已关闭";
                default: return "状态异常";
            }
        }
    }
}
