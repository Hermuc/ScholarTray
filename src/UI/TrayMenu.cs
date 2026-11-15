using System.Windows.Forms;
using ScholarTray.Core;

namespace ScholarTray.UI
{
    /// <summary>只负责构建菜单与渲染文本，不绑定业务动作。</summary>
    internal sealed class TrayMenu
    {
        public readonly ToolStripMenuItem StatusItem;
        public readonly ToolStripMenuItem NetworkItem;
        public readonly ToolStripMenuItem ToggleItem;
        public readonly ToolStripMenuItem RefreshItem;
        public readonly ToolStripMenuItem ExitItem;
        public readonly ContextMenuStrip Menu;

        public TrayMenu()
        {
            Menu = new ContextMenuStrip();
            StatusItem = Item("状态：检测中", false);
            Menu.Items.Add(StatusItem);
            NetworkItem = Item("网络：检测中", false);
            Menu.Items.Add(NetworkItem);
            Menu.Items.Add(new ToolStripSeparator());
            ToggleItem = Item("开启学术工具", true);
            Menu.Items.Add(ToggleItem);
            RefreshItem = Item("重新检测状态", true);
            Menu.Items.Add(RefreshItem);
            Menu.Items.Add(new ToolStripSeparator());
            ExitItem = Item("关闭并退出", true);
            Menu.Items.Add(ExitItem);
        }

        public void Render(ToolState s, bool busy, WifiStatus wifi, bool wifiChecked)
        {
            StatusItem.Text = "状态：" + StateProbe.Name(s) + (busy ? "（处理中…）" : "");
            if (wifiChecked && wifi != null) NetworkItem.Text = "网络：" + wifi.Describe();
            else NetworkItem.Text = "网络：（未检测）";

            if (s == ToolState.Opened) ToggleItem.Text = "关闭学术工具（临时）";
            else if (s == ToolState.Unknown) ToggleItem.Text = "修复：重新开启";
            else ToggleItem.Text = "开启学术工具";

            ToggleItem.Enabled = !busy;
            RefreshItem.Enabled = !busy;
            ExitItem.Enabled = !busy;
        }

        private static ToolStripMenuItem Item(string text, bool enabled)
        {
            ToolStripMenuItem i = new ToolStripMenuItem(text);
            i.Enabled = enabled;
            return i;
        }
    }
}
