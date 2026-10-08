# 项目维护约定

- 修改程序代码后，编译并验证，再更新 GitHub Release；确认附件列表中有最新版 MouseTester-DPI.exe，不以源码提交或草稿代替发布完成。
- .github/workflows/release.yml 在 main 的程序源码或编译脚本更新后自动编译并发布单文件 EXE。必须检查运行成功及 Release 下载附件。
- Windows 程序只提供 EXE，不生成程序 ZIP 包。
- 保留 MIT 许可、来源声明、中文使用说明和截图。
- 用户文档保持简洁，不加入开发 bug、修改过程、初始化细节或调试日志说明。
