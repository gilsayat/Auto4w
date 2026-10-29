using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AUTO4W
{
    public class MainForm : Form
    {
        private AppSettings settings;
        private readonly TabControl tabs = new TabControl();
        private readonly TextBox logBox = new TextBox();
        private readonly TextBox[] folderBoxes = new TextBox[4];
        private readonly CheckBox fourWallets = new CheckBox();
        private readonly CheckBox errorFix = new CheckBox();
        private readonly CheckBox orderSeparation = new CheckBox();
        private readonly CheckBox startup = new CheckBox();
        private readonly CheckBox darkTheme = new CheckBox();
        private readonly CheckBox confirm = new CheckBox();
        private readonly TextBox oldExt = new TextBox();
        private readonly TextBox newExt = new TextBox();
        private readonly TextBox photoSize = new TextBox();
        private readonly TextBox photoLayout = new TextBox();
        private readonly DataGridView scheduleGrid = new DataGridView();

        private readonly string[] folderNames = { "Incoming", "Toprint", "Digital", "Errors" };

        public MainForm()
        {
            settings = AppSettings.Load();
            Text = "AUTO4W";
            MinimumSize = new Size(780, 560);
            Size = new Size(980, 680);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(245, 247, 251);
            BuildUi();
            LoadSettingsToUi();
            ApplyTheme();
            Log("AUTO4W started. Settings loaded.");
        }

        private Label LabelFor(string text, int x, int y, int width = 160)
        {
            return new Label { Text = text, Left = x, Top = y, Width = width, Height = 26, TextAlign = ContentAlignment.MiddleLeft };
        }

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = Color.FromArgb(31, 67, 112) };
            var brand = new Label { Text = "AUTO4W", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 18F), Left = 22, Top = 13, AutoSize = true };
            var subtitle = new Label { Text = "Customizable workflow utility", ForeColor = Color.FromArgb(215, 228, 245), Left = 145, Top = 24, AutoSize = true };
            header.Controls.Add(brand); header.Controls.Add(subtitle);
            Controls.Add(header);

            tabs.Dock = DockStyle.Fill;
            tabs.Padding = new Point(14, 6);
            Controls.Add(tabs);
            tabs.BringToFront();
            BuildHomeTab();
            BuildFoldersTab();
            BuildFunctionsTab();
            BuildSchedulesTab();
            BuildAppearanceTab();
            BuildExtensionsTab();

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Color.White };
            var save = new Button { Text = "Save settings", Width = 130, Height = 32, Left = 18, Top = 10 };
            save.Click += (s, e) => SaveFromUi();
            var run = new Button { Text = "Run selected tasks", Width = 150, Height = 32, Left = 158, Top = 10 };
            run.Click += (s, e) => RunSelectedTasks();
            var preview = new Button { Text = "Preview extension changes", Width = 190, Height = 32, Left = 318, Top = 10 };
            preview.Click += (s, e) => PreviewExtensions();
            bottom.Controls.Add(save); bottom.Controls.Add(run); bottom.Controls.Add(preview);
            Controls.Add(bottom);
            bottom.BringToFront();
        }

        private void BuildHomeTab()
        {
            var page = new TabPage("Overview");
            page.BackColor = Color.FromArgb(245, 247, 251);
            var intro = new Label { Text = "Everything important, without the clutter.", Left = 22, Top = 20, Width = 650, Height = 32, Font = new Font("Segoe UI Semibold", 15F) };
            page.Controls.Add(intro);
            var info = new Label { Text = "Configure folders, automation, timing, photo preferences, and extension rules in the tabs above.", Left = 24, Top = 64, Width = 760, Height = 42 };
            page.Controls.Add(info);
            logBox.Multiline = true; logBox.ReadOnly = true; logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Font = new Font("Consolas", 9F); logBox.Left = 22; logBox.Top = 125; logBox.Width = 850; logBox.Height = 300; logBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            page.Controls.Add(logBox);
            var clear = new Button { Text = "Clear log", Left = 22, Top = 438, Width = 100, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            clear.Click += (s, e) => logBox.Clear();
            page.Controls.Add(clear);
            tabs.TabPages.Add(page);
        }

        private void BuildFoldersTab()
        {
            var page = new TabPage("Folders");
            page.BackColor = Color.FromArgb(245, 247, 251);
            for (int i = 0; i < folderNames.Length; i++)
            {
                int y = 30 + i * 72;
                page.Controls.Add(LabelFor(folderNames[i] + " folder", 24, y, 150));
                folderBoxes[i] = new TextBox { Left = 180, Top = y, Width = 540, Height = 28, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                page.Controls.Add(folderBoxes[i]);
                var browse = new Button { Text = "Browse…", Left = 730, Top = y - 1, Width = 95, Anchor = AnchorStyles.Top | AnchorStyles.Right };
                int index = i;
                browse.Click += (s, e) =>
                {
                    using (var dialog = new FolderBrowserDialog())
                    {
                        dialog.SelectedPath = folderBoxes[index].Text;
                        if (dialog.ShowDialog(this) == DialogResult.OK) folderBoxes[index].Text = dialog.SelectedPath;
                    }
                };
                page.Controls.Add(browse);
            }
            var note = new Label { Text = "Folder paths are saved locally and can be changed at any time.", Left = 24, Top = 330, Width = 600 };
            page.Controls.Add(note);
            tabs.TabPages.Add(page);
        }

        private void BuildFunctionsTab()
        {
            var page = new TabPage("Functions");
            page.BackColor = Color.FromArgb(245, 247, 251);
            fourWallets.Text = "Enable 4-Wallets processing"; fourWallets.SetBounds(28, 30, 320, 28);
            errorFix.Text = "Enable Auto Error Fix"; errorFix.SetBounds(28, 75, 320, 28);
            orderSeparation.Text = "Enable Order Separation"; orderSeparation.SetBounds(28, 120, 320, 28);
            confirm.Text = "Ask for confirmation before processing"; confirm.SetBounds(28, 165, 360, 28);
            page.Controls.Add(fourWallets); page.Controls.Add(errorFix); page.Controls.Add(orderSeparation); page.Controls.Add(confirm);
            page.Controls.Add(LabelFor("Photo size preference", 28, 220, 180));
            photoSize.SetBounds(220, 220, 240, 28); page.Controls.Add(photoSize);
            page.Controls.Add(LabelFor("Photo layout preference", 28, 265, 180));
            photoLayout.SetBounds(220, 265, 240, 28); page.Controls.Add(photoLayout);
            var note = new Label { Text = "These options are configurable. Exact processing behavior will be implemented as the original rules are verified.", Left = 28, Top = 320, Width = 760, Height = 44 };
            page.Controls.Add(note);
            tabs.TabPages.Add(page);
        }

        private void BuildSchedulesTab()
        {
            var page = new TabPage("Timing");
            page.BackColor = Color.FromArgb(245, 247, 251);
            scheduleGrid.SetBounds(18, 22, 840, 300);
            scheduleGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            scheduleGrid.AllowUserToAddRows = false;
            scheduleGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            scheduleGrid.RowHeadersVisible = false;
            scheduleGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            scheduleGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "On" });
            scheduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Schedule name" });
            scheduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Start", HeaderText = "Start (HH:mm)" });
            scheduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "End", HeaderText = "End (HH:mm)" });
            scheduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Days", HeaderText = "Days (Mon,Tue,…)" });
            page.Controls.Add(scheduleGrid);
            page.Controls.Add(new Label { Text = "Edit all six schedule groups here. Times use 24-hour HH:mm format.", Left = 20, Top = 338, Width = 700 });
            tabs.TabPages.Add(page);
        }

        private void BuildAppearanceTab()
        {
            var page = new TabPage("Appearance");
            page.BackColor = Color.FromArgb(245, 247, 251);
            darkTheme.Text = "Use dark theme"; darkTheme.SetBounds(28, 32, 240, 28);
            startup.Text = "Run AUTO4W at startup (preference only in this starter)"; startup.SetBounds(28, 78, 500, 28);
            page.Controls.Add(darkTheme); page.Controls.Add(startup);
            page.Controls.Add(LabelFor("Accent color name", 28, 132, 170));
            var accent = new TextBox { Name = "AccentColor", Left = 210, Top = 132, Width = 220 };
            accent.TextChanged += (s, e) => settings.AccentColor = accent.Text;
            page.Controls.Add(accent);
            page.Controls.Add(new Label { Text = "More appearance options can be added without changing the processing modules.", Left = 28, Top = 190, Width = 700 });
            tabs.TabPages.Add(page);
        }

        private void BuildExtensionsTab()
        {
            var page = new TabPage("Extensions");
            page.BackColor = Color.FromArgb(245, 247, 251);
            page.Controls.Add(LabelFor("Old extension", 28, 35, 150));
            oldExt.SetBounds(190, 35, 230, 28); page.Controls.Add(oldExt);
            page.Controls.Add(LabelFor("New extension", 28, 82, 150));
            newExt.SetBounds(190, 82, 230, 28); page.Controls.Add(newExt);
            page.Controls.Add(new Label { Text = "Preview lists candidate changes only; processing requires confirmation when that option is enabled.", Left = 28, Top = 145, Width = 720, Height = 44 });
            tabs.TabPages.Add(page);
        }

        private void LoadSettingsToUi()
        {
            folderBoxes[0].Text = settings.IncomingFolder;
            folderBoxes[1].Text = settings.ToprintFolder;
            folderBoxes[2].Text = settings.DigitalFolder;
            folderBoxes[3].Text = settings.ErrorsFolder;
            fourWallets.Checked = settings.FourWalletsEnabled;
            errorFix.Checked = settings.AutoErrorFixEnabled;
            orderSeparation.Checked = settings.OrderSeparationEnabled;
            confirm.Checked = settings.ConfirmBeforeProcessing;
            startup.Checked = settings.RunAtStartup;
            darkTheme.Checked = settings.DarkTheme;
            oldExt.Text = settings.OldExtension;
            newExt.Text = settings.NewExtension;
            photoSize.Text = settings.PhotoSize;
            photoLayout.Text = settings.PhotoLayout;
            scheduleGrid.Rows.Clear();
            foreach (var schedule in settings.Schedules)
                scheduleGrid.Rows.Add(schedule.Enabled, schedule.Name, schedule.StartTime, schedule.EndTime, schedule.Days);
        }

        private void SaveFromUi()
        {
            settings.IncomingFolder = folderBoxes[0].Text.Trim();
            settings.ToprintFolder = folderBoxes[1].Text.Trim();
            settings.DigitalFolder = folderBoxes[2].Text.Trim();
            settings.ErrorsFolder = folderBoxes[3].Text.Trim();
            settings.FourWalletsEnabled = fourWallets.Checked;
            settings.AutoErrorFixEnabled = errorFix.Checked;
            settings.OrderSeparationEnabled = orderSeparation.Checked;
            settings.ConfirmBeforeProcessing = confirm.Checked;
            settings.RunAtStartup = startup.Checked;
            settings.DarkTheme = darkTheme.Checked;
            settings.OldExtension = NormalizeExtension(oldExt.Text);
            settings.NewExtension = NormalizeExtension(newExt.Text);
            settings.PhotoSize = photoSize.Text.Trim();
            settings.PhotoLayout = photoLayout.Text.Trim();
            settings.Schedules.Clear();
            foreach (DataGridViewRow row in scheduleGrid.Rows)
            {
                settings.Schedules.Add(new ScheduleGroup {
                    Enabled = Convert.ToBoolean(row.Cells[0].Value ?? false),
                    Name = Convert.ToString(row.Cells[1].Value ?? "Schedule"),
                    StartTime = Convert.ToString(row.Cells[2].Value ?? "09:00"),
                    EndTime = Convert.ToString(row.Cells[3].Value ?? "17:00"),
                    Days = Convert.ToString(row.Cells[4].Value ?? "Mon,Tue,Wed,Thu,Fri")
                });
            }
            while (settings.Schedules.Count < 6) settings.Schedules.Add(new ScheduleGroup());
            settings.Save();
            ApplyTheme();
            Log("Settings saved to " + AppSettings.SettingsPath);
            MessageBox.Show(this, "Settings saved.", "AUTO4W", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string NormalizeExtension(string value)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0) return "";
            return value.StartsWith(".") ? value : "." + value;
        }

        private void ApplyTheme()
        {
            var background = settings.DarkTheme ? Color.FromArgb(31, 36, 46) : Color.FromArgb(245, 247, 251);
            var foreground = settings.DarkTheme ? Color.FromArgb(235, 239, 245) : Color.FromArgb(35, 45, 62);
            foreach (TabPage page in tabs.TabPages)
            {
                page.BackColor = background;
                foreach (Control control in page.Controls)
                {
                    if (!(control is TextBox) && !(control is DataGridView) && !(control is Button))
                        control.ForeColor = foreground;
                    if (!(control is TextBox) && !(control is DataGridView))
                        control.BackColor = background;
                }
            }
            BackColor = background;
        }

        private void RunSelectedTasks()
        {
            SaveSilently();
            Log("Requested tasks:");
            Log("4-Wallets: " + (settings.FourWalletsEnabled ? "enabled" : "disabled"));
            Log("Auto Error Fix: " + (settings.AutoErrorFixEnabled ? "enabled" : "disabled"));
            Log("Order Separation: " + (settings.OrderSeparationEnabled ? "enabled" : "disabled"));
            Log("Task execution modules are being reconstructed; no unverified destructive actions were run.");
            MessageBox.Show(this, "Configuration loaded. Processing modules will be activated as the original rules are verified.", "AUTO4W", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void PreviewExtensions()
        {
            SaveSilently();
            if (!Directory.Exists(settings.IncomingFolder))
            {
                MessageBox.Show(this, "Incoming folder does not exist. Update it in Folders settings.", "AUTO4W", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var oldExtension = NormalizeExtension(settings.OldExtension);
            if (String.IsNullOrWhiteSpace(oldExtension))
            {
                MessageBox.Show(this, "Enter an old extension first.", "AUTO4W", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var files = Directory.GetFiles(settings.IncomingFolder)
                .Where(f => String.Equals(Path.GetExtension(f), oldExtension, StringComparison.OrdinalIgnoreCase))
                .Take(100).ToArray();
            Log("Extension preview: found " + files.Length + " matching file(s). No files changed.");
            foreach (var file in files.Take(12))
                Log(Path.GetFileName(file) + "  ->  " + Path.GetFileNameWithoutExtension(file) + NormalizeExtension(settings.NewExtension));
            if (files.Length > 12) Log("…and " + (files.Length - 12) + " more.");
            MessageBox.Show(this, "Preview complete. No files were changed. See the Overview activity log.", "AUTO4W", MessageBoxButtons.OK, MessageBoxIcon.Information);
            tabs.SelectedIndex = 0;
        }

        private void SaveSilently()
        {
            try
            {
                settings.IncomingFolder = folderBoxes[0].Text.Trim();
                settings.ToprintFolder = folderBoxes[1].Text.Trim();
                settings.DigitalFolder = folderBoxes[2].Text.Trim();
                settings.ErrorsFolder = folderBoxes[3].Text.Trim();
                settings.FourWalletsEnabled = fourWallets.Checked;
                settings.AutoErrorFixEnabled = errorFix.Checked;
                settings.OrderSeparationEnabled = orderSeparation.Checked;
                settings.ConfirmBeforeProcessing = confirm.Checked;
                settings.RunAtStartup = startup.Checked;
                settings.DarkTheme = darkTheme.Checked;
                settings.OldExtension = NormalizeExtension(oldExt.Text);
                settings.NewExtension = NormalizeExtension(newExt.Text);
                settings.PhotoSize = photoSize.Text.Trim();
                settings.PhotoLayout = photoLayout.Text.Trim();
                settings.Save();
            }
            catch (Exception ex) { Log("Could not save settings: " + ex.Message); }
        }

        private void Log(string message)
        {
            if (logBox.IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), message); return; }
            logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        }
    }
}
