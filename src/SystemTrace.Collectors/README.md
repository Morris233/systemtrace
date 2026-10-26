# Collectors / 采集层

PHASE 03: four independent read-only collectors behind Core's ISystemCollector. Windows registry, startup-folder metadata, Configuration Manager device properties and NetworkInterface are isolated behind fakeable sources. Collectors never execute startup commands, write the registry, resolve shortcuts, install software or change adapters.

四类独立只读采集器均通过可替换数据源测试。注册表使用显式视图和只读句柄；驱动仅枚举当前 PnP 设备；网络仅保留允许字段。字段读取错误与真实空值区分，任何不完整结果均不推进基线。Windows 设备枚举出现异常空列表也按失败处理。
