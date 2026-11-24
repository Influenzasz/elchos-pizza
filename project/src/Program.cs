// Program.cs - Сервер корпоративной системы Elcho's Pizza Enterprise Suite
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using PizzaSystem.Data;
using PizzaSystem.Realtime;
using PizzaSystem.Server;

namespace PizzaSystem
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "Elcho's Pizza - Enterprise Management Server";

            PrintBanner();

            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string projectDir = Directory.Exists(Path.Combine(appDir, "project", "wwwroot"))
                ? Path.Combine(appDir, "project")
                : appDir;

            string dbPath = Path.Combine(projectDir, "pizza.db");
            string wwwroot = Path.Combine(projectDir, "wwwroot");
            string configPath = File.Exists(Path.Combine(appDir, "appsettings.json"))
                ? Path.Combine(appDir, "appsettings.json")
                : Path.Combine(projectDir, "appsettings.json");

            int port = 5000;
            bool autoOpen = false;
            string targetPath = "/";

            // Чтение конфигурации
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath, Encoding.UTF8);
                    var ser = new JavaScriptSerializer();
                    var cfg = ser.Deserialize<System.Collections.Generic.Dictionary<string, object>>(json);
                    if (cfg.ContainsKey("Port")) port = Convert.ToInt32(cfg["Port"]);
                    if (cfg.ContainsKey("AutoOpenBrowser")) autoOpen = Convert.ToBoolean(cfg["AutoOpenBrowser"]);
                }
                catch { }
            }

            foreach (var arg in args)
            {
                if (arg.StartsWith("--port=")) int.TryParse(arg.Substring(7), out port);
                if (arg == "--open-browser") autoOpen = true;
                if (arg == "--no-browser") autoOpen = false;
                if (arg.StartsWith("--open=")) targetPath = arg.Substring(7);
            }

            // Переменные окружения для облачных платформ (Render, Railway, Docker)
            string envPort = Environment.GetEnvironmentVariable("PORT");
            if (!string.IsNullOrEmpty(envPort))
            {
                int p;
                if (int.TryParse(envPort, out p)) port = p;
            }

            string envDb = Environment.GetEnvironmentVariable("DATABASE_PATH");
            if (!string.IsNullOrEmpty(envDb))
            {
                dbPath = envDb;
            }

            Console.WriteLine("[DB] Инициализация базы данных: " + dbPath);
            SQLiteDatabase db = null;
            try
            {
                db = new SQLiteDatabase(dbPath);
                DbInitializer.Initialize(db);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ОШИБКА БАЗЫ ДАННЫХ]: " + ex.Message);
                Console.ResetColor();
                Console.WriteLine("Нажмите Enter для выхода...");
                Console.ReadLine();
                return;
            }

            // Запуск сервера
            HttpServer server = null;
            try
            {
                server = new HttpServer(db, wwwroot, port);
                server.Start();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ОШИБКА СЕРВЕРА]: " + ex.Message);
                Console.ResetColor();
                Console.WriteLine("Нажмите Enter для выхода...");
                Console.ReadLine();
                return;
            }

            string baseUrl = "http://localhost:" + port;
            PrintRoutes(baseUrl);

            if (autoOpen)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    Thread.Sleep(600);
                    try { Process.Start(baseUrl + targetPath); } catch { }
                });
            }

            Console.WriteLine("\n[Статус] Система Elcho's Pizza активна и готова к обслуживанию клиентов.");

            // В Docker / на облачном хостинге stdin перенаправлен, поэтому ждем сигнал завершения бесконечно
            if (Console.IsInputRedirected || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PORT")))
            {
                Console.WriteLine("[Cloud] Сервер работает в непрерывном фоновом режиме.");
                var exitEvent = new ManualResetEvent(false);
                AppDomain.CurrentDomain.ProcessExit += (s, e) => exitEvent.Set();
                Console.CancelKeyPress += (s, e) => { e.Cancel = true; exitEvent.Set(); };
                exitEvent.WaitOne();
            }
            else
            {
                Console.WriteLine("Для остановки сервера закройте это окно или нажмите Enter.");
                try { Console.ReadLine(); } catch { Thread.Sleep(Timeout.Infinite); }
            }

            EventBus.Stop();
            server.Stop();
            db.Dispose();
        }

        private static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("========================================================================");
            Console.WriteLine("        ELCHO'S PIZZA — АВТОМАТИЗИРОВАННАЯ СИСТЕМА УПРАВЛЕНИЯ           ");
            Console.WriteLine("                  Enterprise POS & Restaurant Suite                     ");
            Console.WriteLine("========================================================================");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(" * Ядро системы: Высокопроизводительный сервер C# Kestrel-lite");
            Console.WriteLine(" * Реляционная СУБД: Встроенная SQLite (winsqlite3.dll)");
            Console.WriteLine(" * Синхронизация: Realtime Server-Sent Events / WebSockets");
            Console.WriteLine("========================================================================");
            Console.ResetColor();
        }

        private static void PrintRoutes(string baseUrl)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n=== АКТИВНЫЕ МОДУЛИ И РАБОЧИЕ МЕСТА СИСТЕМЫ ===");
            Console.ResetColor();
            Console.WriteLine(" 1. Сайт онлайн-заказа:         {0}/", baseUrl);
            Console.WriteLine(" 2. Кассовый терминал (POS):    {0}/pos", baseUrl);
            Console.WriteLine(" 3. Кухонный дисплей (KDS):     {0}/kitchen", baseUrl);
            Console.WriteLine(" 4. Электронное табло в зале:   {0}/board", baseUrl);
            Console.WriteLine(" 5. Терминал самообслуживания:  {0}/kiosk", baseUrl);
            Console.WriteLine(" 6. Курьерский модуль:          {0}/courier", baseUrl);
            Console.WriteLine(" 7. Панель управления и склад:  {0}/admin", baseUrl);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("------------------------------------------------------------------------");
            Console.WriteLine(" Корпоративные аккаунты: admin@elchos.ru / cashier@elchos.ru / chef@elchos.ru");
            Console.WriteLine(" Пароль по умолчанию: admin123, cashier123, chef123");
            Console.ResetColor();
        }
    }
}
