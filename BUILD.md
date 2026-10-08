# 编译

Windows 上安装 .NET SDK 和 .NET Framework 4.x 后，在仓库根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File ./build.ps1
```

输出：`bin/MouseTester-DPI.exe`，无需第三方 DLL 或配置文件。
