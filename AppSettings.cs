using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        private const int MaxLogEntries = 400;

        public string IncomingFolder { get; set; } = @"C:\AUTO4W\Incoming";
        public string ToprintFolder { get; set; } = @"C:\AUTO4W\ToPrint";
        public string DigitalFolder { get; set; } = @"C:\AUTO4W\Digital";
        public string ErrorsFolder { get; set; } = @"C:\AUTO4W\Errors";

        public bool OrderSeparationEnabled { get; set; } = true;
        public bool FourWalletsEnabled { get; set; } = true;
        public bool AutoErrorFixEnabled { get; set; } = false;

        public string Selected4WSize { get; set; } = "4-Wallets (vert)";
        public bool DarkTheme { get; set; } = true;

        public List<LogEntry> HistoryLogs { get; set; } = new List<LogEntry>();

        public static AppSettings Load()
        {
            var settings = new AppSettings();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key == null) return settings;

                    settings.IncomingFolder = Convert.ToString(key.GetValue("IncomingFolder", settings.IncomingFolder));
                    settings.ToprintFolder = Convert.ToString(key.GetValue("ToprintFolder", settings.ToprintFolder));
                    settings.DigitalFolder = Convert.ToString(key.GetValue("DigitalFolder", settings.DigitalFolder));
                    settings.ErrorsFolder = Convert.ToString(key.GetValue("ErrorsFolder", settings.ErrorsFolder));

                    settings.OrderSeparationEnabled = Convert.ToInt32(key.GetValue("OrderSeparationEnabled", 1)) == 1;
                    settings.FourWalletsEnabled = Convert.ToInt32(key.GetValue("FourWalletsEnabled", 1)) == 1;
                    settings.AutoErrorFixEnabled = Convert.ToInt32(key.GetValue("AutoErrorFixEnabled", 0)) == 1;

                    settings.Selected4WSize = Convert.ToString(key.GetValue("Selected4WSize", settings.Selected4WSize));
                    settings.DarkTheme = Convert.ToInt32(key.GetValue("DarkTheme", 1)) == 1;

                    string[] rawLogs = key.GetValue("HistoryLogs") as string[];
                    if (rawLogs != null)
                    {
                        DateTime cutoff = DateTime.Now.AddDays(-MaxLogDays);
                        foreach (string raw in rawLogs)
                        {
                            int sepIndex = raw.IndexOf('|');
                            if (sepIndex > 0)
                            {
                                string datePart = raw.Substring(0, sepIndex);
                                string msgPart = raw.Substring(sepIndex + 1);
                                DateTime parsedTime;
                                if (DateTime.TryParseExact(datePart, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsedTime))
                                {
                                    if (parsedTime >= cutoff)
                                    {
                                        settings.HistoryLogs.Add(new LogEntry { Timestamp = parsedTime, Message = msgPart });
                                    }
                                }
                            }
                        }
                    }
                }
                settings.PruneAndSaveLogs();
            }
            catch
            {
            }
            return settings;
        }

        public void Save()
        {
            try
            {
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

                    key.SetValue("Selected4WSize", Selected4WSize ?? "4-Wallets (vert)");
                    key.SetValue("DarkTheme", DarkTheme ? 1 : 0, RegistryValueKind.DWord);
                }
                PruneAndSaveLogs();
            }
            catch
            {
            }
        }

        public void AddLog(string message)
        {
            HistoryLogs.Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Message = message
            });
            PruneAndSaveLogs();
        }

        public void ClearLogs()
        {
            HistoryLogs.Clear();
            PruneAndSaveLogs();
        }

        public void PruneAndSaveLogs()
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
                    string[] serialized = HistoryLogs
                        .Select(l => l.Timestamp.ToString("O", CultureInfo.InvariantCulture) + "|" + l.Message)
                        .ToArray();
                    key.SetValue("HistoryLogs", serialized, RegistryValueKind.MultiString);
                }
            }
            catch
            {
            }
        }
    }
}
