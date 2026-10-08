# Mouse Tester — 鼠标回报率与 DPI 测试工具

Windows 鼠标 DPI 检测、鼠标回报率检测、无线游戏鼠标 DPI 检查工具，支持有线和无线鼠标。Mouse DPI analyzer and polling rate tester.

## 下载

[**下载单文件 EXE**](https://github.com/jetsun-builds/MouseTester-DPI-PollingRate/releases/latest/download/MouseTester-DPI.exe)

下载后直接运行 `MouseTester-DPI.exe`。需要 .NET Framework 4.6 或更新的 4.x，无需管理员权限。

## 使用方法

有多只鼠标时，先在设备列表选择待测鼠标；使用自动识别时，只移动要测试的那只。
设备名称优先读取鼠标自身信息，再由内置数据库补充；查不到时显示 VID/PID。无线设备可能显示接收器名称。

### 测回报率

连续移动鼠标 **3–5 秒**，查看下方绿色 **Hz** 结果。无需填写 DPI，也无需按 F5。

### 测 DPI

1. 在鼠标垫或桌面上用直尺标记起点和终点，例如相距 **10 厘米**，在程序中填写实际距离 **10cm**。
2. 将鼠标摆到起点，保持程序窗口在前台，按 **F5**。
3. 保持鼠标朝向固定，沿直线平稳移动到终点，不抬起、不旋转、不来回。
4. 停稳后按 **F6**，查看蓝色 **DPI** 结果。按 **Esc** 可取消。

**鼠标在桌面上实际移动的距离必须与程序填写的距离一致，否则 DPI 结果不准确。** 不需要按住鼠标左键；光标碰到屏幕边缘仍会计数。屏幕轨迹只用于观察速度和直线程度，不是桌面距离的尺子。

不知道当前 DPI 也能测。已知驱动 DPI 时，可勾选“对比标称 DPI”查看偏差。建议重复测量 3–5 次；切换鼠标或 DPI 档位后，点击“重新选择鼠标”清空旧平均值，再测试。

## 截图

### DPI 与回报率测试

![鼠标 DPI 与回报率测试界面](docs/images/mouse-dpi-polling-rate.png)

### 普通无线鼠标回报率

![普通无线鼠标回报率约 125Hz](docs/images/mouse-polling-rate-125hz.png)

普通办公无线鼠标常见回报率为 **125Hz**；游戏鼠标常见 **1000–8000Hz**，具体取决于型号、连接方式和设置。回报率为软件估算值，建议连续移动后观察稳定结果。

## 来源与许可

基于 microe1/MouseTester 的 Raw Input 采集代码开发，重新实现中文测量界面、DPI 与回报率统计、轨迹显示及多鼠标支持。

[原始项目](https://github.com/microe1/MouseTester) · [MIT 许可](LICENSE)

内置 USB 型号数据库来自 USB ID Repository。[第三方声明](THIRD-PARTY-NOTICES.txt)
