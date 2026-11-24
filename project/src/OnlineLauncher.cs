using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace PizzaSystem.Launchers
{
    class OnlineLauncher
    {
        static void Main(string[] args)
        {
            Console.Title = "Elcho's Pizza — Запуск онлайн сервера";
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("========================================================================");
            Console.WriteLine("           ELCHO'S PIZZA — ОБЛАЧНЫЙ СЕРВЕР (ONLINE HOSTING)             ");
            Console.WriteLine("========================================================================");
            Console.ResetColor();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string urlFile = Path.Combine(baseDir, "online_url.txt");
            string onlineUrl = "https://elchos-pizza.onrender.com";

            if (File.Exists(urlFile))
            {
                try
                {
                    string[] lines = File.ReadAllLines(urlFile);
                    foreach (string l in lines)
                    {
                        string trimmed = l.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                        {
                            onlineUrl = trimmed;
                            break;
                        }
                    }
                }
                catch { }
            }
            else
            {
                try
                {
                    File.WriteAllText(urlFile, onlineUrl + Environment.NewLine + 
                        "# Укажите здесь ваш точный адрес с панели управления Render.com или Railway.app, если он отличается.");
                }
                catch { }
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[*] Облачный адрес сервера: " + onlineUrl);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[*] Открытие онлайн-пиццерии в браузере...");
            Console.ResetColor();

            try
            {
                Process.Start(onlineUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Не удалось открыть браузер автоматически: " + ex.Message);
            }

            string cleanUrl = onlineUrl.TrimEnd('/');
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Онлайн-модули предприятия:");
            Console.WriteLine("  • Клиентский сайт:      " + cleanUrl + "/");
            Console.WriteLine("  • Кассовый терминал:    " + cleanUrl + "/pos");
            Console.WriteLine("  • Кухонный дисплей:     " + cleanUrl + "/kitchen");
            Console.WriteLine("  • Табло очереди:        " + cleanUrl + "/board");
            Console.WriteLine("  • Киоск самозаказа:     " + cleanUrl + "/kiosk");
            Console.WriteLine("  • Курьерская доставка:  " + cleanUrl + "/courier");
            Console.WriteLine("  • Управление и склад:   " + cleanUrl + "/admin");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Совет: изменить адрес можно в текстовом файле online_url.txt");
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
    }
}
