using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FinanceMappingTool
{
    public sealed class MainForm : Form
    {
        private readonly TextBox path = new TextBox { Width = 570, ReadOnly = true };
        private readonly TextBox sheet = new TextBox { Text = "科目对照表", Width = 130 };
        private readonly TextBox database = new TextBox { Text = "FinanceDB", Width = 120 };
        private readonly NumericUpDown header = new NumericUpDown { Minimum = 1, Maximum = 10000, Value = 1, Width = 55 };
        private readonly TextBox type = new TextBox { Text = "KDVoucher", MaxLength = 50, Width = 135 };
        private readonly Button load = new Button { Text = "选择并读取 Excel", AutoSize = true };
        private readonly Button export = new Button { Text = "导出 SQL", AutoSize = true, Enabled = false };
        private readonly Button exportExcel = new Button { Text = "导出 Excel", AutoSize = true, Enabled = false };
        private readonly Button saveLog = new Button { Text = "保存检查日志", AutoSize = true, Enabled = false };
        private readonly Label status = new Label { AutoSize = true, Text = "请选择 .xlsx 文件。" };
        private readonly TextBox log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, WordWrap = false };
        private readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, BackgroundColor = Color.White,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
        private List<MappingRow> source;
        private BindingList<MappingRow> preview;
        private string previewType;
        private string ruleFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mapping-rules.xml");

        public MainForm()
        {
            Text = "财务科目对照导入工具 v1.3.1";
            Font = new Font("Microsoft YaHei UI", 9F);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1180, 780);
            MinimumSize = new Size(900, 620);
            AutoScaleMode = AutoScaleMode.Dpi;
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(10) };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 165));
            var files = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
            files.Controls.Add(load); files.Controls.Add(path);
            var options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
            AddLabel(options, "工作表"); options.Controls.Add(sheet);
            AddLabel(options, "表头行"); options.Controls.Add(header);
            AddLabel(options, "映射类型"); options.Controls.Add(type);
            AddLabel(options, "目标数据库"); options.Controls.Add(database);
            options.Controls.Add(export); options.Controls.Add(exportExcel); options.Controls.Add(saveLog);
            var previewPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            previewPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            previewPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            previewPanel.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(1080, 0), Text =
                "可双击修改映射和属性；切换映射类型或重新读取会重置手工修改。CustomerType：资产=1，其余=2；不执行原脚本 UPDATE。\r\n02账套：默认国内编码/名称直接对应；具体业务例外需自行配置，本公开包不含原始业务数据。" }, 0, 0);
            previewPanel.Controls.Add(grid, 0, 1);
            panel.Controls.Add(files, 0, 0); panel.Controls.Add(options, 0, 1); panel.Controls.Add(previewPanel, 0, 2);
            panel.Controls.Add(status, 0, 3); panel.Controls.Add(log, 0, 4);
            Controls.Add(panel);
            AddColumn("ExcelRow", "Excel行", true); AddColumn("FeeNo", "国内科目编码"); AddColumn("FeeName", "国内科目名称");
            AddColumn("FeeNoOverseas", "海外科目编码"); AddColumn("FeeNameOverseas", "海外科目名称");
            AddColumn("OverseasType", "映射类型", true); AddColumn("CustomerType", "客户/供应商");
            AddColumn("CreditDebitType", "收付属性"); AddColumn("Memo", "备注");
            grid.DataBindingComplete += (s, e) => ResizeFirstColumn();
            load.Click += async (s, e) => await LoadExcel();
            type.TextChanged += (s, e) =>
            {
                if (source == null) return;
                export.Enabled = exportExcel.Enabled = !string.IsNullOrWhiteSpace(type.Text);
                status.Text = export.Enabled ? "映射类型已修改，离开输入框后刷新预览。" : "请输入映射类型。";
            };
            type.Leave += (s, e) =>
            {
                if (source != null && !string.IsNullOrWhiteSpace(type.Text) && previewType != type.Text.Trim())
                    TryAction(RefreshPreview);
            };
            export.Click += (s, e) => TryAction(ExportSql);
            exportExcel.Click += async (s, e) => await ExportExcelAsync();
            saveLog.Click += (s, e) => TryAction(SaveLog);
            grid.DataError += (s, e) => { MessageBox.Show(this, "输入格式错误，请检查当前单元格。", "输入检查"); e.ThrowException = false; };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = Math.Max((int)Math.Ceiling(grid.ColumnHeadersHeight * 1.5), grid.Font.Height + 12);
            ResizeFirstColumn();
        }

        private void ResizeFirstColumn()
        {
            if (grid.Columns.Count == 0) return;
            var first = grid.Columns[0];
            int preferred = first.GetPreferredWidth(DataGridViewAutoSizeColumnMode.DisplayedCells, true);
            // Fix only this column so the grid's automatic sizing cannot shrink it back.
            first.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            first.Width = (int)Math.Ceiling(preferred * 1.5);
        }

        private static void AddLabel(FlowLayoutPanel p, string text)
        { p.Controls.Add(new Label { Text = text, AutoSize = true, Margin = new Padding(6, 7, 2, 0) }); }
        private void AddColumn(string property, string title, bool readOnly = false)
        { grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = title, ReadOnly = readOnly, SortMode = DataGridViewColumnSortMode.NotSortable }); }
        private void TryAction(Action action)
        { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "操作未完成", MessageBoxButtons.OK, MessageBoxIcon.Error); } }

        private async Task LoadExcel()
        {
            using (var dialog = new OpenFileDialog { Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string file = dialog.FileName, sheetName = sheet.Text.Trim(); int headerRow = (int)header.Value;
                // Clear previous results so a failed import cannot export stale data.
                source = null; preview = null; previewType = null; grid.DataSource = null; export.Enabled = exportExcel.Enabled = false;
                path.Text = file; load.Enabled = false; type.Enabled = false; saveLog.Enabled = false;
                status.Text = "正在读取和校验…"; log.Clear();
                try
                {
                    ImportResult result;
                    string[] selectedSources;
                    using (var session = await Task.Run(() => MappingEngine.Open(file, sheetName, headerRow)))
                    {
                        var selected = session.AutomaticMapping;
                        if (selected.Count != ExcelSchema.Fields.Count)
                        {
                            using (var mappingDialog = new ColumnMappingForm(session.Columns, selected))
                            {
                                if (mappingDialog.ShowDialog(this) != DialogResult.OK)
                                {
                                    status.Text = "已取消导入，未生成映射。";
                                    return;
                                }
                                selected = mappingDialog.Mapping;
                            }
                        }
                        selectedSources = ExcelSchema.Fields.Select(f => f.Header + " ← " +
                            session.Columns.First(c => c.Number == selected[f.Field]).Label).ToArray();
                        result = await Task.Run(() => session.Read(selected, ruleFile));
                    }
                    source = result.Rows;
                    log.Lines = new[] { "来源：" + file, "工作表：" + sheetName + "；表头行：" + headerRow,
                        "基础映射 " + source.Count + " 条；缺一侧编码跳过 " + result.Skipped + " 行；重复国内编码忽略 " + result.Duplicates + " 行。",
                        "保留 Excel 原值；人工修订仅在 mapping-rules.xml 对应规则 enabled=true 时生效。",
                        "注意：本版不自动新增 Excel 中不存在的税务科目，也不复现原脚本后期 UPDATE。" }
                        .Concat(selectedSources).Concat(result.Messages).ToArray();
                    RefreshPreview(); saveLog.Enabled = true;
                }
                catch (Exception ex)
                {
                    source = null; preview = null; previewType = null; grid.DataSource = null; export.Enabled = exportExcel.Enabled = false;
                    status.Text = "读取失败。"; log.Text = ex.ToString(); saveLog.Enabled = true;
                    MessageBox.Show(this, ex.Message, "读取失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { load.Enabled = true; type.Enabled = true; }
            }
        }
        private void RefreshPreview()
        {
            export.Enabled = exportExcel.Enabled = false;
            var mappingType = type.Text.Trim();
            var rows = MappingEngine.ForType(source, mappingType, ruleFile);
            preview = new BindingList<MappingRow>(rows); grid.DataSource = preview;
            previewType = mappingType;
            status.Text = "当前预览：" + previewType + "，" + rows.Count + " 条。请检查映射和下方日志后导出。";
            export.Enabled = exportExcel.Enabled = true;
        }
        private void ExportSql()
        {
            CommitPreview();
            var rows = preview.ToList();
            string sql = MappingEngine.Sql(rows, database.Text.Trim());
            using (var dialog = new SaveFileDialog { Filter = "SQL 脚本 (*.sql)|*.sql", DefaultExt = "sql", AddExtension = true,
                FileName = "VoucherFeeRelation_" + string.Concat(previewType.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".sql" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(dialog.FileName, sql, new UTF8Encoding(true));
                status.Text = "已导出 " + rows.Count + " 条映射：" + dialog.FileName;
                MessageBox.Show(this, "SQL 已保存。请在 SSMS 中确认目标数据库后执行。\r\n本次SQL仅保存到本地，未执行数据库写入。", "导出完成");
            }
        }
        private void CommitPreview()
        {
            if (preview == null || previewType != type.Text.Trim()) RefreshPreview();
            if (!grid.EndEdit()) throw new InvalidOperationException("请先完成当前单元格的编辑。");
            if (grid.DataSource != null) BindingContext[grid.DataSource].EndCurrentEdit();
        }

        private PreviewTable CapturePreview()
        {
            CommitPreview();
            var columns = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToList();
            var records = grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r =>
                new PreviewRecord((MappingRow)r.DataBoundItem, columns.Select(c => r.Cells[c.Index].Value).ToArray())).ToList();
            return new PreviewTable(columns.Select(c => c.HeaderText).ToArray(), records);
        }

        private void SetExcelExportBusy(bool busy)
        {
            load.Enabled = !busy; grid.Enabled = !busy; type.ReadOnly = busy;
            sheet.ReadOnly = busy; header.Enabled = !busy; database.ReadOnly = busy;
            export.Enabled = exportExcel.Enabled = !busy && preview != null && !string.IsNullOrWhiteSpace(type.Text);
            UseWaitCursor = busy;
        }

        private async Task ExportExcelAsync()
        {
            bool busy = false;
            try
            {
                var table = CapturePreview();
                string file;
                using (var dialog = new SaveFileDialog { Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", DefaultExt = "xlsx", AddExtension = true,
                    FileName = "科目数据_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx" })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    file = dialog.FileName;
                }
                string referenceFile = CustomerReferenceReader.DefaultFilePath;
                if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(referenceFile), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("导出文件不能覆盖参照文件“当前主数据.xlsx”，请选择其他文件名。");
                SetExcelExportBusy(true); busy = true;
                status.Text = "正在读取程序目录下的当前主数据.xlsx（Main）…";
                var reference = await Task.Run(() => CustomerReferenceReader.Read(referenceFile));
                var anomalies = ExcelReport.Analyze(table, reference);
                status.Text = "正在生成一个含两个工作表的Excel文件…";
                await Task.Run(() => ExcelReport.Save(file, table, anomalies));
                log.AppendText(Environment.NewLine + "Excel异常检查参照：" + referenceFile + " / Main" +
                    "；参照有效记录 " + reference.Count + " 条；读取时间 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                    "；异常记录 " + anomalies.Count + " 条。");
                if (reference.Count == 0) log.AppendText(Environment.NewLine + "参照数据为空：没有同国内科目可进行客户/供应商类型对比。");
                saveLog.Enabled = true;
                status.Text = "已导出一个Excel文件：科目数据 " + table.Records.Count + " 行，异常 " + anomalies.Count + " 条。";
                MessageBox.Show(this, "已保存一个Excel文件，包含“科目数据”和“异常科目”两个工作表。\r\n异常数据行号对应“科目数据”中的实际行号（含表头）；同一数据命中两项检查时分别列出。\r\n" + file, "导出完成");
            }
            catch (Exception ex)
            {
                status.Text = "Excel导出未完成。";
                MessageBox.Show(this, "Excel导出未完成：" + ex.Message + "\r\n请检查程序EXE所在目录的“当前主数据.xlsx”、Main工作表以及FeeNo、CustomerType列；不会跳过类型检查后生成不完整报告。", "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (busy) SetExcelExportBusy(false);
            }
        }

        private void SaveLog()
        {
            using (var dialog = new SaveFileDialog { Filter = "文本日志 (*.txt)|*.txt", FileName = "映射检查日志.txt" })
                if (dialog.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dialog.FileName, log.Text, new UTF8Encoding(true));
        }
    }
}
