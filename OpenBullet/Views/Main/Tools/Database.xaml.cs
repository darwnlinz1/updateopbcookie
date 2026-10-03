using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OpenBulletCE.Views.Main.Tools
{
    /// <summary>
    /// Logica di interazione per Database.xaml
    /// </summary>
    public partial class Database : Page
    {
        public Database()
        {
            InitializeComponent();
        }

        private void shrinkButton_Click(object sender, RoutedEventArgs e)
        {
            if (OB.RunnerManager.RunnersCollection.Any(r => r.ViewModel.Master.IsBusy))
            {
                OB.Logger.LogWarning(Components.Database, "Please stop all active runners before shrinking the database!", true);
                return;
            }

            try
            {
                var dbFiles = new[]
                {
                    OB.dataBaseHitsFile,
                    OB.dataBaseProxiesFile,
                    OB.dataBaseCookiesFile,
                    OB.dataBaseWordlistsFile,
                    OB.dataBaseRecordsFile
                };

                long totalPrevious = 0;
                long totalNew = 0;

                foreach (var file in dbFiles)
                {
                    if (File.Exists(file))
                    {
                        var prev = new FileInfo(file).Length;
                        totalPrevious += prev;
                        var conn = Repositories.LiteDBRepository<RuriLib.Models.Hit>.GetConnection(file);
                        lock (conn.LockObj)
                        {
                            conn.Db.Shrink();
                        }
                        var now = new FileInfo(file).Length;
                        totalNew += now;
                    }
                }

                int prevKb = (int)(totalPrevious / 1024);
                int newKb = (int)(totalNew / 1024);
                OB.Logger.LogInfo(Components.Database, $"All databases successfully shrinked from {prevKb} KB to {newKb} KB (Saved {prevKb - newKb} KB)", true);
            }
            catch (Exception ex)
            {
                OB.Logger.LogError(Components.Database, $"Shrink failed! Error: {ex.Message}");
            }
        }
    }
}
