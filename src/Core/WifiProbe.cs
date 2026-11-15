using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace ScholarTray.Core
{
    internal enum WifiGate
    {
        /// <summary>已连接，且 SSID 命中允许的校园网名。</summary>
        Connected,
        /// <summary>有无线连接，但 SSID 不在允许列表。</summary>
        NotCampus,
        /// <summary>没有任何已连接的无线网络。</summary>
        NoWifi,
        /// <summary>探测失败（API 不可用等），此时按放行处理，避免误伤。</summary>
        Unknown
    }

    internal sealed class WifiStatus
    {
        public WifiGate Gate;
        public string Ssid = "";
        public string Source = "";
        public string Detail = "";

        /// <summary>Unknown 视为放行：检测能力失效不应阻断正常使用。</summary>
        public bool Allowed { get { return Gate == WifiGate.Connected || Gate == WifiGate.Unknown; } }

        public string Describe()
        {
            switch (Gate)
            {
                case WifiGate.Connected: return "已连接校园网：" + Ssid;
                case WifiGate.NotCampus: return "当前无线网络不是校园网：" + (Ssid.Length > 0 ? Ssid : "(未知)");
                case WifiGate.NoWifi: return "未检测到已连接的无线网络";
                default: return "无法判断网络类型" + (Detail.Length > 0 ? "：" + Detail : "");
            }
        }
    }

    /// <summary>
    /// 只读检测当前是否连接指定校园网。按可靠度依次尝试，任一成功即返回：
    ///   1) WMI MSFT_NetConnectionProfile —— 实测非管理员亦可用，能返回真实 SSID（如 "NCWU 2"）
    ///   2) WlanQueryInterface 当前连接 —— 需管理员 + 位置权限，作补充
    ///   3) WlanGetProfileList + dwFlags 标记 —— 仅用于判断「是否存在允许的 profile」，
    ///      不作为当前 SSID 依据（实测 dwIndex 恒为 0，指向首个配置而非当前连接）
    /// 不使用 netsh wlan show interfaces：实测在本机返回 error 5（缺位置权限）。
    /// 全程只读，不修改任何网络设置。
    /// </summary>
    internal sealed class WifiProbe
    {
        private readonly Logger _log;

        public WifiProbe(Logger log) { _log = log; }

        public WifiStatus Query(IList<string> allowedSsid)
        {
            string ssid;
            string src;
            string why;

            // 路径 1：WMI（最可靠，实测可读真实 SSID）
            if (TryWmi(out ssid, out src, out why))
                return Classify(ssid, src, allowedSsid);
            string firstWhy = why;

            // 路径 2：WLAN API 当前连接（管理员下可用）
            if (TryQueryCurrentConnection(out ssid, out src, out why))
                return Classify(ssid, src, allowedSsid);
            if (!string.IsNullOrEmpty(why)) firstWhy = firstWhy + " | " + why;

            // 路径 3：无任何活动 Wi-Fi 接口时，明确判定为 NoWifi
            if (!HasActiveWirelessInterface())
            {
                WifiStatus noWifi = new WifiStatus();
                noWifi.Gate = WifiGate.NoWifi;
                _log.Info("WiFi: 无活动无线接口（" + firstWhy + "）");
                return noWifi;
            }

            // 有无线接口但读不到 SSID：判为 Unknown 并放行，避免误伤
            WifiStatus unk = new WifiStatus();
            unk.Gate = WifiGate.Unknown;
            unk.Detail = firstWhy ?? "无法读取当前 SSID";
            _log.Warn("WiFi 探测失败: " + unk.Detail);
            return unk;
        }

        private WifiStatus Classify(string ssid, string source, IList<string> allowed)
        {
            WifiStatus st = new WifiStatus();
            st.Ssid = ssid ?? "";
            st.Source = source ?? "";
            if (st.Ssid.Length == 0)
            {
                st.Gate = WifiGate.NoWifi;
                return st;
            }
            st.Gate = Match(st.Ssid, allowed) ? WifiGate.Connected : WifiGate.NotCampus;
            _log.Info("WiFi: " + st.Gate + " ssid='" + st.Ssid + "' via " + st.Source);
            return st;
        }

        /// <summary>
        /// 匹配规则：忽略大小写与首尾空白后精确相等，或「允许名 + 分隔符 + 后缀」。
        /// 这样 NCWU / ncwu / NCWU 2 / NCWU-5G / NCWU_2 命中，而 NCWUX / NCWU2 不命中。
        /// </summary>
        public static bool Match(string ssid, IList<string> allowed)
        {
            if (string.IsNullOrEmpty(ssid) || allowed == null) return false;
            string s = ssid.Trim().ToLowerInvariant();
            foreach (string raw in allowed)
            {
                if (raw == null) continue;
                string a = raw.Trim().ToLowerInvariant();
                if (a.Length == 0) continue;
                if (s == a) return true;
                if (s.Length > a.Length && s.StartsWith(a, StringComparison.Ordinal))
                {
                    char sep = s[a.Length];
                    if (sep == ' ' || sep == '-' || sep == '_' || sep == '(' || sep == '.' || sep == '#')
                        return true;
                }
            }
            return false;
        }

        // ---------- 路径 1：WMI ----------
        private bool TryWmi(out string ssid, out string source, out string why)
        {
            ssid = "";
            source = "WMI";
            why = null;
            try
            {
                ManagementScope scope = new ManagementScope(@"\\" + Environment.MachineName + @"\root\StandardCimv2");
                scope.Connect();
                using (ManagementObjectSearcher searcher =
                       new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM MSFT_NetConnectionProfile")))
                {
                    foreach (ManagementBaseObject o in searcher.Get())
                    {
                        object name = o["Name"];
                        object alias = o["InterfaceAlias"];
                        if (name == null) continue;
                        string n = name.ToString();
                        string a = alias == null ? "" : alias.ToString();
                        // 只认无线接口，避免把 Radmin VPN / 以太网 当成 WiFi
                        if (!IsWirelessAlias(a)) continue;
                        if (n.Length > 0) { ssid = n.Trim(); return true; }
                    }
                }
                why = "WMI 未找到无线连接配置";
                return false;
            }
            catch (Exception ex) { why = "WMI 异常: " + ex.Message; return false; }
        }

        private static bool IsWirelessAlias(string alias)
        {
            if (string.IsNullOrEmpty(alias)) return false;
            return alias.IndexOf("WLAN", StringComparison.OrdinalIgnoreCase) >= 0
                || alias.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) >= 0
                || alias.IndexOf("Wireless", StringComparison.OrdinalIgnoreCase) >= 0
                || alias.IndexOf("无线", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ---------- 路径 2：WLAN API 当前连接 ----------
        private bool TryQueryCurrentConnection(out string ssid, out string source, out string why)
        {
            ssid = "";
            source = "WlanQueryInterface";
            why = null;
            IntPtr client = IntPtr.Zero;
            IntPtr list = IntPtr.Zero;
            try
            {
                uint neg;
                int rc = WlanOpenHandle(2, IntPtr.Zero, out neg, out client);
                if (rc != 0) { why = "WlanOpenHandle rc=" + rc; return false; }
                rc = WlanEnumInterfaces(client, IntPtr.Zero, out list);
                if (rc != 0) { why = "WlanEnumInterfaces rc=" + rc; return false; }

                int count = Marshal.ReadInt32(list);
                for (int i = 0; i < count; i++)
                {
                    IntPtr item = new IntPtr(list.ToInt64() + 8 + (long)i * InterfaceInfoSize);
                    GUID g = (GUID)Marshal.PtrToStructure(item, typeof(GUID));
                    int state = Marshal.ReadInt32(item, 528);
                    if (state != 1) continue;   // 1 = connected

                    IntPtr data;
                    uint size;
                    rc = WlanQueryInterface(client, ref g, 7 /* current_connection */,
                                            IntPtr.Zero, out size, out data, IntPtr.Zero);
                    if (rc != 0 || data == IntPtr.Zero) { why = "WlanQueryInterface rc=" + rc; continue; }
                    try
                    {
                        // WLAN_CONNECTION_ATTRIBUTES: state(4) + mode(4) + WCHAR[256] profileName
                        string prof = Marshal.PtrToStringUni(new IntPtr(data.ToInt64() + 8), 256);
                        ssid = TrimAtNul(prof);
                        if (ssid.Length > 0) return true;
                        why = "当前连接 SSID 为空";
                    }
                    finally { WlanFreeMemory(data); }
                }
                if (why == null) why = "无已连接的无线接口";
                return false;
            }
            catch (Exception ex) { why = "WLAN API 异常: " + ex.Message; return false; }
            finally
            {
                if (list != IntPtr.Zero) WlanFreeMemory(list);
                if (client != IntPtr.Zero) WlanCloseHandle(client, IntPtr.Zero);
            }
        }

        /// <summary>是否存在处于 connected 状态的无线接口；用于区分「没连 WiFi」与「读不到 SSID」。</summary>
        private bool HasActiveWirelessInterface()
        {
            IntPtr client = IntPtr.Zero;
            IntPtr list = IntPtr.Zero;
            try
            {
                uint neg;
                if (WlanOpenHandle(2, IntPtr.Zero, out neg, out client) != 0) return false;
                if (WlanEnumInterfaces(client, IntPtr.Zero, out list) != 0) return false;
                int count = Marshal.ReadInt32(list);
                for (int i = 0; i < count; i++)
                {
                    IntPtr item = new IntPtr(list.ToInt64() + 8 + (long)i * InterfaceInfoSize);
                    if (Marshal.ReadInt32(item, 528) == 1) return true;
                }
                return false;
            }
            catch { return false; }
            finally
            {
                if (list != IntPtr.Zero) WlanFreeMemory(list);
                if (client != IntPtr.Zero) WlanCloseHandle(client, IntPtr.Zero);
            }
        }

        private static string TrimAtNul(string s)
        {
            if (s == null) return "";
            int i = s.IndexOf('\0');
            if (i >= 0) s = s.Substring(0, i);
            return s.Trim();
        }

        private const int InterfaceInfoSize = 532;   // GUID(16) + WCHAR[256](512) + state(4)

        [StructLayout(LayoutKind.Sequential)]
        private struct GUID
        {
            public uint Data1;
            public ushort Data2;
            public ushort Data3;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] Data4;
        }

        [DllImport("wlanapi.dll")]
        private static extern int WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

        [DllImport("wlanapi.dll")]
        private static extern int WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        private static extern int WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll")]
        private static extern int WlanQueryInterface(IntPtr hClientHandle, ref GUID pInterfaceGuid, int OpCode,
            IntPtr pReserved, out uint pdwDataSize, out IntPtr ppData, IntPtr pWlanOpcodeValueType);

        [DllImport("wlanapi.dll")]
        private static extern void WlanFreeMemory(IntPtr pMemory);
    }
}
