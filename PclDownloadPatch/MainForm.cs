using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using PclDownloadPatch.Ui;

namespace PclDownloadPatch
{
    internal sealed class MainForm : Form
    {
        private static readonly Color Accent = Color.FromArgb(37, 99, 235);
        private static readonly Color TitleText = Color.FromArgb(30, 49, 74);
        private static readonly Color MutedText = Color.FromArgb(138, 151, 168);
        private static readonly Color SuccessGreen = Color.FromArgb(29, 143, 91);
        private static readonly Color ErrorRed = Color.FromArgb(195, 57, 57);
        private static readonly Color InfoBlue = Color.FromArgb(43, 123, 205);

        private readonly string toolDirectory;
        private readonly DropCard card;
        private readonly StatusIcon icon;
        private readonly Label titleLabel;
        private readonly Label subtitleLabel;
        private readonly Label versionLabel;
        private readonly LinkLabel footer;
        private bool processing;
        private bool dragHighlight;

        public MainForm(string toolDirectory)
        {
            this.toolDirectory = toolDirectory;
            Text = "PCL 下载功能补丁";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            MinimumSize = new Size(560, 460);
            Size = new Size(680, 520);
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(244, 247, 251);
            AllowDrop = true;
            DragEnter += DragEnterFile;
            DragDrop += OnDrop;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(28, 22, 28, 12),
                ColumnCount = 1,
                RowCount = 3,
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 标题区随字体自适应，避免中文行高被裁切
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            Controls.Add(root);

            // 顶部标题区：两行 AutoSize 行，高度随字体/DPI 自适应
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                BackColor = BackColor,
                AutoSize = true
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(header, 0, 0);

            var headerTitle = new Label
            {
                Text = "解除 PCL 下载限制",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold),
                ForeColor = TitleText,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            header.Controls.Add(headerTitle, 0, 0);

            var headerSubtitle = new Label
            {
                Text = "拖入或点击选择官方 PCL.exe，自动识别版本并处理",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = MutedText,
                Margin = new Padding(0, 3, 0, 0),
                BackColor = Color.Transparent
            };
            header.Controls.Add(headerSubtitle, 0, 1);

            // 中央拖放/选择卡片
            card = new DropCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 14, 0, 14),
                Radius = 18F,
                Cursor = Cursors.Hand
            };
            // 先挂布局事件再加入布局树：TLP 在 Add 时就会同步定位卡片，
            // 之后再挂接会错过首次 Resize，导致内容停留在默认位置。
            card.Resize += delegate { if (titleLabel != null) LayoutCardContent(); };
            root.Controls.Add(card, 0, 1);

            icon = new StatusIcon { Size = new Size(76, 76) };
            card.Controls.Add(icon);

            titleLabel = new Label
            {
                Text = "拖入 Plain Craft Launcher 2.exe",
                Font = new Font("Segoe UI Semibold", 15.5F, FontStyle.Bold),
                ForeColor = TitleText,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };
            card.Controls.Add(titleLabel);

            subtitleLabel = new Label
            {
                Text = "或点击此处选择文件",
                Font = new Font("Segoe UI", 10F),
                ForeColor = MutedText,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };
            card.Controls.Add(subtitleLabel);

            versionLabel = new Label
            {
                Text = string.Empty,
                Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(69, 88, 114),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };
            card.Controls.Add(versionLabel);
            // 版本行文本变化时（识别出版本号 / 清空）重排内容，保证可见内容始终居中
            versionLabel.TextChanged += delegate { LayoutCardContent(); };

            // 底部信息（tooplick 为可点击的 GitHub 链接）
            footer = new LinkLabel
            {
                Text = "作者: tooplick",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(150, 160, 174),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = BackColor,
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = Accent,
                ActiveLinkColor = Accent,
                VisitedLinkColor = Accent
            };
            int linkStart = footer.Text.IndexOf("tooplick", StringComparison.Ordinal);
            if (linkStart >= 0) footer.LinkArea = new LinkArea(linkStart, "tooplick".Length);
            footer.LinkClicked += delegate
            {
                try { System.Diagnostics.Process.Start("https://github.com/tooplick"); } catch { }
            };
            root.Controls.Add(footer, 0, 2);

            // 卡片点击 = 打开文件选择框
            card.Click += OnCardClick;
            icon.Click += OnCardClick;
            titleLabel.Click += OnCardClick;
            subtitleLabel.Click += OnCardClick;
            versionLabel.Click += OnCardClick;

            // 拖放目标：窗体 + 卡片 + 卡片内全部子控件
            RegisterDropTarget(card);
            RegisterDropTarget(icon);
            RegisterDropTarget(titleLabel);
            RegisterDropTarget(subtitleLabel);
            RegisterDropTarget(versionLabel);
            card.DragEnter += delegate { SetDragHighlight(true); };
            card.DragLeave += delegate { SetDragHighlight(false); };

