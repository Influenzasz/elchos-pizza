using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

namespace PizzaSystem.Launchers
{
    class LocalLauncher
    {
        static void Main(string[] args)
        {
            Console.Title = "Elcho's Pizza — Запуск локального сервера";
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("========================================================================");
            Console.WriteLine("             ELCHO'S PIZZA — ЛОКАЛЬНЫЙ СЕРВЕР И СИСТЕМА                 ");
            Console.WriteLine("========================================================================");
            Console.ResetColor();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string serverExe = Path.Combine(baseDir, "Server.exe");
            if (!File.Exists(serverExe)) serverExe = Path.Combine(baseDir, "project", "Server.exe");

            if (!IsServerAlive())
            {
                if (File.Exists(serverExe))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[*] Запуск встроенного сервера Elcho's Pizza...");
                    Console.ResetColor();

                    var psi = new ProcessStartInfo
                    {
                        FileName = serverExe,
                        Arguments = "--no-browser",
                        WorkingDirectory = Path.GetDirectoryName(serverExe),
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal
                    };
                    Process.Start(psi);

                    for (int i = 0; i < 30; i++)
                    {
                        Thread.Sleep(200);
                        if (IsServerAlive()) break;
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[-] Ошибка: Исполняемый файл сервера Server.exe не найден!");
                    Console.ResetColor();
                    Console.WriteLine("Нажмите любую клавишу для выхода...");
                    Console.ReadKey();
                    return;
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[+] Сервер успешно запущен: http://localhost:5000");
            Console.WriteLine("[*] Открытие системы в браузере...");
            Console.ResetColor();

            try
            {
                Process.Start("http://localhost:5000");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Не удалось открыть браузер автоматически: " + ex.Message);
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Доступные модули системы:");
            Console.WriteLine("  • Сайт пиццерии:        http://localhost:5000/");
            Console.WriteLine("  • Кассовый терминал POS: http://localhost:5000/pos");
            Console.WriteLine("  • Кухонный дисплей KDS: http://localhost:5000/kitchen");
            Console.WriteLine("  • Табло очереди:        http://localhost:5000/board");
            Console.WriteLine("  • Киоск самозаказа:     http://localhost:5000/kiosk");
            Console.WriteLine("  • Курьерская доставка:  http://localhost:5000/courier");
            Console.WriteLine("  • Управление и склад:   http://localhost:5000/admin");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Окно закроется автоматически через 5 секунд...");

            int seconds = 5;
            while (seconds > 0)
            {
                try { if (!Console.IsInputRedirected && Console.KeyAvailable) break; } catch { }
                Thread.Sleep(1000);
                seconds--;
            }
        }

        static bool IsServerAlive()
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
    }
}
