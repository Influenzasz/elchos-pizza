// LauncherApp.cs - Лаунчер десктопных модулей Elcho's Pizza
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

namespace PizzaSystem.Launchers
{
    class LauncherApp
    {
        static void Main(string[] args)
        {
            string targetPath = "/";
            string title = "Elcho's Pizza";
            int width = 1280;
            int height = 800;
            bool isWebsite = false;

#if CASHIER
            targetPath = "/pos";
            title = "Elcho's Pizza — Кассовый терминал POS";
            width = 1280;
            height = 850;
#elif KITCHEN
            targetPath = "/kitchen";
            title = "Elcho's Pizza — Кухонный дисплей KDS";
            width = 1366;
            height = 768;
#elif BOARD
            targetPath = "/board";
            title = "Elcho's Pizza — Табло готовности заказов";
            width = 1024;
            height = 768;
#elif KIOSK
            targetPath = "/kiosk";
            title = "Elcho's Pizza — Киоск самообслуживания";
            width = 960;
            height = 1080;
#elif COURIER
            targetPath = "/courier";
            title = "Elcho's Pizza — Модуль курьерской доставки";
            width = 440;
            height = 860;
#elif ADMIN
            targetPath = "/admin";
            title = "Elcho's Pizza — Панель управления и склад";
            width = 1440;
            height = 900;
#elif WEBSITE
            targetPath = "/";
            title = "Elcho's Pizza — Официальный сайт онлайн-заказа";
            isWebsite = true;
#else
            targetPath = "/";
            title = "Elcho's Pizza";
            isWebsite = true;
#endif

            if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
            {
                targetPath = args[0];
            }

            // 1. Проверяем и запускаем сервер, если он еще не запущен
            EnsureServerRunning();

            string url = "http://localhost:5000" + targetPath;

            // 2. Открытие
            if (isWebsite)
            {
                // Сайт открывается в браузере пользователя по умолчанию
                try { Process.Start(url); } catch { }
            }
            else
            {
                // Рабочие места открываются в отдельном десктопном окне программы (--app=...)
                OpenAsDesktopApp(url, title, width, height);
            }
        }

        private static void EnsureServerRunning()
        {
            if (IsServerAlive()) return;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string serverExe = Path.Combine(baseDir, "Server.exe");
            if (!File.Exists(serverExe))
            {
                serverExe = Path.Combine(baseDir, "Сервер.exe");
            }
            if (!File.Exists(serverExe))
            {
                serverExe = Path.Combine(baseDir, "project", "Server.exe");
            }
            if (!File.Exists(serverExe))
            {
                serverExe = Path.Combine(baseDir, "project", "PizzaSystem.exe");
            }
            if (!File.Exists(serverExe))
            {
                serverExe = Path.Combine(baseDir, "PizzaSystem.exe");
            }
            if (!File.Exists(serverExe))
            {
                serverExe = Path.Combine(baseDir, "..", "Server.exe");
            }
            if (!File.Exists(serverExe))
            {
                serverExe = Path.Combine(baseDir, "..", "Сервер.exe");
            }
            if (!File.Exists(serverExe))
            {
                serverExe = Path.Combine(baseDir, "..", "project", "Server.exe");
            }

            if (File.Exists(serverExe))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = serverExe,
                    Arguments = "--no-browser",
                    WorkingDirectory = Path.GetDirectoryName(serverExe),
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                };
                Process.Start(psi);

                for (int i = 0; i < 25; i++)
                {
                    Thread.Sleep(200);
                    if (IsServerAlive()) break;
                }
            }
        }

        private static bool IsServerAlive()
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://localhost:5000/api/categories");
                req.Timeout = 800;
                req.Method = "GET";
                using (var res = (HttpWebResponse)req.GetResponse())
                {
                    return res.StatusCode == HttpStatusCode.OK;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void OpenAsDesktopApp(string url, string title, int width, int height)
        {
            string browserExe = FindChromiumExecutable();

            if (!string.IsNullOrEmpty(browserExe))
            {
                // Запуск в режиме отдельного автономного окна Windows без адресной строки и вкладок
                string appDataDir = Path.Combine(Path.GetTempPath(), "ElchosApp_" + title.GetHashCode().ToString("X"));
                string args = string.Format("--app=\"{0}\" --window-size={1},{2} --user-data-dir=\"{3}\"", url, width, height, appDataDir);

                try
                {
                    Process.Start(browserExe, args);
                    return;
                }
                catch { }
            }

            // Fallback: стандартный запуск
            try
            {
                Process.Start(url);
            }
            catch { }
        }

        private static string FindChromiumExecutable()
        {
            string[] directPaths = {
                @"C:\Program Files\imput\Helium\Application\chrome.exe",
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe")
            };

            foreach (var p in directPaths)
            {
                if (File.Exists(p)) return p;
            }

            // Поиск в каталогах EdgeCore
            string edgeCore = @"C:\Program Files (x86)\Microsoft\EdgeCore";
            if (Directory.Exists(edgeCore))
            {
                try
                {
                    var files = Directory.GetFiles(edgeCore, "msedge.exe", SearchOption.AllDirectories);
                    if (files.Length > 0) return files[0];
                }
                catch { }
            }

            return null;
        }
    }
}
