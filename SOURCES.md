# 协议来源及致谢

本工具根据已公开的 GPD 控制器协议实现 Windows HID 访问和鼠标模式映射。

- **pelrun/pyWinControls** — GPL-3.0，公开了配置字段偏移、特殊鼠标按键码、读取及写入流程。
  - https://github.com/pelrun/pyWinControls
  - https://github.com/pelrun/pyWinControls/blob/main/wincontrols-hid-format.txt
- **OpenWinControls/libOpenWinControls** — Copyright (C) 2026 kylon，GPL-3.0-or-later，提供协议 v1 文档及实现参考。
  - https://github.com/OpenWinControls/libOpenWinControls
  - https://github.com/OpenWinControls/libOpenWinControls/blob/main/docs/protocolV1.md

上游库将 WIN 3 标注为待测试、默认未启用。本工具针对 G1618-03 / X221 K118 组合独立验证了配置读取、校验和、写入读回及实际肩键功能，并限制为该已验证组合。

此仓库及随附 C# 源码以 GPL-3.0-or-later 发布；许可证全文见 LICENSE。
