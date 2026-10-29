using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace AUTO4W
{
    public class ScheduleGroup
    {
        public string Name { get; set; } = "Schedule";
        public bool Enabled { get; set; } = false;
        public string StartTime { get; set; } = "09:00";
        public string EndTime { get; set; } = "17:00";
        public string Days { get; set; } = "Mon,Tue,Wed,Thu,Fri";
    }

    public class AppSettings
    {
        public string IncomingFolder { get; set; } = @"C:\AUTO4W\Incoming";
        public string ToprintFolder { get; set; } = @"C:\AUTO4W\Toprint";
        public string DigitalFolder { get; set; } = @"C:\AUTO4W\Digital";
        public string ErrorsFolder { get; set; } = @"C:\AUTO4W\Errors";
        public bool FourWalletsEnabled { get; set; } = true;
        public bool AutoErrorFixEnabled { get; set; } = true;
        public bool OrderSeparationEnabled { get; set; } = true;
        public bool RunAtStartup { get; set; } = false;
        public bool DarkTheme { get; set; } = false;
        public string AccentColor { get; set; } = "Blue";
        public string OldExtension { get; set; } = ".old";
        public string NewExtension { get; set; } = ".jpg";
        public string PhotoSize { get; set; } = "Wallet";
        public string PhotoLayout { get; set; } = "4 per page";
        public bool ConfirmBeforeProcessing { get; set; } = true;
        public List<ScheduleGroup> Schedules { get; set; } = CreateSchedules();

        private static List<ScheduleGroup> CreateSchedules()
        {
            var list = new List<ScheduleGroup>();
            for (int i = 1; i <= 6; i++)
                list.Add(new ScheduleGroup { Name = "Schedule " + i, StartTime = "09:00", EndTime = "17:00" });
            return list;
        }

        public static string SettingsDirectory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AUTO4W"); }
        }

        public static string SettingsPath { get { return Path.Combine(SettingsDirectory, "settings.json"); } }

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return new AppSettings();
                var serializer = new JavaScriptSerializer();
                var loaded = serializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                if (loaded == null) return new AppSettings();
                if (loaded.Schedules == null || loaded.Schedules.Count != 6) loaded.Schedules = CreateSchedules();
                return loaded;
            }
            catch { return new AppSettings(); }
        }

        public void Save()
        {
            Directory.CreateDirectory(SettingsDirectory);
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            File.WriteAllText(SettingsPath, serializer.Serialize(this));
        }
    }
}