            LayoutCardContent();
            ShowIdle();
        }

        // 句柄创建后 WinForms 会按 DPI 缩放一次子控件；此后用真实 DPI 因子重排，
        // 保证与缩放后的坐标一致（跨显示器移动时同样生效）。
        private float uiScale = 1F;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            uiScale = DeviceDpi / 96F;
            LayoutCardContent();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            uiScale = DeviceDpi / 96F;
            LayoutCardContent();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LayoutCardContent();
        }

        private void LayoutCardContent()
        {
            if (card == null || card.Width < 1 || card.Height < 1) return;
            float s = uiScale;

            int titleH = (int)(38F * s);   // 预留中文回退字体（雅黑）行高，避免裁切
            int subH = (int)(26F * s);
            int verH = (int)(26F * s);
            int gap2 = (int)(2F * s);
            int gap3 = (int)(4F * s);
            // 版本行只在有文本时计入内容高度，避免空行把可见内容顶得偏上
            bool hasVersion = versionLabel.Text.Length > 0;
            int verBlock = hasVersion ? gap3 + verH : 0;
            int w = Math.Max(60, card.Width - (int)(56F * s));
            int x = (card.Width - w) / 2;

            if (icon.Visible)
            {
                int iconSize = (int)(76F * s);
                int gap1 = (int)(12F * s);
                int contentH = iconSize + gap1 + titleH + gap2 + subH + verBlock;
                int top = Math.Max((int)(16F * s), (card.Height - contentH) / 2);
                icon.SetBounds((card.Width - iconSize) / 2, top, iconSize, iconSize);
                titleLabel.SetBounds(x, icon.Bottom + gap1, w, titleH);
            }
            else
            {
                int contentH = titleH + gap2 + subH + verBlock;
                int top = Math.Max((int)(16F * s), (card.Height - contentH) / 2);
                titleLabel.SetBounds(x, top, w, titleH);
            }
            subtitleLabel.SetBounds(x, titleLabel.Bottom + gap2, w, subH);
            versionLabel.SetBounds(x, subtitleLabel.Bottom + gap3, w, verH);
        }

        private void RegisterDropTarget(Control control)
        {
            control.AllowDrop = true;
            control.DragEnter += DragEnterFile;
            control.DragDrop += OnDrop;
        }

        // ---------- 状态外观 ----------

        private void SetCardStyle(Color fill, Color border, float borderWidth)
        {
            card.FillColor = fill;
            card.BorderColor = border;
            card.BorderWidth = borderWidth;
        }

        private void SetIcon(StatusIcon.IconKind kind, Color glyph, Color circle)
        {
            icon.Kind = kind;
            icon.GlyphColor = glyph;
            icon.CircleColor = circle;
        }

        private void ShowIdle()
        {
            dragHighlight = false;
            SetCardStyle(Color.White, Color.FromArgb(203, 213, 225), 1.6F);
            icon.Visible = false;   // 空闲态不显示图标
            titleLabel.Text = "拖入 Plain Craft Launcher 2.exe";
            titleLabel.ForeColor = TitleText;
            subtitleLabel.Text = "或点击此处选择文件";
            subtitleLabel.ForeColor = MutedText;
            versionLabel.Text = string.Empty;
            card.Cursor = Cursors.Hand;
            LayoutCardContent();
        }

        private void SetDragHighlight(bool active)
        {
            if (processing) return;
            if (active == dragHighlight) return;
            dragHighlight = active;
            if (active)
            {
                SetCardStyle(Color.FromArgb(239, 246, 255), Accent, 2.4F);
                icon.Visible = false;
                titleLabel.Text = "松开鼠标开始处理";
                subtitleLabel.Text = "支持 PCL 2.99.0.0 及以上版本";
                LayoutCardContent();
            }
            else
            {
                ShowIdle();
            }
        }

        private void ShowProcessing(string fileName)
        {
            SetCardStyle(Color.White, Color.FromArgb(203, 213, 225), 1.6F);
            SetIcon(StatusIcon.IconKind.Spinner, Accent, Color.FromArgb(239, 246, 255));
            icon.Visible = true;
            titleLabel.Text = "正在处理…";
            titleLabel.ForeColor = TitleText;
            subtitleLabel.Text = fileName;
            subtitleLabel.ForeColor = MutedText;
            card.Cursor = Cursors.Default;
            LayoutCardContent();
        }

        private void ShowResult(bool patched, bool noRestriction, string detail)
        {
            if (patched)
            {
                SetCardStyle(Color.FromArgb(241, 249, 245), Color.FromArgb(167, 220, 192), 1.6F);
                SetIcon(StatusIcon.IconKind.Check, SuccessGreen, Color.FromArgb(227, 244, 235));
                titleLabel.Text = "补丁成功";
                titleLabel.ForeColor = SuccessGreen;
                subtitleLabel.Text = "下载限制已解除，原文件已直接替换";
                subtitleLabel.ForeColor = Color.FromArgb(90, 138, 115);
            }
            else if (noRestriction)
            {
                SetCardStyle(Color.FromArgb(245, 249, 253), Color.FromArgb(185, 212, 236), 1.6F);
                SetIcon(StatusIcon.IconKind.Info, InfoBlue, Color.FromArgb(232, 241, 250));
                titleLabel.Text = "该版本无下载限制";
                titleLabel.ForeColor = InfoBlue;
                subtitleLabel.Text = "无需处理，原文件未做任何修改";
                subtitleLabel.ForeColor = Color.FromArgb(96, 125, 152);
            }
            else
            {
                SetCardStyle(Color.FromArgb(252, 243, 243), Color.FromArgb(229, 180, 180), 1.6F);
                SetIcon(StatusIcon.IconKind.Cross, ErrorRed, Color.FromArgb(251, 234, 234));
                titleLabel.Text = "处理失败";
                titleLabel.ForeColor = ErrorRed;
                subtitleLabel.Text = detail;
                subtitleLabel.ForeColor = Color.FromArgb(176, 74, 74);
            }
            icon.Visible = true;
            LayoutCardContent();
            card.Cursor = Cursors.Hand;
        }

        private void ShowAlreadyPatched()
        {
            SetCardStyle(Color.FromArgb(245, 249, 253), Color.FromArgb(185, 212, 236), 1.6F);
            SetIcon(StatusIcon.IconKind.Info, InfoBlue, Color.FromArgb(232, 241, 250));
            titleLabel.Text = "该文件已应用补丁";
            titleLabel.ForeColor = InfoBlue;
            subtitleLabel.Text = "无需重复处理，原文件未做任何修改";
            subtitleLabel.ForeColor = Color.FromArgb(96, 125, 152);
            icon.Visible = true;
            LayoutCardContent();
            card.Cursor = Cursors.Hand;
        }

        // ---------- 交互 ----------

        private void DragEnterFile(object sender, DragEventArgs e)
        {
            if (!processing && e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                e.Effect = files != null && files.Length == 1 &&
                    string.Equals(Path.GetExtension(files[0]), ".exe", StringComparison.OrdinalIgnoreCase)
                    ? DragDropEffects.Copy : DragDropEffects.None;
            }
            else e.Effect = DragDropEffects.None;
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            SetDragHighlight(false);
            if (processing || e.Data == null) return;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length != 1 || !File.Exists(files[0])) return;
            if (!string.Equals(Path.GetExtension(files[0]), ".exe", StringComparison.OrdinalIgnoreCase)) return;
            await HandleExecutableAsync(files[0]);
        }

        private async void OnCardClick(object sender, EventArgs e)
        {
            if (processing) return;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择 Plain Craft Launcher 可执行文件";
                dialog.Filter = "可执行文件 (*.exe)|*.exe";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    await HandleExecutableAsync(dialog.FileName);
                }
            }
        }

        // ---------- 处理流程（逻辑与旧版一致） ----------

        private async Task HandleExecutableAsync(string source)
        {
            processing = true;
            versionLabel.Text = string.Empty;
            ShowProcessing(Path.GetFileName(source));

            string temp = null;
            try
            {
                Version fileVersion = await Task.Run(() => ReadPclVersion(source));
                versionLabel.Text = "PCL " + fileVersion + "  ·  " + Path.GetFileName(source);

                if (fileVersion < new Version(2, 99, 0, 0))
                {
                    ShowResult(false, true, null);
                    return;
                }

                // 已应用过补丁的文件直接提示，不进入补丁流程（补丁引擎本身也会
                // 因模式匹配失败而拒绝，这里只是给出更友好的结果）。
                bool alreadyPatched = await Task.Run(() => McDownloadPatch.IsAlreadyPatched(source));
                if (alreadyPatched)
                {
                    ShowAlreadyPatched();
                    return;
                }

                temp = source + "." + Guid.NewGuid().ToString("N") + ".patching.tmp";
                await Task.Run(() =>
                {
                    McDownloadPatch.SetDependencyDirectory(toolDirectory);
                    McDownloadPatch.ApplyPatch(source, temp, null);
                });

                var patchedVersion = ReadPclVersion(temp);
                if (patchedVersion != fileVersion)
                    throw new InvalidDataException("补丁输出版本不一致，未替换原文件。");

                // 补丁写入同级临时文件后直接替换原文件；按需求不创建备份。
                File.Replace(temp, source, null, true);
                temp = null;
                ShowResult(true, false, null);
            }
            catch (Exception ex)
            {
                ShowResult(false, false, ex.Message);
            }
            finally
            {
                if (temp != null)
                {
                    try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                }
                processing = false;
            }
        }

        private static Version ReadPclVersion(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length < 1024 * 1024)
                throw new InvalidDataException("无效的 PCL exe 文件。");

            var details = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
            string description = details.FileDescription ?? string.Empty;
            if (description.IndexOf("Plain Craft Launcher", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidDataException("无法识别为 Plain Craft Launcher 文件。");

            Version parsed;
            if (!Version.TryParse(details.FileVersion, out parsed))
                throw new InvalidDataException("无法识别 PCL 版本号。");
            return parsed;
        }
    }
}
