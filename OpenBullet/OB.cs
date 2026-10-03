using OpenBulletCE.ViewModels;
using PluginFramework;
using RuriLib;
using RuriLib.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace OpenBulletCE
{
    public static class OB
    {
        public static IApplication App => new OpenBulletApp()
        {
            RunnerManager = RunnerManager,
            ProxyManager = ProxyManager,
            ProxyChecker = ProxyManager,
            WordlistManager = WordlistManager,
            CookieManager = CookieManager,
            ConfigManager = ConfigManager,
            HitsDB = HitsDB,
            Settings = Settings,
            Logger = Logger,
            Alerter = Alerter
        };

        public static string Version => "1.3.1 by darwnlinz";

        // Block Mappings (including Plugins)
        public static List<(Type, Type, Color)> BlockMappings = new List<(Type, Type, Color)>();

        // Block Plugins
        // HACK: Find a better way to do this and a better place to put them
        public static List<IBlockPlugin> BlockPlugins;
        public static IEnumerable<BlockBase> BlockPluginsAsBase => BlockPlugins.Cast<BlockBase>();

        // Windows
        // TODO: Remove these from here, everything should only depend on the ViewModels not on the Views!
        public static MainWindow MainWindow { get; set; }
        public static LogWindow LogWindow { get; set; }

        // ViewModels
        public static RunnerManagerViewModel RunnerManager { get; set; }
        public static ProxyManagerViewModel ProxyManager { get; set; }
        public static WordlistManagerViewModel WordlistManager { get; set; }
        public static CookieManagerViewModel CookieManager { get; set; }
        public static ConfigManagerViewModel ConfigManager { get; set; }
        public static StackerViewModel Stacker { get; set; }
        public static HitsDBViewModel HitsDB { get; set; }
        public static Alerter Alerter { get; set; } = new Alerter();
        public static LoggerViewModel Logger { get; set; } = new LoggerViewModel();
        public static GlobalSettings Settings { get; set; } = new GlobalSettings();
        public static OBSettingsViewModel OBSettings { get; set; }

        // Constant file paths (Absolute paths anchored to AppDomain.CurrentDomain.BaseDirectory)
        public static readonly string dataBaseFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DB");
        public static readonly string dataBaseFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DB", "OpenBullet.db");
        public static readonly string dataBaseBackupFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DB", "OpenBullet-BackupCopy.db");
        public static readonly string dataBaseHitsFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DB", "Hits.db");
        public static readonly string dataBaseProxiesFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DB", "Proxies.db");
        public static readonly string dataBaseCookiesFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DB", "Cookies.db");
        public static readonly string dataBaseWordlistsFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DB", "Wordlists.db");
        public static readonly string dataBaseRecordsFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DB", "Records.db");
        public static readonly string cookieIndexFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "CookieIndex");
        public static readonly string obSettingsFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings", "OBSettings.json");
        public static readonly string rlSettingsFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings", "RLSettings.json");
        public static readonly string proxyManagerSettingsFile = @"Settings/ProxyManagerSettings.json";
        public static readonly string envFile = @"Settings/Environment.ini";
        public static readonly string licenseFile = @"Settings/License.txt";
        public static readonly string logFile = @"Log.txt";
        public static readonly string configFolder = @"Configs";
        public static readonly string pluginsFolder = @"Plugins";
        public static readonly string defaultProxySiteUrl = "https://google.com";
        public static readonly string defaultProxyKey = "title>Google";
    }
}
