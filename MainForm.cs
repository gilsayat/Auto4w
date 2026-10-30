using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AUTO4W
{
    public class MainForm : Form
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private static readonly HashSet<string> NonOrderExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp",
            ".exe", ".dll", ".pdb", ".zip", ".rar", ".7z", ".ini", ".db", ".tmp"
        };

        private AppSettings settings;
        private bool isRunning = false;
        private bool isBusyCycle = false;
        private readonly Timer workerTimer = new Timer();
        private readonly HashSet<string> processedInPlaceFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // UI Controls
        private Panel headerPanel;
        private Label titleLabel;
        private Button themeBtn;

        private ModernToggle toggleSeparation;
        private ModernToggle toggleAuto4W;
        private ModernToggle toggleErrorFix;
        private Label lblSeparation;
        private Label lblAuto4W;
        private Label lblErrorFix;

        private Label lbl4WSize;
        private ComboBox cmb4WSize;

        private readonly Panel[] folderCards = new Panel[4];
        private readonly Label[] folderLabels = new Label[4];
        private readonly TextBox[] folderBoxes = new TextBox[4];
        private readonly Button[] browseButtons = new Button[4];

        private Button btnStartStop;

        private Label lblHistoryHeader;
        private Button btnClearHistory;
        private ListBox lstHistory;

        private Panel statusPanel;
        private Label lblStatusDot;
        private Label lblStatusText;

        public MainForm()
        {
            settings = AppSettings.Load();

            Text = "AUTO4W";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(420, 560);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            SetCustom4WIcon();
            BuildCompactUi();
            LoadSettingsIntoUi();
            ApplyTheme();

            workerTimer.Interval = 2000;
            workerTimer.Tick += WorkerTimer_Tick;

            Log("AUTO4W ready. Press START to lock & run.");
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
                        Color.FromArgb(20, 148, 135),
                        Color.FromArgb(24, 90, 157),
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
            // Header
            headerPanel = new Panel { Dock = DockStyle.Top, Height = 42 };
            var badge = new Label
            {
                Text = "4W",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(22, 132, 125),
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(14, 9, 30, 24)
            };
            titleLabel = new Label
            {
                Text = "AUTO4W",
                Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(50, 10)
            };
            themeBtn = new Button
            {
                Bounds = new Rectangle(326, 8, 80, 26),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            themeBtn.FlatAppearance.BorderSize = 1;
            themeBtn.Click += (s, e) =>
            {
                settings.DarkTheme = !settings.DarkTheme;
                settings.Save();
                ApplyTheme();
            };
            headerPanel.Controls.Add(badge);
            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(themeBtn);
            Controls.Add(headerPanel);

            // Toggles Section
            toggleSeparation = new ModernToggle { Location = new Point(48, 56) };
            toggleAuto4W = new ModernToggle { Location = new Point(182, 56) };
            toggleErrorFix = new ModernToggle { Location = new Point(316, 56) };

            toggleSeparation.CheckedChanged += (s, e) => SaveCurrentUiSettings();
            toggleAuto4W.CheckedChanged += (s, e) => SaveCurrentUiSettings();
            toggleErrorFix.CheckedChanged += (s, e) => SaveCurrentUiSettings();

            lblSeparation = new Label
            {
                Text = "Order Separation",
                Bounds = new Rectangle(12, 86, 128, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold)
            };
            lblAuto4W = new Label
            {
                Text = "Auto 4W",
                Bounds = new Rectangle(146, 86, 128, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold)
            };
            lblErrorFix = new Label
            {
                Text = "Auto Error Fix",
                Bounds = new Rectangle(280, 86, 128, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold)
            };

            Controls.Add(toggleSeparation);
            Controls.Add(toggleAuto4W);
            Controls.Add(toggleErrorFix);
            Controls.Add(lblSeparation);
            Controls.Add(lblAuto4W);
            Controls.Add(lblErrorFix);

            // 4W Size Row
            lbl4WSize = new Label
            {
                Text = "4W Size:",
                Bounds = new Rectangle(16, 118, 65, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold)
            };
            cmb4WSize = new ComboBox
            {
                Bounds = new Rectangle(85, 118, 320, 24),
                DropDownStyle = ComboBoxStyle.DropDown,
                Font = new Font("Segoe UI", 9F)
            };
            cmb4WSize.Items.AddRange(new object[]
            {
                "4-Wallets (vert)",
                "4W vert",
                "Wallet Combo (4W)",
                "4-Wallets",
                "8x10 and 4W on (10in)",
                "5x7 and 4-Wallet Combo (8x10)"
            });
            cmb4WSize.SelectedIndexChanged += (s, e) => SaveCurrentUiSettings();
            cmb4WSize.TextChanged += (s, e) => SaveCurrentUiSettings();

            Controls.Add(lbl4WSize);
            Controls.Add(cmb4WSize);

            // 4 Folder Rows
            string[] titles = { "Incoming", "ToPrint", "Digital", "Errors (All Sub)" };
            for (int i = 0; i < 4; i++)
            {
                int y = 152 + (i * 38);
                folderCards[i] = new Panel
                {
                    Bounds = new Rectangle(14, y, 392, 32)
                };

                folderLabels[i] = new Label
                {
                    Text = titles[i],
                    Bounds = new Rectangle(8, 6, 98, 20),
                    Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                folderBoxes[i] = new TextBox
                {
                    Bounds = new Rectangle(108, 6, 244, 20),
                    BorderStyle = BorderStyle.None,
                    Font = new Font("Segoe UI", 8.75F)
                };
                folderBoxes[i].TextChanged += (s, e) => SaveCurrentUiSettings();

                int idx = i;
                browseButtons[i] = new Button
                {
                    Text = "...",
                    Bounds = new Rectangle(356, 3, 30, 25),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                browseButtons[i].FlatAppearance.BorderSize = 0;
                browseButtons[i].Click += (s, e) => BrowseForFolder(idx);

                folderCards[i].Controls.Add(folderLabels[i]);
                folderCards[i].Controls.Add(folderBoxes[i]);
                folderCards[i].Controls.Add(browseButtons[i]);
                Controls.Add(folderCards[i]);
            }

            // Start / Stop Button
            btnStartStop = new Button
            {
                Bounds = new Rectangle(14, 312, 392, 38),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnStartStop.FlatAppearance.BorderSize = 0;
            btnStartStop.Click += BtnStartStop_Click;
            Controls.Add(btnStartStop);

            // History Log Section
            lblHistoryHeader = new Label
            {
                Text = "HISTORY LOG (7-DAY AUTO CLEAN)",
                Bounds = new Rectangle(14, 360, 260, 18),
                Font = new Font("Segoe UI", 7.75F, FontStyle.Bold)
            };
            btnClearHistory = new Button
            {
                Text = "Clear",
                Bounds = new Rectangle(350, 357, 56, 22),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
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
                Bounds = new Rectangle(14, 382, 392, 140),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.25F),
                HorizontalScrollbar = true
            };

            Controls.Add(lblHistoryHeader);
            Controls.Add(btnClearHistory);
            Controls.Add(lstHistory);

            // Bottom Status Bar
            statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 28 };
            lblStatusDot = new Label
            {
                Text = "●",
                Bounds = new Rectangle(12, 5, 16, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            lblStatusText = new Label
            {
                Bounds = new Rectangle(28, 6, 380, 18),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular)
            };
            statusPanel.Controls.Add(lblStatusDot);
            statusPanel.Controls.Add(lblStatusText);
            Controls.Add(statusPanel);

            UpdateStartStopVisuals();
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
                    SaveCurrentUiSettings();
                }
            }
        }

        private void LoadSettingsIntoUi()
        {
            folderBoxes[0].Text = settings.IncomingFolder;
            folderBoxes[1].Text = settings.ToprintFolder;
            folderBoxes[2].Text = settings.DigitalFolder;
            folderBoxes[3].Text = settings.ErrorsFolder;

            toggleSeparation.Checked = settings.OrderSeparationEnabled;
            toggleAuto4W.Checked = settings.FourWalletsEnabled;
            toggleErrorFix.Checked = settings.AutoErrorFixEnabled;

            cmb4WSize.Text = settings.Selected4WSize;

            lstHistory.Items.Clear();
            foreach (var entry in settings.HistoryLogs)
            {
                lstHistory.Items.Add(entry.ToString());
            }
            if (lstHistory.Items.Count > 0)
            {
                lstHistory.TopIndex = lstHistory.Items.Count - 1;
            }
        }

        private void SaveCurrentUiSettings()
        {
            if (folderBoxes[0] == null) return;
            settings.IncomingFolder = folderBoxes[0].Text.Trim();
            settings.ToprintFolder = folderBoxes[1].Text.Trim();
            settings.DigitalFolder = folderBoxes[2].Text.Trim();
            settings.ErrorsFolder = folderBoxes[3].Text.Trim();

            settings.OrderSeparationEnabled = toggleSeparation.Checked;
            settings.FourWalletsEnabled = toggleAuto4W.Checked;
            settings.AutoErrorFixEnabled = toggleErrorFix.Checked;

            if (!string.IsNullOrWhiteSpace(cmb4WSize.Text))
            {
                settings.Selected4WSize = cmb4WSize.Text.Trim();
            }

            settings.Save();
        }

        private void BtnStartStop_Click(object sender, EventArgs e)
        {
            if (!isRunning)
            {
                SaveCurrentUiSettings();

                if (!settings.OrderSeparationEnabled && !settings.FourWalletsEnabled && !settings.AutoErrorFixEnabled)
                {
                    MessageBox.Show(this, "Turn on at least one toggle (Order Separation, Auto 4W, or Auto Error Fix) before starting.", "AUTO4W", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                isRunning = true;
                UpdateStartStopVisuals();
                Log(string.Format("STARTED [Sep:{0} | 4W:{1} | ErrFix:{2}]",
                    settings.OrderSeparationEnabled ? "ON" : "OFF",
                    settings.FourWalletsEnabled ? "ON" : "OFF",
                    settings.AutoErrorFixEnabled ? "ON" : "OFF"));

                RunProcessingCycle();
                workerTimer.Start();
            }
            else
            {
                workerTimer.Stop();
                isRunning = false;
                UpdateStartStopVisuals();
                Log("STOPPED — Toggles and settings unlocked.");
            }
        }

        private void UpdateStartStopVisuals()
        {
            // Lock or unlock toggles and inputs based on running state
            toggleSeparation.Enabled = !isRunning;
            toggleAuto4W.Enabled = !isRunning;
            toggleErrorFix.Enabled = !isRunning;
            cmb4WSize.Enabled = !isRunning;

            for (int i = 0; i < 4; i++)
            {
                folderBoxes[i].ReadOnly = isRunning;
                browseButtons[i].Enabled = !isRunning;
            }

            if (isRunning)
            {
                btnStartStop.Text = "■   STOP ENGINE  (CONTROLS LOCKED)";
                btnStartStop.BackColor = Color.FromArgb(182, 58, 68); // Soft muted crimson
                lblStatusDot.ForeColor = Color.FromArgb(46, 204, 113);
                lblStatusText.Text = "Active • Watching folders (Toggles Locked)";
            }
            else
            {
                btnStartStop.Text = "▶   START ENGINE";
                btnStartStop.BackColor = Color.FromArgb(32, 134, 92); // Muted emerald green
                lblStatusDot.ForeColor = Color.FromArgb(240, 173, 78);
                lblStatusText.Text = "Stopped • Ready (Toggles Unlocked)";
            }
        }

        private void WorkerTimer_Tick(object sender, EventArgs e)
        {
            if (!isRunning || isBusyCycle) return;
            RunProcessingCycle();
        }

        private void RunProcessingCycle()
        {
            if (isBusyCycle) return;
            isBusyCycle = true;

            try
            {
                settings.PruneAndSaveLogs();

                // 1. AUTO ERROR FIX (Scans Errors folder + all subfolders)
                if (settings.AutoErrorFixEnabled)
                {
                    RunAutoErrorFix();
                }

                // 2. ORDER SEPARATION & AUTO 4W (Scans Incoming folder)
                if (settings.OrderSeparationEnabled || settings.FourWalletsEnabled)
                {
                    RunIncomingOrderProcessor();
                }
            }
            catch (Exception ex)
            {
                Log("Cycle warning: " + ex.Message);
            }
            finally
            {
                isBusyCycle = false;
            }
        }

        // ====================================================================
        // MODULE 1: AUTO ERROR FIX (All Subfolders -> Rename .err / order to .txt)
        // ====================================================================
        private void RunAutoErrorFix()
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
                    if (string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (NonOrderExtensions.Contains(ext))
                        continue;

                    if (!IsFileReady(filePath))
                        continue;

                    bool isErrExt = string.Equals(ext, ".err", StringComparison.OrdinalIgnoreCase);
                    bool looksLikeOrder = isErrExt || FileContainsOrderSignature(filePath);

                    if (!looksLikeOrder)
                        continue;

                    string targetTxtPath = Path.ChangeExtension(filePath, ".txt");
                    string origName = Path.GetFileName(filePath);
                    string newName = Path.GetFileName(targetTxtPath);

                    if (File.Exists(targetTxtPath))
                    {
                        File.Delete(targetTxtPath);
                    }

                    File.Move(filePath, targetTxtPath);
                    Log("Error Fix: " + origName + " -> " + newName);
                }
                catch (Exception ex)
                {
                    Log("Error Fix skipped " + Path.GetFileName(filePath) + ": " + ex.Message);
                }
            }
        }

        private bool FileContainsOrderSignature(string filePath)
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
        // MODULE 2 & 3: ORDER SEPARATION & AUTO 4W
        // ====================================================================
        private void RunIncomingOrderProcessor()
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
                ? "4-Wallets (vert)"
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

                    // If Order Separation is OFF, avoid re-processing the exact same file repeatedly in Incoming
                    string fileSignature = filePath + "|" + File.GetLastWriteTimeUtc(filePath).Ticks;
                    if (!settings.OrderSeparationEnabled && processedInPlaceFiles.Contains(fileSignature))
                    {
                        continue;
                    }

                    if (isDigital)
                    {
                        // 1. If Auto 4W is ON: copy order to ToPrint with Media=Glossy and Size=<chosen4WSize> (PrintName untouched)
                        if (settings.FourWalletsEnabled)
                        {
                            Directory.CreateDirectory(settings.ToprintFolder);
                            string toPrintCopy = ConvertDigitalOrderTo4WGlossy(content, chosen4WSize);
                            string destPrintFile = Path.Combine(settings.ToprintFolder, fileName);
                            WriteFileAtomically(destPrintFile, toPrintCopy);
                            Log("Auto 4W (Digital): Copied " + fileName + " -> ToPrint (" + chosen4WSize + ")");
                        }

                        // 2. If Order Separation is ON: move original Digital order to Digital folder
                        if (settings.OrderSeparationEnabled)
                        {
                            Directory.CreateDirectory(settings.DigitalFolder);
                            string destDigitalFile = Path.Combine(settings.DigitalFolder, fileName);
                            MoveFileSafely(filePath, destDigitalFile);
                            Log("Separated: " + fileName + " -> Digital");
                        }
                        else
                        {
                            processedInPlaceFiles.Add(fileSignature);
                        }
                    }
                    else if (isGlossy)
                    {
                        string finalGlossyContent = content;
                        bool added4W = false;

                        // 1. If Auto 4W is ON: append 4W block for each photo into the SAME single .txt order (only changing Size=)
                        if (settings.FourWalletsEnabled)
                        {
                            finalGlossyContent = Append4WToGlossyOrder(content, chosen4WSize, out added4W);
                        }

                        // 2. If Order Separation is ON: move the single order to ToPrint
                        if (settings.OrderSeparationEnabled)
                        {
                            Directory.CreateDirectory(settings.ToprintFolder);
                            string destPrintFile = Path.Combine(settings.ToprintFolder, fileName);

                            if (added4W)
                            {
                                WriteFileAtomically(destPrintFile, finalGlossyContent);
                                File.Delete(filePath);
                                Log("Auto 4W + Separated: " + fileName + " -> ToPrint (+4W)");
                            }
                            else
                            {
                                MoveFileSafely(filePath, destPrintFile);
                                Log("Separated: " + fileName + " -> ToPrint");
                            }
                        }
                        else if (added4W)
                        {
                            WriteFileAtomically(filePath, finalGlossyContent);
                            string updatedSig = filePath + "|" + File.GetLastWriteTimeUtc(filePath).Ticks;
                            processedInPlaceFiles.Add(updatedSig);
                            Log("Auto 4W (Glossy): Updated " + fileName + " (+4W)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log("Order error (" + Path.GetFileName(filePath) + "): " + ex.Message);
                }
            }
        }

        private string ConvertDigitalOrderTo4WGlossy(string content, string fourWSize)
        {
            // Change Media=Digital to Media=Glossy, and Size=... to Size=<fourWSize> (PrintName stays untouched)
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

        private string Append4WToGlossyOrder(string content, string fourWSize, out bool modified)
        {
            modified = false;
            string normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = normalized.Split('\n');

            // Group lines into blocks separated by blank lines
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

            // Check if 4W blocks for all photos are already present
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
                        // Keeps Qty=, PrintName=, FilePath= completely untouched
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

        private void Log(string message)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), message);
                return;
            }

            settings.AddLog(message);
            string formatted = "[" + DateTime.Now.ToString("MM-dd HH:mm:ss") + "] " + message;
            lstHistory.Items.Add(formatted);
            lstHistory.TopIndex = lstHistory.Items.Count - 1;
        }

        private void ApplyTheme()
        {
            bool dark = settings.DarkTheme;

            Color bgMain = dark ? Color.FromArgb(24, 28, 36) : Color.FromArgb(244, 246, 250);
            Color bgHeader = dark ? Color.FromArgb(18, 21, 28) : Color.FromArgb(255, 255, 255);
            Color bgCard = dark ? Color.FromArgb(34, 40, 52) : Color.FromArgb(255, 255, 255);
            Color fgPrimary = dark ? Color.FromArgb(238, 242, 248) : Color.FromArgb(30, 38, 50);
            Color fgSecondary = dark ? Color.FromArgb(156, 168, 186) : Color.FromArgb(100, 112, 128);
            Color btnBrowseBg = dark ? Color.FromArgb(48, 56, 72) : Color.FromArgb(230, 235, 242);

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

    // Custom Modern Pill Toggle Control
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
            Size = new Size(54, 26);
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

            int thumbSize = Height - 8;
            int thumbX = isChecked ? Width - thumbSize - 5 : 4;
            Rectangle thumbRect = new Rectangle(thumbX, 4, thumbSize, thumbSize);
            Color thumbColor = Enabled ? Color.White : Color.FromArgb(190, 195, 205);

            using (SolidBrush thumbBrush = new SolidBrush(thumbColor))
            {
                g.FillEllipse(thumbBrush, thumbRect);
            }
        }
    }
}
