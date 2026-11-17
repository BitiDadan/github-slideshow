# 财务科目对照导入工具 v1.0

C# / WinForms / .NET Framework 4.8。读取 Excel 科目对照表，预览并生成 SQL Server 2014 兼容的 INSERT 脚本。离线运行，不连接数据库，不依赖安装 Office。

## 下载运行

打开本目录 `Downloads/FinanceMappingTool-v1.0.zip`，点击 GitHub 的 **Download raw file** 下载完整包。完整解压后进入 `Program`，双击 `FinanceMappingTool.exe`。EXE、DLL、配置文件必须放在同一目录。

需要 Windows 和 .NET Framework 4.8。无需安装 Python、.NET 8 或 VS 即可运行。

## VS 2022 编译

安装“.NET 桌面开发”工作负载及 .NET Framework 4.8 开发包；打开 `Source/FinanceMappingTool.sln`，还原 NuGet 后以 Release 编译。

## 使用步骤

1. 选择 `.xlsx`，默认工作表“科目对照表”，表头第1行。
2. 预览结果及日志，可以修改科目编码、名称和属性。
3. 选择映射类型，填写实际目标数据库名称。公开版本默认值 `FinanceDB` 只是占位，执行前必须改为实际库名。
4. 导出 SQL，先在测试数据库用 SSMS 打开并核对执行。

固定表头：A `*编码`、C `名称（越南文）`、D `科目编码`、E `科目名称`、F `科目类型`。读取 A/C/D/E，B不参与，G以后的拼接辅助列不使用。

D→FeeNo，E→FeeName，A→FeeNoOverseas，C→FeeNameOverseas。
两侧均空忽略；缺一侧编码跳过并记录；空名称保留并提示；同一国内编码默认取第一条并列出重复行。
CustomerType 初始规则为资产=1，其余=2；CreditDebitType 初始为NULL。可在预览修改。不执行任何后期维护 UPDATE。
KDVoucher 使用Excel对应关系；KDVoucher02 默认海外编码/名称直接使用国内编码/名称，备注为02账套。

## 业务配置

此公开交付包不包含用户上传的 Excel、原 SQL、实际科目映射、业务修订配置或基于原数据生成的脚本。`mapping-rules.xml` 为通用空配置。
需要修订时，在运行目录配置并重新读取 Excel：

```xml
<MappingRules>
  <Override enabled="true" sourceFeeNo="DEMO01" sourceOverseas="DEMO02" feeNo="DEMO03" />
  <Type02Override feeNo="DEMO03" overseas="DEMO04" />
</MappingRules>
```

示例为虚构编码。Override 在去重前应用；Type02Override 用于02类型已存在的国内编码，不会自动新增科目。

## 部署行为

- 按 OverseasType + FeeNo + IsDeleted=0 查找有效记录。
- 内容完全相同跳过；内容冲突或目标已有重复有效记录时报错，整个事务回滚。
- 不清空、不删除、不覆盖现有记录。已软删除记录不阻止新增有效记录。
- ID自增；CreateDate、IsDeleted依赖表的默认约束。
- Unicode字符串及单引号转义；文件编码UTF-8 BOM；兼容 SQL Server 2014。
- 表结构必须与工具列名、字段类型及默认值一致。当前版本不创建表结构。
- 编码建议Excel存为文本；已有前导零显示格式会保留，已丢失的前导零/精度无法恢复。
- 切换映射类型或重新读取会重置预览中的手工修改。

已完成Release编译及核心样例/SQL 2014解析检查。未在Windows实际启动界面，未连接SQL Server执行。首次运行需本地验收。

第三方依赖及许可证见 `Source/ThirdPartyNotices`，通过NuGet管理。程序包约2.7MB，不含.NET Framework运行时。
