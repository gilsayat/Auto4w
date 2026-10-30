using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AUTO4W
{
    public class MainForm : Form
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private const string AddSizeOptionText = "+ Add another size...";

        private static readonly HashSet<string> NonOrderExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp",
            ".exe", ".dll", ".pdb", ".zip", ".rar", ".7z", ".ini", ".db", ".tmp"
        };

        private AppSettings settings;
        private volatile bool isRunning = false;
        private int isProcessingFlag = 0;
        private bool isLoadingUi = false;
        private string lastSelectedSize = "4-Wallets Vert";

        // Counters
        private int countSeparated = 0;
        private int count4W = 0;
        private int countErrFixed = 0;

        // Timers & Watchers
        private readonly System.Windows.Forms.Timer backgroundSweepTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer scheduleAndHealthTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer saveDebounceTimer = new System.Windows.Forms.Timer();
        private FileSystemWatcher incomingWatcher;
        private FileSystemWatcher errorsWatcher;
        private readonly HashSet<string> processedInPlaceSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Header UI
        private Panel headerPanel;
        private Label titleLabel;
        private Button themeBtn;

        // Toggles UI
        private ModernToggle toggleSeparation;
        private ModernToggle toggleAuto4W;
        private ModernToggle toggleErrorFix;
        private Label lblSeparation;
        private Label lblAuto4W;
        private Label lblErrorFix;

        // 4W Size UI
        private Label lbl4WSize;
        private ComboBox cmb4WSize;

        // 4W Schedule Card UI
        private Panel schedCard;
        private CheckBox chk4WSchedule;
        private DateTimePicker dtpStart;
        private Label lblTimeTo;
        private DateTimePicker dtpEnd;
        private CheckBox chkUseDate;
        private DateTimePicker dtpDate;
        private Label lblDays;
        private readonly Button[] dayButtons = new Button[7];
        private readonly bool[] activeDays = new bool[7] { true, true, true, true, true, true, true };

        // Folders UI
        private readonly Panel[] folderCards = new Panel[4];
        private readonly Label[] folderDots = new Label[4];
        private readonly Label[] folderLabels = new Label[4];
        private readonly TextBox[] folderBoxes = new TextBox[4];
        private readonly Button[] browseButtons = new Button[4];

        // Start / Stop & History UI
        private Button btnStartStop;
        private Label lblHistoryHeader;
        private Button btnClearHistory;
        private ListBox lstHistory;

        // Status Bar UI
        private Panel statusPanel;
        private Label lblStatusDot;
        private Label lblStatusText;
        private Label lblAuthor;

        public MainForm()
        {
            DoubleBuffered = true;
            settings = AppSettings.Load();

            Text = "AUTO4W";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(385, 480); // Compact, sleek footprint
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);

            SetCustom4WIcon();
            BuildCompactUi();
            LoadSettingsIntoUi();
            ApplyTheme();

            // Debounced settings saver (prevents UI lag while typing)
            saveDebounceTimer.Interval = 800;
            saveDebounceTimer.Tick += (s, e) =>
            {
                saveDebounceTimer.Stop();
                CommitUiToSettings();
            };

            // Relaxed fallback sweep (every 6 seconds on background thread)
            backgroundSweepTimer.Interval = 6000;
            backgroundSweepTimer.Tick += (s, e) => TriggerBackgroundCycle();

            // 1-second lightweight tick for 4W schedule check & folder health indicators
            scheduleAndHealthTimer.Interval = 1500;
            scheduleAndHealthTimer.Tick += ScheduleAndHealthTimer_Tick;
            scheduleAndHealthTimer.Start();

            CheckFolderHealthAsync();
            AppendLogToUi(settings.AddLog("AUTO4W ready. Press START to lock & run."));
        }

        private void SetCustom4WIcon()
        {
            try
            {
                using (Bitmap bmp = new Bitmap(64, 64))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);

                    using (GraphicsPath path = CreateRoundedRect(new Rectangle(2, 2, 60, 60), 14))
                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        new Rectangle(0, 0, 64, 64),
                        Color.FromArgb(0, 184, 194),
                        Color.FromArgb(24, 98, 178),
                        LinearGradientMode.ForwardDiagonal))
                    {
                        g.FillPath(brush, path);
                    }

                    using (Font f = new Font("Segoe UI", 22F, FontStyle.Bold))
                    {
                        SizeF size = g.MeasureString("4W", f);
                        g.DrawString("4W", f, Brushes.White, (64 - size.Width) / 2f, (64 - size.Height) / 2f);
                    }

                    IntPtr hIcon = bmp.GetHicon();
                    Icon = (Icon)Icon.FromHandle(hIcon).Clone();
                    DestroyIcon(hIcon);
                }
            }
            catch
            {
            }
        }

        private void BuildCompactUi()
        {
            // 1. Header Bar (Height: 34)
            headerPanel = new Panel { Dock = DockStyle.Top, Height = 34 };
            var badge = new Label
            {
                Text = "4W",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(18, 138, 130),
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(10, 6, 26, 22)
            };
            titleLabel = new Label
            {
                Text = "AUTO4W",
                Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(42, 7)
            };
            themeBtn = new Button
            {
                Bounds = new Rectangle(305, 5, 70, 24),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.75F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            themeBtn.FlatAppearance.BorderSize = 1;
            themeBtn.Click += (s, e) =>
            {
                settings.DarkTheme = !settings.DarkTheme;
                settings.SaveAsync();
                ApplyTheme();
            };
            headerPanel.Controls.Add(badge);
            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(themeBtn);
            Controls.Add(headerPanel);

            // 2. Three Toggles Row (y = 40..80)
            toggleSeparation = new ModernToggle { Location = new Point(41, 40) };
            toggleAuto4W = new ModernToggle { Location = new Point(168, 40) };
            toggleErrorFix = new ModernToggle { Location = new Point(296, 40) };

            toggleSeparation.CheckedChanged += (s, e) => QueueSettingsSave();
            toggleAuto4W.CheckedChanged += (s, e) => QueueSettingsSave();
            toggleErrorFix.CheckedChanged += (s, e) => QueueSettingsSave();

            lblSeparation = new Label
            {
                Text = "Order Separation",
                Bounds = new Rectangle(4, 64, 122, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 7.75F, FontStyle.Bold)
            };
            lblAuto4W = new Label
            {
                Text = "Auto 4W",
                Bounds = new Rectangle(131, 64, 122, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 7.75F, FontStyle.Bold)
            };
            lblErrorFix = new Label
            {
                Text = "Auto Error Fix",
                Bounds = new Rectangle(259, 64, 122, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 7.75F, FontStyle.Bold)
            };

            Controls.Add(toggleSeparation);
            Controls.Add(toggleAuto4W);
            Controls.Add(toggleErrorFix);
            Controls.Add(lblSeparation);
            Controls.Add(lblAuto4W);
            Controls.Add(lblErrorFix);

            // 3. 4W Size Selector Row (y = 85)
            lbl4WSize = new Label
            {
                Text = "4W Size:",
                Bounds = new Rectangle(10, 87, 54, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Semibold", 8.25F, FontStyle.Bold)
            };
            cmb4WSize = new ComboBox
            {
                Bounds = new Rectangle(66, 85, 309, 22),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 8.25F)
            };
            cmb4WSize.SelectedIndexChanged += Cmb4WSize_SelectedIndexChanged;
            Controls.Add(lbl4WSize);
            Controls.Add(cmb4WSize);

            // 4. Auto 4W Schedule Card (y = 113, Height = 50)
            schedCard = new Panel { Bounds = new Rectangle(10, 113, 365, 50) };

            chk4WSchedule = new CheckBox
            {
                Text = "4W Timer",
                Bounds = new Rectangle(6, 4, 72, 20),
                Font = new Font("Segoe UI Semibold", 7.75F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            chk4WSchedule.CheckedChanged += (s, e) => QueueSettingsSave();

            dtpStart = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm",
                ShowUpDown = true,
                Bounds = new Rectangle(80, 3, 56, 21),
                Font = new Font("Segoe UI", 7.75F)
            };
            dtpStart.ValueChanged += (s, e) => QueueSettingsSave();

            lblTimeTo = new Label
            {
                Text = "to",
                Bounds = new Rectangle(137, 6, 16, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 7.5F)
            };

            dtpEnd = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm",
                ShowUpDown = true,
                Bounds = new Rectangle(154, 3, 56, 21),
                Font = new Font("Segoe UI", 7.75F)
            };
            dtpEnd.ValueChanged += (s, e) => QueueSettingsSave();

            chkUseDate = new CheckBox
            {
                Text = "Date:",
                Bounds = new Rectangle(216, 4, 50, 20),
                Font = new Font("Segoe UI", 7.5F),
                Cursor = Cursors.Hand
            };
            chkUseDate.CheckedChanged += (s, e) =>
            {
                dtpDate.Enabled = !isRunning && chkUseDate.Checked;
                QueueSettingsSave();
            };

            dtpDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MM/dd/yy",
                Bounds = new Rectangle(267, 3, 92, 21),
                Font = new Font("Segoe UI", 7.75F)
            };
            dtpDate.ValueChanged += (s, e) => QueueSettingsSave();

            lblDays = new Label
            {
                Text = "4W Days:",
                Bounds = new Rectangle(6, 28, 70, 18),
                Font = new Font("Segoe UI", 7.5F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft
            };

            string[] dayLetters = { "M", "T", "W", "T", "F", "S", "S" };
            for (int i = 0; i < 7; i++)
            {
                int dIdx = i;
                dayButtons[i] = new Button
                {
                    Text = dayLetters[i],
                    Bounds = new Rectangle(80 + (i * 40), 27, 36, 19),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                dayButtons[i].FlatAppearance.BorderSize = 1;
                dayButtons[i].Click += (s, e) =>
                {
                    if (isRunning) return;
                    activeDays[dIdx] = !activeDays[dIdx];
                    UpdateDayButtonsTheme();
                    QueueSettingsSave();
                };
                schedCard.Controls.Add(dayButtons[i]);
            }

            schedCard.Controls.Add(chk4WSchedule);
            schedCard.Controls.Add(dtpStart);
            schedCard.Controls.Add(lblTimeTo);
            schedCard.Controls.Add(dtpEnd);
            schedCard.Controls.Add(chkUseDate);
            schedCard.Controls.Add(dtpDate);
            schedCard.Controls.Add(lblDays);
            Controls.Add(schedCard);

            // 5. Four Folder Rows (y = 169..285)
            string[] titles = { "Incoming", "ToPrint", "Digital", "Errors (Sub)" };
            for (int i = 0; i < 4; i++)
            {
                int y = 169 + (i * 29);
                folderCards[i] = new Panel { Bounds = new Rectangle(10, y, 365, 26) };

                folderDots[i] = new Label
                {
                    Text = "●",
                    Bounds = new Rectangle(4, 5, 12, 16),
                    Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                    ForeColor = Color.Gray
                };

                folderLabels[i] = new Label
                {
                    Text = titles[i],
                    Bounds = new Rectangle(17, 5, 76, 16),
                    Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                folderBoxes[i] = new TextBox
                {
                    Bounds = new Rectangle(95, 5, 238, 17),
                    BorderStyle = BorderStyle.None,
                    Font = new Font("Segoe UI", 8F)
                };
                folderBoxes[i].TextChanged += (s, e) => QueueSettingsSave();

                int idx = i;
                browseButtons[i] = new Button
                {
                    Text = "...",
                    Bounds = new Rectangle(337, 2, 25, 22),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                browseButtons[i].FlatAppearance.BorderSize = 0;
                browseButtons[i].Click += (s, e) => BrowseForFolder(idx);

                folderCards[i].Controls.Add(folderDots[i]);
                folderCards[i].Controls.Add(folderLabels[i]);
                folderCards[i].Controls.Add(folderBoxes[i]);
                folderCards[i].Controls.Add(browseButtons[i]);
                Controls.Add(folderCards[i]);
            }

            // 6. Start / Stop Button (y = 289, Height = 32)
            btnStartStop = new Button
            {
                Bounds = new Rectangle(10, 289, 365, 32),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnStartStop.FlatAppearance.BorderSize = 0;
            btnStartStop.Click += BtnStartStop_Click;
            Controls.Add(btnStartStop);

            // 7. History Log Section (y = 326..452)
            lblHistoryHeader = new Label
            {
                Text = "HISTORY LOG (7-DAY AUTO CLEAN)",
                Bounds = new Rectangle(10, 327, 230, 16),
                Font = new Font("Segoe UI", 7.25F, FontStyle.Bold)
            };
            btnClearHistory = new Button
            {
                Text = "Clear",
                Bounds = new Rectangle(327, 325, 48, 19),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnClearHistory.FlatAppearance.BorderSize = 1;
            btnClearHistory.Click += (s, e) =>
            {
                settings.ClearLogs();
                lstHistory.Items.Clear();
            };

            lstHistory = new ListBox
            {
                Bounds = new Rectangle(10, 346, 365, 104),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 7.75F),
                HorizontalScrollbar = true
            };

            Controls.Add(lblHistoryHeader);
            Controls.Add(btnClearHistory);
            Controls.Add(lstHistory);

            // 8. Bottom Status Bar (Height = 24, with "Created by Gil" on right)
            statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 24 };
            lblStatusDot = new Label
            {
                Text = "●",
                Bounds = new Rectangle(8, 3, 14, 16),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Bold)
            };
            lblStatusText = new Label
            {
                Bounds = new Rectangle(22, 4, 255, 16),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft
            };
            lblAuthor = new Label
            {
                Text = "Created by Gil",
                Bounds = new Rectangle(280, 5, 97, 15),
                Font = new Font("Segoe UI", 7.25F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleRight
            };
            statusPanel.Controls.Add(lblStatusDot);
            statusPanel.Controls.Add(lblStatusText);
            statusPanel.Controls.Add(lblAuthor);
            Controls.Add(statusPanel);

            UpdateStartStopVisuals();
        }

        private void Populate4WSizeDropdown(string selectSize)
        {
            isLoadingUi = true;
            cmb4WSize.Items.Clear();

            foreach (string def in AppSettings.Default4WSizes)
            {
                cmb4WSize.Items.Add(def);
            }
            foreach (string custom in settings.Custom4WSizes)
            {
                if (!cmb4WSize.Items.Contains(custom))
                {
                    cmb4WSize.Items.Add(custom);
                }
            }
            cmb4WSize.Items.Add(AddSizeOptionText);

            int idx = cmb4WSize.FindStringExact(selectSize ?? "4-Wallets Vert");
            cmb4WSize.SelectedIndex = idx >= 0 ? idx : 0;
            lastSelectedSize = cmb4WSize.SelectedItem.ToString();
            isLoadingUi = false;
        }

        private void Cmb4WSize_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isLoadingUi || cmb4WSize.SelectedItem == null) return;

            string chosen = cmb4WSize.SelectedItem.ToString();
            if (chosen == AddSizeOptionText)
            {
                string added = PromptForNew4WSize();
                if (!string.IsNullOrWhiteSpace(added))
                {
                    added = added.Trim();
                    if (!AppSettings.Default4WSizes.Contains(added, StringComparer.OrdinalIgnoreCase) &&
                        !settings.Custom4WSizes.Contains(added, StringComparer.OrdinalIgnoreCase))
                    {
                        settings.Custom4WSizes.Add(added);
                    }
                    settings.Selected4WSize = added;
                    Populate4WSizeDropdown(added);
                    settings.SaveAsync();
                    AppendLogToUi(settings.AddLog("Added custom 4W size: " + added));
                }
                else
                {
                    Populate4WSizeDropdown(lastSelectedSize);
                }
            }
            else
            {
                lastSelectedSize = chosen;
                QueueSettingsSave();
            }
        }

        private string PromptForNew4WSize()
        {
            bool dark = settings.DarkTheme;
            using (Form prompt = new Form())
            {
                prompt.Text = "Add 4W Size";
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.ClientSize = new Size(280, 105);
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;
                prompt.BackColor = dark ? Color.FromArgb(28, 33, 43) : Color.FromArgb(245, 247, 250);
                prompt.ForeColor = dark ? Color.White : Color.Black;
                prompt.Font = new Font("Segoe UI", 8.5F);

                Label lbl = new Label { Text = "Enter new 4W size name:", Bounds = new Rectangle(14, 12, 250, 18) };
                TextBox txt = new TextBox
                {
                    Bounds = new Rectangle(14, 34, 252, 23),
                    BackColor = dark ? Color.FromArgb(40, 47, 60) : Color.White,
                    ForeColor = dark ? Color.White : Color.Black
                };
                Button btnOk = new Button
                {
                    Text = "Add Size",
                    DialogResult = DialogResult.OK,
                    Bounds = new Rectangle(110, 68, 75, 26),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(28, 134, 92),
                    ForeColor = Color.White
                };
                btnOk.FlatAppearance.BorderSize = 0;

                Button btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Bounds = new Rectangle(191, 68, 75, 26),
                    FlatStyle = FlatStyle.Flat
                };

                prompt.Controls.Add(lbl);
                prompt.Controls.Add(txt);
                prompt.Controls.Add(btnOk);
                prompt.Controls.Add(btnCancel);
                prompt.AcceptButton = btnOk;
                prompt.CancelButton = btnCancel;

                return prompt.ShowDialog(this) == DialogResult.OK ? txt.Text : null;
            }
        }

        private void LoadSettingsIntoUi()
        {
            isLoadingUi = true;

            folderBoxes[0].Text = settings.IncomingFolder;
            folderBoxes[1].Text = settings.ToprintFolder;
            folderBoxes[2].Text = settings.DigitalFolder;
            folderBoxes[3].Text = settings.ErrorsFolder;

            toggleSeparation.Checked = settings.OrderSeparationEnabled;
            toggleAuto4W.Checked = settings.FourWalletsEnabled;
            toggleErrorFix.Checked = settings.AutoErrorFixEnabled;

            Populate4WSizeDropdown(settings.Selected4WSize);

            chk4WSchedule.Checked = settings.Schedule4WEnabled;
            DateTime tStart, tEnd, tDate;
            if (DateTime.TryParseExact(settings.Schedule4WStart, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out tStart))
                dtpStart.Value = tStart;
            if (DateTime.TryParseExact(settings.Schedule4WEnd, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out tEnd))
                dtpEnd.Value = tEnd;
            if (DateTime.TryParseExact(settings.Schedule4WDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out tDate))
                dtpDate.Value = tDate;

            chkUseDate.Checked = settings.Schedule4WUseDate;
            dtpDate.Enabled = chkUseDate.Checked;

            string[] parts = (settings.Schedule4WDays ?? "1,1,1,1,1,1,1").Split(',');
            for (int i = 0; i < 7; i++)
            {
                activeDays[i] = i >= parts.Length || parts[i].Trim() == "1";
            }
            UpdateDayButtonsTheme();

            lstHistory.BeginUpdate();
            lstHistory.Items.Clear();
            foreach (var entry in settings.HistoryLogs)
            {
                lstHistory.Items.Add(entry.ToString());
            }
            if (lstHistory.Items.Count > 0)
            {
                lstHistory.TopIndex = lstHistory.Items.Count - 1;
            }
            lstHistory.EndUpdate();

            isLoadingUi = false;
        }

        private void QueueSettingsSave()
        {
            if (isLoadingUi) return;
            saveDebounceTimer.Stop();
            saveDebounceTimer.Start();
        }

        private void CommitUiToSettings()
        {
            if (isLoadingUi || folderBoxes[0] == null) return;

            settings.IncomingFolder = folderBoxes[0].Text.Trim();
            settings.ToprintFolder = folderBoxes[1].Text.Trim();
            settings.DigitalFolder = folderBoxes[2].Text.Trim();
            settings.ErrorsFolder = folderBoxes[3].Text.Trim();

            settings.OrderSeparationEnabled = toggleSeparation.Checked;
            settings.FourWalletsEnabled = toggleAuto4W.Checked;
            settings.AutoErrorFixEnabled = toggleErrorFix.Checked;

            if (cmb4WSize.SelectedItem != null && cmb4WSize.SelectedItem.ToString() != AddSizeOptionText)
            {
                settings.Selected4WSize = cmb4WSize.SelectedItem.ToString();
            }

            settings.Schedule4WEnabled = chk4WSchedule.Checked;
            settings.Schedule4WStart = dtpStart.Value.ToString("HH:mm");
            settings.Schedule4WEnd = dtpEnd.Value.ToString("HH:mm");
            settings.Schedule4WUseDate = chkUseDate.Checked;
            settings.Schedule4WDate = dtpDate.Value.ToString("yyyy-MM-dd");
            settings.Schedule4WDays = string.Join(",", activeDays.Select(b => b ? "1" : "0"));

            settings.SaveAsync();
            CheckFolderHealthAsync();
        }

        private void BrowseForFolder(int index)
        {
            if (isRunning) return;
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = folderBoxes[index].Text;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    folderBoxes[index].Text = dialog.SelectedPath;
                    CommitUiToSettings();
                }
            }
        }

        private void BtnStartStop_Click(object sender, EventArgs e)
        {
            if (!isRunning)
            {
                CommitUiToSettings();

                if (!settings.OrderSeparationEnabled && !settings.FourWalletsEnabled && !settings.AutoErrorFixEnabled && !settings.Schedule4WEnabled)
                {
                    MessageBox.Show(this, "Turn on at least one toggle (or 4W Timer) before starting.", "AUTO4W", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                isRunning = true;
                Evaluate4WScheduleWindow();
                UpdateStartStopVisuals();
                StartWatchers();
                backgroundSweepTimer.Start();

                AppendLogToUi(settings.AddLog(string.Format("STARTED [Sep:{0} | 4W:{1} | ErrFix:{2} | 4WSched:{3}]",
                    settings.OrderSeparationEnabled ? "ON" : "OFF",
                    settings.FourWalletsEnabled ? "ON" : "OFF",
                    settings.AutoErrorFixEnabled ? "ON" : "OFF",
                    settings.Schedule4WEnabled ? "ON" : "OFF")));

                TriggerBackgroundCycle();
            }
            else
            {
                isRunning = false;
                backgroundSweepTimer.Stop();
                StopWatchers();
                UpdateStartStopVisuals();
                AppendLogToUi(settings.AddLog("STOPPED — Controls unlocked."));
            }
        }

        private void UpdateStartStopVisuals()
        {
            toggleSeparation.Enabled = !isRunning;
            toggleAuto4W.Enabled = !isRunning;
            toggleErrorFix.Enabled = !isRunning;
            cmb4WSize.Enabled = !isRunning;

            chk4WSchedule.Enabled = !isRunning;
            dtpStart.Enabled = !isRunning;
            dtpEnd.Enabled = !isRunning;
            chkUseDate.Enabled = !isRunning;
            dtpDate.Enabled = !isRunning && chkUseDate.Checked;

            for (int i = 0; i < 4; i++)
            {
                folderBoxes[i].ReadOnly = isRunning;
                browseButtons[i].Enabled = !isRunning;
            }

            UpdateDayButtonsTheme();

            if (isRunning)
            {
                btnStartStop.Text = "■   STOP ENGINE  (LOCKED)";
                btnStartStop.BackColor = Color.FromArgb(182, 58, 68);
                lblStatusDot.ForeColor = Color.FromArgb(46, 204, 113);
                lblStatusText.Text = settings.Schedule4WEnabled
                    ? "Active 24/7 • 4W Timer Armed"
                    : "Active • Watching folders (Locked)";
            }
            else
            {
                btnStartStop.Text = "▶   START ENGINE";
                btnStartStop.BackColor = Color.FromArgb(32, 134, 92);
                lblStatusDot.ForeColor = Color.FromArgb(240, 173, 78);
                lblStatusText.Text = "Stopped • Ready";
            }
        }

        // ====================================================================
        // 4W SCHEDULE TIMER (Automatically toggles ONLY Auto 4W while running)
        // ====================================================================
        private void ScheduleAndHealthTimer_Tick(object sender, EventArgs e)
        {
            if (isRunning && settings.Schedule4WEnabled)
            {
                Evaluate4WScheduleWindow();
            }
        }

        private void Evaluate4WScheduleWindow()
        {
            if (!settings.Schedule4WEnabled) return;

            DateTime now = DateTime.Now;
            int dayIndex = ((int)now.DayOfWeek + 6) % 7; // Monday = 0 .. Sunday = 6
            bool dayMatches = activeDays[dayIndex];

            if (settings.Schedule4WUseDate)
            {
                dayMatches = now.ToString("yyyy-MM-dd") == settings.Schedule4WDate;
            }

            TimeSpan startTs = dtpStart.Value.TimeOfDay;
            TimeSpan endTs = dtpEnd.Value.TimeOfDay;
            TimeSpan nowTs = now.TimeOfDay;

            bool timeMatches;
            if (startTs <= endTs)
            {
                timeMatches = nowTs >= startTs && nowTs < endTs;
            }
            else
            {
                // Supports overnight schedules (e.g. 22:00 to 06:00)
                timeMatches = nowTs >= startTs || nowTs < endTs;
            }

            bool should4WBeOn = dayMatches && timeMatches;

            if (should4WBeOn != settings.FourWalletsEnabled)
            {
                isLoadingUi = true;
                settings.FourWalletsEnabled = should4WBeOn;
                toggleAuto4W.Checked = should4WBeOn;
                isLoadingUi = false;

                settings.SaveAsync();
                AppendLogToUi(settings.AddLog("4W Timer: Auto 4W switched " + (should4WBeOn ? "ON" : "OFF")));

                if (should4WBeOn && isRunning)
                {
                    TriggerBackgroundCycle();
                }
            }
        }

        private void CheckFolderHealthAsync()
        {
            string[] paths = new string[]
            {
                settings.IncomingFolder,
                settings.ToprintFolder,
                settings.DigitalFolder,
                settings.ErrorsFolder
            };

            Task.Run(() =>
            {
                bool[] exists = new bool[4];
                for (int i = 0; i < 4; i++)
                {
                    try
                    {
                        exists[i] = !string.IsNullOrWhiteSpace(paths[i]) && Directory.Exists(paths[i]);
                    }
                    catch
                    {
                        exists[i] = false;
                    }
                }

                if (!IsDisposed && IsHandleCreated)
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                folderDots[i].ForeColor = exists[i]
                                    ? Color.FromArgb(46, 204, 113)
                                    : Color.FromArgb(220, 85, 85);
                            }
                        }));
                    }
                    catch
                    {
                    }
                }
            });
        }

        // ====================================================================
        // INSTANT EVENT WATCHERS + NON-BLOCKING BACKGROUND WORKER
        // ====================================================================
        private void StartWatchers()
        {
            StopWatchers();
            try
            {
                if (!string.IsNullOrWhiteSpace(settings.IncomingFolder) && Directory.Exists(settings.IncomingFolder))
                {
                    incomingWatcher = new FileSystemWatcher(settings.IncomingFolder, "*.txt")
                    {
                        IncludeSubdirectories = false,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
                    };
                    incomingWatcher.Created += (s, e) => TriggerBackgroundCycle();
                    incomingWatcher.Changed += (s, e) => TriggerBackgroundCycle();
                    incomingWatcher.Renamed += (s, e) => TriggerBackgroundCycle();
                    incomingWatcher.EnableRaisingEvents = true;
                }
            }
            catch
            {
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(settings.ErrorsFolder) && Directory.Exists(settings.ErrorsFolder))
                {
                    errorsWatcher = new FileSystemWatcher(settings.ErrorsFolder)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
                    };
                    errorsWatcher.Created += (s, e) => TriggerBackgroundCycle();
                    errorsWatcher.Renamed += (s, e) => TriggerBackgroundCycle();
                    errorsWatcher.EnableRaisingEvents = true;
                }
            }
            catch
            {
            }
        }

        private void StopWatchers()
        {
            try { if (incomingWatcher != null) { incomingWatcher.EnableRaisingEvents = false; incomingWatcher.Dispose(); incomingWatcher = null; } } catch { }
            try { if (errorsWatcher != null) { errorsWatcher.EnableRaisingEvents = false; errorsWatcher.Dispose(); errorsWatcher = null; } } catch { }
        }

        private void TriggerBackgroundCycle()
        {
            if (!isRunning) return;
            if (Interlocked.CompareExchange(ref isProcessingFlag, 1, 0) != 0) return;

            Task.Run(() =>
            {
                try
                {
                    if (!isRunning) return;

                    if (settings.AutoErrorFixEnabled)
                    {
                        RunAutoErrorFixBackground();
                    }

                    if (settings.OrderSeparationEnabled || settings.FourWalletsEnabled)
                    {
                        RunIncomingOrderProcessorBackground();
                    }
                }
                catch (Exception ex)
                {
                    LogFromBackground("Cycle warning: " + ex.Message);
                }
                finally
                {
                    Interlocked.Exchange(ref isProcessingFlag, 0);
                }
            });
        }

        // ====================================================================
        // MODULE 1: AUTO ERROR FIX (All Subfolders -> Rename .err / order to .txt)
        // ====================================================================
        private void RunAutoErrorFixBackground()
        {
            string errRoot = settings.ErrorsFolder;
            if (string.IsNullOrWhiteSpace(errRoot) || !Directory.Exists(errRoot)) return;

            IEnumerable<string> allFiles;
            try
            {
                allFiles = Directory.EnumerateFiles(errRoot, "*.*", SearchOption.AllDirectories);
            }
            catch
            {
                return;
            }

            foreach (string filePath in allFiles)
            {
                if (!isRunning) break;

                try
                {
                    string ext = Path.GetExtension(filePath);
                    if (string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase)) continue;
                    if (NonOrderExtensions.Contains(ext)) continue;
                    if (!IsFileReady(filePath)) continue;

                    bool isErrExt = string.Equals(ext, ".err", StringComparison.OrdinalIgnoreCase);
                    bool looksLikeOrder = isErrExt || FileContainsOrderSignature(filePath);
                    if (!looksLikeOrder) continue;

                    string targetTxtPath = Path.ChangeExtension(filePath, ".txt");
                    string origName = Path.GetFileName(filePath);
                    string newName = Path.GetFileName(targetTxtPath);

                    if (File.Exists(targetTxtPath))
                    {
                        File.Delete(targetTxtPath);
                    }

                    File.Move(filePath, targetTxtPath);
                    Interlocked.Increment(ref countErrFixed);
                    LogFromBackground("Error Fix: " + origName + " -> " + newName);
                }
                catch (Exception ex)
                {
                    LogFromBackground("Error Fix skipped " + Path.GetFileName(filePath) + ": " + ex.Message);
                }
            }
        }

        private static bool FileContainsOrderSignature(string filePath)
        {
            try
            {
                FileInfo fi = new FileInfo(filePath);
                if (fi.Length == 0 || fi.Length > 2 * 1024 * 1024) return false;

                string sample = File.ReadAllText(filePath);
                return sample.IndexOf("Media=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       sample.IndexOf("Media =", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       sample.IndexOf("FilePath=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       sample.IndexOf("OrderReferenceID", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       sample.IndexOf("ExtCabin", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        // ====================================================================
        // MODULE 2 & 3: ORDER SEPARATION & AUTO 4W (CORE LOGIC UNTOUCHED)
        // ====================================================================
        private void RunIncomingOrderProcessorBackground()
        {
            string incomingDir = settings.IncomingFolder;
            if (string.IsNullOrWhiteSpace(incomingDir) || !Directory.Exists(incomingDir)) return;

            string[] orderFiles;
            try
            {
                orderFiles = Directory.GetFiles(incomingDir, "*.txt", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return;
            }

            string chosen4WSize = string.IsNullOrWhiteSpace(settings.Selected4WSize)
                ? "4-Wallets Vert"
                : settings.Selected4WSize.Trim();

            foreach (string filePath in orderFiles)
            {
                if (!isRunning) break;

                try
                {
                    if (!IsFileReady(filePath)) continue;

                    string fileName = Path.GetFileName(filePath);
                    string content = File.ReadAllText(filePath);
                    if (string.IsNullOrWhiteSpace(content)) continue;

                    bool isDigital = Regex.IsMatch(content, @"^\s*Media\s*=\s*Digital\b", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    bool isGlossy = Regex.IsMatch(content, @"^\s*Media\s*=\s*Glossy\b", RegexOptions.IgnoreCase | RegexOptions.Multiline);

                    if (!isDigital && !isGlossy) continue;

                    string fileSignature = filePath + "|" + File.GetLastWriteTimeUtc(filePath).Ticks;
                    if (!settings.OrderSeparationEnabled && processedInPlaceSignatures.Contains(fileSignature))
                    {
                        continue;
                    }

                    if (isDigital)
                    {
                        if (settings.FourWalletsEnabled)
                        {
                            Directory.CreateDirectory(settings.ToprintFolder);
                            string toPrintCopy = ConvertDigitalOrderTo4WGlossy(content, chosen4WSize);
                            string destPrintFile = Path.Combine(settings.ToprintFolder, fileName);
                            WriteFileAtomically(destPrintFile, toPrintCopy);
                            Interlocked.Increment(ref count4W);
                            LogFromBackground("Auto 4W (Digital): Copied " + fileName + " -> ToPrint (" + chosen4WSize + ")");
                        }

                        if (settings.OrderSeparationEnabled)
                        {
                            Directory.CreateDirectory(settings.DigitalFolder);
                            string destDigitalFile = Path.Combine(settings.DigitalFolder, fileName);
                            MoveFileSafely(filePath, destDigitalFile);
                            Interlocked.Increment(ref countSeparated);
                            LogFromBackground("Separated: " + fileName + " -> Digital");
                        }
                        else
                        {
                            processedInPlaceSignatures.Add(fileSignature);
                        }
                    }
                    else if (isGlossy)
                    {
                        string finalGlossyContent = content;
                        bool added4W = false;

                        if (settings.FourWalletsEnabled)
                        {
                            finalGlossyContent = Append4WToGlossyOrder(content, chosen4WSize, out added4W);
                            if (added4W) Interlocked.Increment(ref count4W);
                        }

                        if (settings.OrderSeparationEnabled)
                        {
                            Directory.CreateDirectory(settings.ToprintFolder);
                            string destPrintFile = Path.Combine(settings.ToprintFolder, fileName);

                            if (added4W)
                            {
                                WriteFileAtomically(destPrintFile, finalGlossyContent);
                                File.Delete(filePath);
                                Interlocked.Increment(ref countSeparated);
                                LogFromBackground("Auto 4W + Separated: " + fileName + " -> ToPrint (+4W)");
                            }
                            else
                            {
                                MoveFileSafely(filePath, destPrintFile);
                                Interlocked.Increment(ref countSeparated);
                                LogFromBackground("Separated: " + fileName + " -> ToPrint");
                            }
                        }
                        else if (added4W)
                        {
                            WriteFileAtomically(filePath, finalGlossyContent);
                            string updatedSig = filePath + "|" + File.GetLastWriteTimeUtc(filePath).Ticks;
                            processedInPlaceSignatures.Add(updatedSig);
                            LogFromBackground("Auto 4W (Glossy): Updated " + fileName + " (+4W)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogFromBackground("Order error (" + Path.GetFileName(filePath) + "): " + ex.Message);
                }
            }
        }

        private static string ConvertDigitalOrderTo4WGlossy(string content, string fourWSize)
        {
            string updated = Regex.Replace(
                content,
                @"^(\s*Media\s*=\s*)Digital\b",
                "${1}Glossy",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);

            updated = Regex.Replace(
                updated,
                @"^(\s*Size\s*=\s*)[^\r\n]+",
                "${1}" + fourWSize,
                RegexOptions.IgnoreCase | RegexOptions.Multiline);

            return updated;
        }

        private static string Append4WToGlossyOrder(string content, string fourWSize, out bool modified)
        {
            modified = false;
            string normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = normalized.Split('\n');

            List<List<string>> blocks = new List<List<string>>();
            List<string> currentBlock = new List<string>();

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (currentBlock.Count > 0)
                    {
                        blocks.Add(currentBlock);
                        currentBlock = new List<string>();
                    }
                }
                else
                {
                    currentBlock.Add(line);
                }
            }
            if (currentBlock.Count > 0)
            {
                blocks.Add(currentBlock);
            }

            List<List<string>> photoBlocks = new List<List<string>>();
            foreach (var block in blocks)
            {
                bool hasSize = block.Any(l => Regex.IsMatch(l, @"^\s*Size\s*=", RegexOptions.IgnoreCase));
                bool hasFile = block.Any(l => Regex.IsMatch(l, @"^\s*FilePath\s*=", RegexOptions.IgnoreCase));
                if (hasSize && hasFile)
                {
                    photoBlocks.Add(block);
                }
            }

            if (photoBlocks.Count == 0) return content;

            int existing4WCount = photoBlocks.Count(b =>
                b.Any(l => Regex.IsMatch(l, @"^\s*Size\s*=\s*" + Regex.Escape(fourWSize) + @"\s*$", RegexOptions.IgnoreCase)));
            if (existing4WCount >= photoBlocks.Count)
            {
                return content;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(content.TrimEnd());
            sb.Append(Environment.NewLine);
            sb.Append(Environment.NewLine);

            for (int i = 0; i < photoBlocks.Count; i++)
            {
                foreach (string line in photoBlocks[i])
                {
                    if (Regex.IsMatch(line, @"^\s*Size\s*=", RegexOptions.IgnoreCase))
                    {
                        string prefix = Regex.Match(line, @"^\s*Size\s*=\s*", RegexOptions.IgnoreCase).Value;
                        sb.AppendLine(prefix + fourWSize);
                    }
                    else if (Regex.IsMatch(line, @"^\s*Media\s*=", RegexOptions.IgnoreCase))
                    {
                        string prefix = Regex.Match(line, @"^\s*Media\s*=\s*", RegexOptions.IgnoreCase).Value;
                        sb.AppendLine(prefix + "Glossy");
                    }
                    else
                    {
                        sb.AppendLine(line);
                    }
                }
                if (i < photoBlocks.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            modified = true;
            return sb.ToString();
        }

        private static bool IsFileReady(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void WriteFileAtomically(string destinationPath, string content)
        {
            string dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string tempPath = Path.Combine(dir ?? "", Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(tempPath, content, Encoding.UTF8);

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
            File.Move(tempPath, destinationPath);
        }

        private static void MoveFileSafely(string sourcePath, string destinationPath)
        {
            string dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
            File.Move(sourcePath, destinationPath);
        }

        private void LogFromBackground(string message)
        {
            LogEntry entry = settings.AddLog(message);
            if (!IsDisposed && IsHandleCreated)
            {
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        AppendLogToUi(entry);
                        UpdateCounterLabels();
                    }));
                }
                catch
                {
                }
            }
        }

        private void AppendLogToUi(LogEntry entry)
        {
            if (IsDisposed || entry == null) return;
            lstHistory.Items.Add(entry.ToString());
            if (lstHistory.Items.Count > 350)
            {
                lstHistory.Items.RemoveAt(0);
            }
            lstHistory.TopIndex = lstHistory.Items.Count - 1;
        }

        private void UpdateCounterLabels()
        {
            lblSeparation.Text = countSeparated > 0 ? "Order Sep (" + countSeparated + ")" : "Order Separation";
            lblAuto4W.Text = count4W > 0 ? "Auto 4W (" + count4W + ")" : "Auto 4W";
            lblErrorFix.Text = countErrFixed > 0 ? "Error Fix (" + countErrFixed + ")" : "Auto Error Fix";
        }

        private void UpdateDayButtonsTheme()
        {
            bool dark = settings.DarkTheme;
            for (int i = 0; i < 7; i++)
            {
                if (dayButtons[i] == null) continue;
                if (activeDays[i])
                {
                    dayButtons[i].BackColor = isRunning
                        ? Color.FromArgb(45, 95, 135)
                        : Color.FromArgb(24, 125, 195);
                    dayButtons[i].ForeColor = Color.White;
                    dayButtons[i].FlatAppearance.BorderColor = Color.FromArgb(24, 125, 195);
                }
                else
                {
                    dayButtons[i].BackColor = dark ? Color.FromArgb(42, 49, 62) : Color.FromArgb(228, 233, 240);
                    dayButtons[i].ForeColor = dark ? Color.FromArgb(130, 142, 160) : Color.FromArgb(120, 130, 145);
                    dayButtons[i].FlatAppearance.BorderColor = dark ? Color.FromArgb(55, 65, 82) : Color.FromArgb(205, 212, 222);
                }
            }
        }

        private void ApplyTheme()
        {
            bool dark = settings.DarkTheme;

            Color bgMain = dark ? Color.FromArgb(22, 26, 34) : Color.FromArgb(242, 245, 250);
            Color bgHeader = dark ? Color.FromArgb(16, 19, 26) : Color.FromArgb(255, 255, 255);
            Color bgCard = dark ? Color.FromArgb(32, 38, 50) : Color.FromArgb(255, 255, 255);
            Color fgPrimary = dark ? Color.FromArgb(238, 242, 248) : Color.FromArgb(28, 36, 48);
            Color fgSecondary = dark ? Color.FromArgb(152, 165, 184) : Color.FromArgb(100, 112, 128);
            Color btnBrowseBg = dark ? Color.FromArgb(46, 54, 70) : Color.FromArgb(228, 234, 242);

            BackColor = bgMain;
            headerPanel.BackColor = bgHeader;
            titleLabel.ForeColor = fgPrimary;

            themeBtn.Text = dark ? "☀ Light" : "🌙 Dark";
            themeBtn.BackColor = bgCard;
            themeBtn.ForeColor = fgPrimary;
            themeBtn.FlatAppearance.BorderColor = dark ? Color.FromArgb(55, 65, 82) : Color.FromArgb(210, 218, 228);

            lblSeparation.ForeColor = fgPrimary;
            lblAuto4W.ForeColor = fgPrimary;
            lblErrorFix.ForeColor = fgPrimary;

            toggleSeparation.DarkTheme = dark;
            toggleAuto4W.DarkTheme = dark;
            toggleErrorFix.DarkTheme = dark;

            lbl4WSize.ForeColor = fgPrimary;
            cmb4WSize.BackColor = bgCard;
            cmb4WSize.ForeColor = fgPrimary;

            schedCard.BackColor = bgCard;
            chk4WSchedule.ForeColor = fgPrimary;
            lblTimeTo.ForeColor = fgSecondary;
            chkUseDate.ForeColor = fgSecondary;
            lblDays.ForeColor = fgSecondary;
            UpdateDayButtonsTheme();

            for (int i = 0; i < 4; i++)
            {
                folderCards[i].BackColor = bgCard;
                folderLabels[i].ForeColor = fgPrimary;
                folderBoxes[i].BackColor = bgCard;
                folderBoxes[i].ForeColor = fgSecondary;
                browseButtons[i].BackColor = btnBrowseBg;
                browseButtons[i].ForeColor = fgPrimary;
            }

            lblHistoryHeader.ForeColor = fgSecondary;
            btnClearHistory.BackColor = bgCard;
            btnClearHistory.ForeColor = fgSecondary;
            btnClearHistory.FlatAppearance.BorderColor = dark ? Color.FromArgb(55, 65, 82) : Color.FromArgb(210, 218, 228);

            lstHistory.BackColor = bgCard;
            lstHistory.ForeColor = fgPrimary;

            statusPanel.BackColor = bgHeader;
            lblStatusText.ForeColor = fgSecondary;
            lblAuthor.ForeColor = fgSecondary;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            isRunning = false;
            StopWatchers();
            CommitUiToSettings();
            settings.Save();
            base.OnFormClosing(e);
        }

        internal static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class ModernToggle : Control
    {
        private bool isChecked = false;
        private bool darkTheme = true;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked != value)
                {
                    isChecked = value;
                    Invalidate();
                    if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
                }
            }
        }

        public bool DarkTheme
        {
            get { return darkTheme; }
            set { darkTheme = value; Invalidate(); }
        }

        public ModernToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(48, 22);
            Cursor = Cursors.Hand;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (!Enabled) return;
            Checked = !Checked;
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.No;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);

            Rectangle rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using (GraphicsPath path = MainForm.CreateRoundedRect(rect, rect.Height / 2))
            {
                if (isChecked)
                {
                    Color c1 = Enabled ? Color.FromArgb(0, 198, 207) : Color.FromArgb(65, 115, 125);
                    Color c2 = Enabled ? Color.FromArgb(28, 115, 212) : Color.FromArgb(55, 85, 120);
                    using (LinearGradientBrush brush = new LinearGradientBrush(rect, c1, c2, LinearGradientMode.Horizontal))
                    {
                        g.FillPath(brush, path);
                    }
                }
                else
                {
                    Color offBg = darkTheme ? Color.FromArgb(52, 60, 76) : Color.FromArgb(205, 212, 222);
                    if (!Enabled) offBg = darkTheme ? Color.FromArgb(38, 44, 56) : Color.FromArgb(222, 226, 232);
                    using (SolidBrush brush = new SolidBrush(offBg))
                    {
                        g.FillPath(brush, path);
                    }
                }
            }

            int thumbSize = Height - 6;
            int thumbX = isChecked ? Width - thumbSize - 4 : 3;
            Rectangle thumbRect = new Rectangle(thumbX, 3, thumbSize, thumbSize);
            Color thumbColor = Enabled ? Color.White : Color.FromArgb(190, 195, 205);

            using (SolidBrush thumbBrush = new SolidBrush(thumbColor))
            {
                g.FillEllipse(thumbBrush, thumbRect);
            }
        }
    }
}
