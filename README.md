# ScholarTray · 学术工具托盘控制器

> 把「右键管理员运行 → 输入 1 → 回车」这个繁琐流程，变成一个**托盘图标点一下**。

![平台](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)
![框架](https://img.shields.io/badge/.NET-Framework%204.x-512BD4)
![依赖](https://img.shields.io/badge/dependencies-%E9%9B%B6-green)
![许可证](https://img.shields.io/badge/license-MIT-yellow)

---

## 这是什么

`scholartools.exe` 是部分高校使用的**国际学术资源访问工具**（俗称「学术通道」），用来加速访问 GitHub、HuggingFace、Docker Hub、Google 学术等站点。

但它用起来有点麻烦：**每次都要右键「以管理员身份运行」，然后在黑窗口里输入 `1` 再按回车**。想关掉还要再输 `2`。

ScholarTray 就是给这个小工具加一个**托盘图标**：

| 你想做的事 | 以前 | 现在 |
|---|---|---|
| 打开学术工具 | 右键管理员运行 → 输 `1` → 回车 | **双击图标就行**（自动完成） |
| 临时关掉 | 重新运行 → 输 `2` → 回车 | **左键点一下托盘图标** |
| 再打开 | 又输 `1` → 回车 | **再点一下** |
| 彻底关闭 + 退出 | 关掉黑窗口（后台还在跑） | **右键 → 关闭并退出** |

> ⚠️ **重要提醒**：「临时关闭」对应原工具的 `2`，它会把学术工具的证书、hosts 配置**真正移除**，不是「暂停」。再点一次会重新安装。功能上等价于原菜单的 2，但不建议频繁来回切换。

---

## 单文件绿色：工具本体已内嵌

从这一版起，`scholartools.exe` 本体（约 6.25 MB）已**编译进 `ScholarTray.exe`**，交付物只有**一个 exe**：

- 不用再把 `scholartools.exe` 放在旁边，此前那种「找不到 scholartools.exe」不会再出现；
- 运行时才把内嵌本体释放到系统临时目录（`%TEMP%\ScholarTray-<实例键>\`），以管理员令牌执行；
- 退出时删除该临时目录；若工具进程仍在运行导致删除失败，系统也会清理 `%TEMP%`，不留持久残留；
- 想改回外置（用自己另行放置的版本）：ini 写 `UseEmbeddedTool=0`，或用 `ToolPath=` 指定绝对路径（优先级最高）。

> 代价：exe 体积从约 60 KB 变为约 6.6 MB —— 这是把工具本体打进来的必然结果。

构建方式：把原始 `scholartools.exe` 放进仓库 `assets\` 目录，再运行 `build.cmd`。

---

## 主要功能

### 1. 托盘图标，一眼看懂状态

- 🟢 **彩色图标** = 学术工具已开启
- ⚫ **灰色图标** = 学术工具已关闭
- 鼠标悬停显示 `学术工具：已开启` / `已关闭`

### 2. 左键单击 = 一键切换

点一下就能在「开启 ↔ 关闭」之间切换，切换过程中图标会进入「处理中」，防止你手快点两下导致冲突。

### 3. 右键菜单

```
状态：已开启
网络：已连接校园网：NCWU 2
─────────────────
关闭学术工具（临时）
重新检测状态
─────────────────
关闭并退出
```

点「关闭并退出」会**先把学术工具关掉，再退出自己**，不会留下后台残留。

### 4. 校园网检测（可选，默认开启）

学术通道**只能在校园网里用**。所以每次点「开启」前，程序会自动检查你当前连的 WiFi：

| 情况 | 结果 |
|---|---|
| 连着校园网（如 `NCWU`、`NCWU 2`） | ✅ 正常开启 |
| 连着别的 WiFi（如家里的热点） | ❌ 弹窗提示「请连接校园网后再试」 |
| 没连 WiFi | ❌ 同样提示 |

**只有「开启」会检查，关闭永远不检查** —— 不然你断网之后就关不掉了。

> 如果你学校的 WiFi 名字不是 `NCWU`，改配置文件里的 `WifiAllowedSsid` 即可，不用重新编译。

---

## 快速开始（普通用户）

### 第 1 步：准备

1. 确认你已经拿到 `scholartools.exe`（学校提供的学术工具）
2. 确认系统是 **Windows 10 / 11 64 位**

### 第 2 步：放置文件

把 `ScholarTray.exe` 放到 **`scholartools.exe` 同一个文件夹里**（推荐），比如：

```
D:\UserData\Downloads\学术工具\
├── scholartools.exe      ← 学校给的
├── ScholarTray.exe       ← 放这里
└── 学术工具使用说明.docx
```

> 放别处也能用，程序会自动在几个常见位置找 `scholartools.exe`。找不到的话，用配置文件里的 `ToolPath` 手动指定。

### 第 3 步：运行

**双击 `ScholarTray.exe`** → 弹出 UAC 提示点「是」→ 托盘出现图标，就完成了。

> 为什么每次启动都要点 UAC？因为学术工具本身必须管理员权限（它要改证书和 hosts）。程序自己声明了 `requireAdministrator`，**只需在启动时授权一次**，之后每次点托盘切换都不再弹窗。

---

## 从源码编译（开发者）

### 环境要求

- Windows 10 / 11
- .NET Framework 4.x（Win10/11 系统自带，**不用额外装任何东西**）

### 编译

```bat
build.cmd
```

双击运行即可，会生成两个文件：

| 文件 | 用途 |
|---|---|
| `ScholarTray.exe` | 主程序（管理员权限） |
| `ScholarTray.SelfTest.exe` | 只读自检程序，**普通权限就能跑**，用来检查环境和网络状态 |

编译用的是系统自带的 `csc.exe`（`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`），**零第三方依赖、零 NuGet 包**。

### 自检

```bat
ScholarTray.SelfTest.exe
```

会输出类似：

```
BaseDirectory : D:\...\学术工具
ToolPath      : D:\...\学术工具\scholartools.exe
RootCert      : True          ← 证书是否已装
HostsBlock    : True          ← hosts 是否已改
Detect        : 已开启
WifiGate      : Connected
WifiSsid      : [NCWU 2]
WifiAllowed   : True
```

自检**不修改任何东西**，可以放心跑。

---

## 配置文件（可选）

程序**不配置也能用**。需要自定义时，把 `ScholarTray.ini.example` 复制为 `ScholarTray.ini`，放在 `ScholarTray.exe` 同目录。

```ini
# scholartools.exe 的完整路径（默认自动搜索）
ToolPath=D:\UserData\Downloads\学术工具\scholartools.exe

# 怎么驱动原工具：
#   menu = 模拟输入 1/2（默认，和教程一样）
#   cli  = 直接用 install / uninstall 参数
InvokeMode=menu

# 你的校园网 WiFi 名字，多个用逗号分隔，不区分大小写
WifiAllowedSsid=NCWU,ncwu

# 不在校园网时是否仍然允许开启（默认 0 = 不允许）
WifiBypassWhenNotCampus=0

# 是否写日志文件（默认 0 = 不写，保持绿色）
LogEnabled=0

# 单次操作超时秒数（默认 180，网络慢别设太小）
TimeoutSeconds=180
```

---

## 绿色软件（不污染系统）

这点很重要，所以单独说：

- ✅ **不写注册表**
- ✅ **不装系统服务**
- ✅ **不建计划任务**
- ✅ **不加开机启动项**
- ✅ **不改 PATH 环境变量**
- ✅ **不往 AppData 写任何东西**
- ✅ **默认不写日志文件**
- ✅ **不改动、不内嵌 `scholartools.exe`**（只是调用它）

**卸载方法**：直接删掉 `ScholarTray.exe` 就完事了。如果生成过 `ScholarTray.ini` / `ScholarTray.log` / `ScholarTray.crash.log`，一并删掉即可。

> 注意：学术工具自己对系统做的改动（证书、hosts）是 `scholartools.exe` 干的，不归本程序管。用「关闭并退出」会让它执行一次卸载。

---

## 项目结构

```
ScholarTray/
├── src/
│   ├── Program.cs              程序入口：单实例锁、全局异常捕获
│   ├── SelfTest.cs             只读自检入口
│   ├── Core/                   ← 核心逻辑（不依赖界面）
│   │   ├── AppPaths.cs         所有路径的唯一来源
│   │   ├── AppConfig.cs        读取 INI 配置
│   │   ├── Logger.cs           日志
│   │   ├── StateProbe.cs       检测学术工具当前是开还是关
│   │   ├── ToolRunner.cs       调用 scholartools.exe
│   │   └── WifiProbe.cs        检测是不是校园网
│   └── UI/                     ← 界面（依赖 Core）
│       ├── TrayApp.cs          托盘主逻辑
│       ├── TrayMenu.cs         右键菜单
│       └── TrayResources.cs    图标加载
├── ScholarTray.manifest        声明需要管理员权限
├── build.cmd                   一键编译
└── ScholarTray.ini.example     配置样例
```

**设计原则**：`UI` 层可以调用 `Core` 层，但 `Core` 层**绝不**反过来依赖 `UI`。所以「检测状态」「检测网络」这些逻辑可以单独用 `SelfTest.exe` 测试，不用起界面。

---

## 常见问题

**Q：双击没反应？**
A：检查任务栏右下角（可能藏在 `^` 箭头里）。另外确认你点的是 `ScholarTray.exe` 不是 `ScholarTray.SelfTest.exe`。

**Q：提示「找不到 scholartools.exe」？**
A：把 `ScholarTray.exe` 和 `scholartools.exe` 放同一个文件夹，或者在 `ScholarTray.ini` 里写 `ToolPath=` 完整路径。

**Q：点了开启没反应 / 报错？**
A：先把 `LogEnabled=1` 写进配置，再点一次，然后看同目录的 `ScholarTray.log`。多半是权限或网络问题。

**Q：切换不生效？**
A：在配置里改成 `InvokeMode=cli`（改用命令行参数而不是模拟按键）再试。

**Q：能不能用 Edge？**
A：可以。本程序只是调用学术工具，和浏览器无关。学术工具装好后，Edge 直接就能访问加速站点——不需要装那三个专用浏览器。

**Q：会拖慢开机吗？**
A：不会。它不设开机自启（除非你自己加），运行时常驻内存极小（约几 MB）。

**Q：安全吗？**
A：本程序所有代码都在这个仓库里，可以自己看、自己编译。它**不联网、不上传任何数据**（只读系统状态 + 调用本地 `scholartools.exe`）。但它调用的学术工具会安装根证书和修改 hosts —— 那是学术工具的行为，请自行评估。

---

## 技术细节（给好奇的人）

### 为什么用 C# 5 语法

系统自带的 `csc.exe` 版本只支持到 C# 5，**不支持**字符串插值 `$""`、`nameof`、`?.` 等新语法。为了保证「拿到任何一台 Windows 都能直接编译」，本项目刻意只用 C# 5 写法。

### 为什么源码要加 `/codepage:65001`

源码是 UTF-8 编码且包含中文。不加这个参数，`csc` 会按系统 ANSI 代码页解码，中文会变成乱码。

### 怎么判断「已开启 / 已关闭」

不靠程序自己记状态（那样会和你手动操作对不上），而是**直接读系统真实状态**：

1. 用 `X509Store` 查根证书里有没有 `Scholar Root CA`
2. 查 `hosts` 文件里有没有 `# START OF SCHOLAR TOOLS ENTRIES` 标记
3. **两个都有**才算「已开启」

这样即使你用原始方式手动开关过，托盘图标也能显示正确。

### 怎么判断操作成功

**不看退出码**。因为实测发现：学术工具在权限不足时也会返回退出码 0（但实际没干活）。所以程序的做法是——**执行完再读一次系统状态**，状态真的变了才算成功。

### 校园网检测用了什么 API

试过三条路，最终选 WMI 做主路径：

| 方式 | 结果 |
|---|---|
| `netsh wlan show interfaces` | ❌ 实测报 `error 5`，要求开「位置服务」 |
| WLAN API `WlanQueryInterface` | ⚠️ 要管理员权限，普通权限被拒 |
| **WMI `MSFT_NetConnectionProfile`** | ✅ 普通权限就能读到真实 SSID |

---

## 免责声明

本项目是**第三方封装工具**，与 `scholartools.exe` 的开发者/运营方**没有任何关系**。

- 本项目不修改、不内嵌、不分发 `scholartools.exe`
- 使用本工具产生的任何后果由使用者自行承担
- 请遵守你所在学校的网络使用规定
- 学术工具会安装根证书并修改 hosts，请确认来源可信后再使用

---

## 许可证

[MIT](LICENSE) —— 随便用，出事别找我。