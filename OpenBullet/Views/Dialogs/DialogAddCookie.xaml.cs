using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using Ookii.Dialogs.Wpf;
using OpenBulletCE.Views.Main;
using RuriLib.Models;

namespace OpenBulletCE.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for DialogAddCookie.xaml
    /// </summary>
    public partial class DialogAddCookie : Page
    {
        public object Caller { get; set; }

        public DialogAddCookie(object caller)
        {
            InitializeComponent();

            Caller = caller;

        }

        private async void acceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (Caller.GetType() == typeof(CookieManager))
            {
                if (nameTextbox.Text.Trim() == string.Empty) { System.Windows.Forms.MessageBox.Show("The name cannot be blank"); return; }

                var path = locationTextbox.Text;
                var cookie = new Cookie(nameTextbox.Text, path, false);
                var cookieManager = (CookieManager)Caller;
                cookieManager.AddCookie(cookie);

                ((MainDialog)Parent).Close();

                // Scan in background thread so UI never freezes
                await Task.Run(() =>
                {
                    try
                    {
                        var files = Cookie.FastScanCookieFiles(path);
                        cookie.PathAllCookiesFolders = files;
                        cookie.TotalCookiesFolders = files.Count;
                        cookie.SavePathsToIndex(files);
                        OB.CookieManager.Update(cookie);
                        OB.Logger.LogInfo(Components.CookieManager, $"Completed scanning {files.Count} cookies for '{cookie.Name}'");
                    }
                    catch (Exception ex)
                    {
                        OB.Logger.LogError(Components.CookieManager, $"Failed scanning cookies for '{cookie.Name}': {ex.Message}");
                    }
                });
                return;
            }
            ((MainDialog)Parent).Close();
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            VistaFolderBrowserDialog folderBrowserDialog = new VistaFolderBrowserDialog();

            folderBrowserDialog.Description = "Select path to you LOGS\r\nУкажите путь к вашим логам.";
            folderBrowserDialog.RootFolder = Environment.SpecialFolder.Desktop;

            bool? show = folderBrowserDialog.ShowDialog();
            if (show != null && show == true)
            {
                locationTextbox.Text = folderBrowserDialog.SelectedPath;
                nameTextbox.Text = System.IO.Path.GetFileNameWithoutExtension(folderBrowserDialog.SelectedPath);
            }


        }
    }
}
