using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace AUTO4W
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            return "[" + Timestamp.ToString("MM-dd HH:mm:ss") + "] " + Message;
        }
    }

    public class AppSettings
    {
        private const string RegistryPath = @"Software\AUTO4W";
        private const int MaxLogDays = 7;
        private const int MaxLogEntries = 350;

        public static readonly string[] Default4WSizes = new string[]
        {
            "4-Wallets Vert",
            "4-2.5x3.5 Wallets",
            "4-2.5x3.5 Wallets (8x10)",
            "4-2.5x3.5 Wallets (5x7)"
        };

        private readonly object syncLock = new object();

        public string IncomingFolder { get; set; } = @"C:\AUTO4W\Incoming";
        public string ToprintFolder { get; set; } = @"C:\AUTO4W\ToPrint";
        public string DigitalFolder { get; set; } = @"C:\AUTO4W\Digital";
        public string ErrorsFolder { get; set; } = @"C:\AUTO4W\Errors";

        public bool OrderSeparationEnabled { get; set; } = true;
        public bool FourWalletsEnabled { get; set; } = true;
        public bool AutoErrorFixEnabled { get; set; } = false;

        public string Selected4WSize { get; set; } = "4-Wallets Vert";
        public List<string> Custom4WSizes { get; set; } = new List<string>();
        public bool DarkTheme { get; set; } = true;

        // Auto 4W Schedule Settings (Controls Auto 4W only)
        public bool Schedule4WEnabled { get; set; } = false;
        public string Schedule4WStart { get; set; } = "08:00";
        public string Schedule4WEnd { get; set; } = "17:00";
        public string Schedule4WDays { get; set; } = "1,1,1,1,1,1,1"; // Mon..Sun
        public bool Schedule4WUseDate { get; set; } = false;
        public string Schedule4WDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        public List<LogEntry> HistoryLogs { get; set; } = new List<LogEntry>();

        public static AppSettings Load()
        {
            var s = new AppSettings();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key == null) return s;

                    s.IncomingFolder = Convert.ToString(key.GetValue("IncomingFolder", s.IncomingFolder));
                    s.ToprintFolder = Convert.ToString(key.GetValue("ToprintFolder", s.ToprintFolder));
                    s.DigitalFolder = Convert.ToString(key.GetValue("DigitalFolder", s.DigitalFolder));
                    s.ErrorsFolder = Convert.ToString(key.GetValue("ErrorsFolder", s.ErrorsFolder));

                    s.OrderSeparationEnabled = Convert.ToInt32(key.GetValue("OrderSeparationEnabled", 1)) == 1;
                    s.FourWalletsEnabled = Convert.ToInt32(key.GetValue("FourWalletsEnabled", 1)) == 1;
                    s.AutoErrorFixEnabled = Convert.ToInt32(key.GetValue("AutoErrorFixEnabled", 0)) == 1;

                    s.Selected4WSize = Convert.ToString(key.GetValue("Selected4WSize", s.Selected4WSize));
                    s.DarkTheme = Convert.ToInt32(key.GetValue("DarkTheme", 1)) == 1;

                    s.Schedule4WEnabled = Convert.ToInt32(key.GetValue("Schedule4WEnabled", 0)) == 1;
                    s.Schedule4WStart = Convert.ToString(key.GetValue("Schedule4WStart", s.Schedule4WStart));
                    s.Schedule4WEnd = Convert.ToString(key.GetValue("Schedule4WEnd", s.Schedule4WEnd));
                    s.Schedule4WDays = Convert.ToString(key.GetValue("Schedule4WDays", s.Schedule4WDays));
                    s.Schedule4WUseDate = Convert.ToInt32(key.GetValue("Schedule4WUseDate", 0)) == 1;
                    s.Schedule4WDate = Convert.ToString(key.GetValue("Schedule4WDate", s.Schedule4WDate));

                    string[] customSizes = key.GetValue("Custom4WSizes") as string[];
                    if (customSizes != null)
                    {
                        foreach (string sz in customSizes)
                        {
                            if (!string.IsNullOrWhiteSpace(sz) &&
                                !Default4WSizes.Contains(sz, StringComparer.OrdinalIgnoreCase) &&
                                !s.Custom4WSizes.Contains(sz, StringComparer.OrdinalIgnoreCase))
                            {
                                s.Custom4WSizes.Add(sz.Trim());
                            }
                        }
                    }

                    string[] rawLogs = key.GetValue("HistoryLogs") as string[];
                    if (rawLogs != null)
                    {
                        DateTime cutoff = DateTime.Now.AddDays(-MaxLogDays);
                        foreach (string raw in rawLogs)
                        {
                            int idx = raw.IndexOf('|');
                            if (idx > 0)
                            {
                                DateTime dt;
                                if (DateTime.TryParseExact(raw.Substring(0, idx), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt))
                                {
                                    if (dt >= cutoff)
                                    {
                                        s.HistoryLogs.Add(new LogEntry { Timestamp = dt, Message = raw.Substring(idx + 1) });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return s;
        }

        public void SaveAsync()
        {
            Task.Run(() => Save());
        }

        public void Save()
        {
            lock (syncLock)
            {
                try
                {
                    DateTime cutoff = DateTime.Now.AddDays(-MaxLogDays);
                    HistoryLogs = HistoryLogs
                        .Where(l => l.Timestamp >= cutoff)
                        .OrderBy(l => l.Timestamp)
                        .ToList();

                    if (HistoryLogs.Count > MaxLogEntries)
                    {
                        HistoryLogs = HistoryLogs.Skip(HistoryLogs.Count - MaxLogEntries).ToList();
                    }

                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                    {
                        if (key == null) return;

                        key.SetValue("IncomingFolder", IncomingFolder ?? "");
                        key.SetValue("ToprintFolder", ToprintFolder ?? "");
                        key.SetValue("DigitalFolder", DigitalFolder ?? "");
                        key.SetValue("ErrorsFolder", ErrorsFolder ?? "");

                        key.SetValue("OrderSeparationEnabled", OrderSeparationEnabled ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("FourWalletsEnabled", FourWalletsEnabled ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("AutoErrorFixEnabled", AutoErrorFixEnabled ? 1 : 0, RegistryValueKind.DWord);

                        key.SetValue("Selected4WSize", Selected4WSize ?? "4-Wallets Vert");
                        key.SetValue("Custom4WSizes", Custom4WSizes.ToArray(), RegistryValueKind.MultiString);
                        key.SetValue("DarkTheme", DarkTheme ? 1 : 0, RegistryValueKind.DWord);

                        key.SetValue("Schedule4WEnabled", Schedule4WEnabled ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("Schedule4WStart", Schedule4WStart ?? "08:00");
                        key.SetValue("Schedule4WEnd", Schedule4WEnd ?? "17:00");
                        key.SetValue("Schedule4WDays", Schedule4WDays ?? "1,1,1,1,1,1,1");
                        key.SetValue("Schedule4WUseDate", Schedule4WUseDate ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("Schedule4WDate", Schedule4WDate ?? DateTime.Now.ToString("yyyy-MM-dd"));

                        string[] serializedLogs = HistoryLogs
                            .Select(l => l.Timestamp.ToString("O", CultureInfo.InvariantCulture) + "|" + l.Message)
                            .ToArray();
                        key.SetValue("HistoryLogs", serializedLogs, RegistryValueKind.MultiString);
                    }
                }
                catch
                {
                }
            }
        }

        public LogEntry AddLog(string message)
        {
            var entry = new LogEntry { Timestamp = DateTime.Now, Message = message };
            lock (syncLock)
            {
                HistoryLogs.Add(entry);
            }
            SaveAsync();
            return entry;
        }

        public void ClearLogs()
        {
            lock (syncLock)
            {
                HistoryLogs.Clear();
            }
            SaveAsync();
        }
    }
}
