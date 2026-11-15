using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ScholarTray.UI
{
    /// <summary>图标从嵌入资源加载，产物不依赖外部 ico 文件（单文件绿色）。</summary>
    internal static class TrayResources
    {
        public static Icon Opened { get { return Load("ScholarTray.Resources.AppIcon.ico"); } }
        public static Icon Closed { get { return Load("ScholarTray.Resources.AppIconOff.ico"); } }

        private static Icon Load(string resourceName)
        {
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (s != null)
            {
                using (s) { return new Icon(s); }
            }
            // 兜底：复制系统图标而不是直接返回共享实例，保证调用方可安全 Dispose
            return new Icon(SystemIcons.Application, 16, 16);
        }
    }
}
