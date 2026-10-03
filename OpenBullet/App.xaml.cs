using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace OpenBulletCE
{
    /// <summary>
    /// Logica di interazione per App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                try
                {
                    string assemblyName = new System.Reflection.AssemblyName(args.Name).Name;
                    string binPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", assemblyName + ".dll");
                    if (File.Exists(binPath))
                    {
                        return System.Reflection.Assembly.LoadFrom(binPath);
                    }

                    string rootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName + ".dll");
                    if (File.Exists(rootPath))
                    {
                        return System.Reflection.Assembly.LoadFrom(rootPath);
                    }
                }
                catch { }
                return null;
            };
        }

        public App()
        {
            // Define how to handle unhandled exception
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                OnUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            Dispatcher.UnhandledException += (s, e) =>
            {
                OnUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");
                e.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                OnUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
                try { e.SetObserved(); } catch { }
            };
        }

        public void OnUnhandledException(Exception ex, string @event)
        {
            File.AppendAllText(OB.logFile, $"[FATAL][{@event}] UHANDLED EXCEPTION{Environment.NewLine}{ex.ToString()}");
        }
    }
}
