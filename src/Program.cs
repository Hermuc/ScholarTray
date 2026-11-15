using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ScholarTray.Core;

namespace ScholarTray
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
            Application.ThreadException += OnThreadException;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 单实例：按 exe 所在目录派生名字，不同目录的副本互不冲突（可移植/绿色）
            string name = "Local\\ScholarTray-" + AppPaths.InstanceKey;
            bool created;
            using (Mutex mutex = new Mutex(true, name, out created))
            {
                if (!created) return 0;
                Application.Run(new UI.TrayApp());
                GC.KeepAlive(mutex);
            }
            return 0;
        }

        private static void OnUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            WriteCrash(e.ExceptionObject as Exception);
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            WriteCrash(e.Exception);
        }

        // 崩溃日志是唯一无条件写入的文件：正常运行时不留任何痕迹
        private static void WriteCrash(Exception ex)
        {
            try
            {
                string path = Path.Combine(AppPaths.BaseDirectory, "ScholarTray.crash.log");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " +
                              (ex == null ? "unknown" : ex.ToString()) + Environment.NewLine;
                File.AppendAllText(path, line, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
