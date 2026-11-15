using System;
using System.IO;
using System.Text;

namespace ScholarTray.Core
{
    /// <summary>同目录日志，默认关闭；开启时自动轮转，只保留一份 .old。</summary>
    internal sealed class Logger
    {
        private readonly object _sync = new object();
        private readonly string _path;
        private readonly bool _enabled;

        public Logger(bool enabled, string path)
        {
            _enabled = enabled;
            _path = path;
            if (!_enabled) return;
            try
            {
                FileInfo fi = new FileInfo(_path);
                if (fi.Exists && fi.Length > 256 * 1024)
                {
                    string old = _path + ".old";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(_path, old);
                }
            }
            catch { }
        }

        public void Info(string m) { Write("INFO", m); }
        public void Warn(string m) { Write("WARN", m); }
        public void Error(string m) { Write("ERROR", m); }

        private void Write(string level, string message)
        {
            if (!_enabled) return;
            try
            {
                lock (_sync)
                {
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                                  " [" + level + "] " + message + Environment.NewLine;
                    File.AppendAllText(_path, line, new UTF8Encoding(false));
                }
            }
            catch { }
        }
    }
}
