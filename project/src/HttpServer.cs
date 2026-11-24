// HttpServer.cs - Автономный HTTP веб-сервер Kestrel-lite на чистых TCP сокетах
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using PizzaSystem.Api;
using PizzaSystem.Data;
using PizzaSystem.Realtime;

namespace PizzaSystem.Server
{
    public class SimpleHttpRequest
    {
        public string Method { get; set; }
        public string RawUrl { get; set; }
        public string Path { get; set; }
        public Dictionary<string, string> Query { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string Body { get; set; }

        public SimpleHttpRequest()
        {
            Query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Body = "";
        }
    }

    public class HttpServer
    {
        private readonly TcpListener _listener;
        private readonly ApiHandler _api;
        private readonly string _wwwroot;
        private readonly int _port;
        private bool _isRunning;
        private static readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        private static readonly Dictionary<string, string> _mimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".html", "text/html; charset=utf-8" },
            { ".htm", "text/html; charset=utf-8" },
            { ".css", "text/css; charset=utf-8" },
            { ".js", "application/javascript; charset=utf-8" },
            { ".json", "application/json; charset=utf-8" },
            { ".png", "image/png" },
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".gif", "image/gif" },
            { ".svg", "image/svg+xml" },
            { ".ico", "image/x-icon" },
            { ".webp", "image/webp" },
            { ".avif", "image/avif" },
            { ".woff", "font/woff" },
            { ".woff2", "font/woff2" },
            { ".ttf", "font/ttf" },
            { ".mp3", "audio/mpeg" },
            { ".wav", "audio/wav" }
        };

        public HttpServer(SQLiteDatabase db, string wwwroot, int port)
        {
            _wwwroot = Path.GetFullPath(wwwroot);
            _port = port;
            _api = new ApiHandler(db);
            _listener = new TcpListener(IPAddress.Any, _port);
        }

        public void Start()
        {
            _listener.Start();
            _isRunning = true;
            Console.WriteLine("[Server] Сервер Kestrel-Portable успешно запущен на порту " + _port);
            Console.WriteLine("[Server] URL системы: http://localhost:" + _port);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (_isRunning)
                {
                    try
                    {
                        var client = _listener.AcceptTcpClient();
                        ThreadPool.QueueUserWorkItem(ProcessClient, client);
                    }
                    catch (SocketException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[Server] Ошибка акцепта клиента: " + ex.Message);
                    }
                }
            });
        }

        private void ProcessClient(object state)
        {
            var client = (TcpClient)state;
            try
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                var req = ParseRequest(stream);
                if (req == null)
                {
                    client.Close();
                    return;
                }

                // 1. SSE подключение
                if (req.Path.Equals("/api/events", StringComparison.OrdinalIgnoreCase))
                {
                    EventBus.Subscribe(client, stream);
                    return; // Не закрываем соединение!
                }

                // 2. REST API запросы
                if (req.Path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                {
                    HandleApi(stream, req);
                    client.Close();
                    return;
                }

                // 3. Статические файлы и SPA роуты
                HandleStaticFile(stream, req);
            }
            catch
            {
            }
            finally
            {
                // Для обычных запросов закрываем сокет
                try
                {
                    if (client.Connected) client.Close();
                }
                catch { }
            }
        }

        private SimpleHttpRequest ParseRequest(NetworkStream stream)
        {
            var req = new SimpleHttpRequest();
            var memory = new MemoryStream();
            byte[] buffer = new byte[4096];
            int totalRead = 0;
            int headerEnd = -1;

            // Читаем заголовки
            while (true)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0) return null;
                memory.Write(buffer, 0, read);
                totalRead += read;

                byte[] current = memory.ToArray();
                headerEnd = FindDoubleCrlf(current);
                if (headerEnd != -1) break;
                if (totalRead > 65536) return null; // Слишком большой заголовок
            }

            byte[] fullBytes = memory.ToArray();
            string headerStr = Encoding.UTF8.GetString(fullBytes, 0, headerEnd);
            string[] lines = headerStr.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0) return null;

            // Первая строка: GET /path?query HTTP/1.1
            string[] firstTokens = lines[0].Split(' ');
            if (firstTokens.Length < 2) return null;
            req.Method = firstTokens[0].ToUpperInvariant();
            req.RawUrl = firstTokens[1];

            // Разделение пути и query
            int qIndex = req.RawUrl.IndexOf('?');
            if (qIndex >= 0)
            {
                req.Path = req.RawUrl.Substring(0, qIndex);
                string qStr = req.RawUrl.Substring(qIndex + 1);
                string[] qParts = qStr.Split('&');
                foreach (var qp in qParts)
                {
                    string[] kv = qp.Split('=');
                    if (kv.Length == 2)
                    {
                        req.Query[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
                    }
                    else if (kv.Length == 1)
                    {
                        req.Query[Uri.UnescapeDataString(kv[0])] = "";
                    }
                }
            }
            else
            {
                req.Path = req.RawUrl;
            }

            // Заголовки
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon > 0)
                {
                    string k = lines[i].Substring(0, colon).Trim();
                    string v = lines[i].Substring(colon + 1).Trim();
                    req.Headers[k] = v;
                }
            }

            // Тело запроса
            int contentLength = 0;
            if (req.Headers.ContainsKey("Content-Length"))
            {
                int.TryParse(req.Headers["Content-Length"], out contentLength);
            }

            int bodyStart = headerEnd + 4;
            int bodyAlreadyRead = fullBytes.Length - bodyStart;

            using (var bodyStream = new MemoryStream())
            {
                if (bodyAlreadyRead > 0)
                {
                    bodyStream.Write(fullBytes, bodyStart, bodyAlreadyRead);
                }

                while (bodyStream.Length < contentLength)
                {
                    int toRead = Math.Min(buffer.Length, contentLength - (int)bodyStream.Length);
                    int r = stream.Read(buffer, 0, toRead);
                    if (r <= 0) break;
                    bodyStream.Write(buffer, 0, r);
                }

                req.Body = Encoding.UTF8.GetString(bodyStream.ToArray());
            }

            return req;
        }

        private int FindDoubleCrlf(byte[] data)
        {
            for (int i = 0; i < data.Length - 3; i++)
            {
                if (data[i] == '\r' && data[i + 1] == '\n' && data[i + 2] == '\r' && data[i + 3] == '\n')
                {
                    return i;
                }
            }
            return -1;
        }

        private void HandleApi(NetworkStream stream, SimpleHttpRequest req)
        {
            if (req.Method == "OPTIONS")
            {
                SendHeaders(stream, 200, "text/plain", 0);
                return;
            }

            try
            {
                object result = _api.Execute(req);
                string json = _json.Serialize(result);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                SendHeaders(stream, 200, "application/json; charset=utf-8", bytes.Length);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
            catch (Exception ex)
            {
                string errJson = _json.Serialize(new { error = ex.Message, stack = ex.StackTrace });
                byte[] errBytes = Encoding.UTF8.GetBytes(errJson);
                SendHeaders(stream, 500, "application/json; charset=utf-8", errBytes.Length);
                stream.Write(errBytes, 0, errBytes.Length);
                stream.Flush();
            }
        }

        private void HandleStaticFile(NetworkStream stream, SimpleHttpRequest req)
        {
            string lower = req.Path.TrimEnd('/').ToLowerInvariant();
            string filePath = null;

            if (lower == "" || lower == "/" || lower == "/index" || lower == "/home")
            {
                filePath = Path.Combine(_wwwroot, "index.html");
            }
            else if (lower == "/pos" || lower == "/cashier")
            {
                filePath = Path.Combine(_wwwroot, "pos.html");
            }
            else if (lower == "/kitchen" || lower == "/kds")
            {
                filePath = Path.Combine(_wwwroot, "kitchen.html");
            }
            else if (lower == "/board" || lower == "/display" || lower == "/tv")
            {
                filePath = Path.Combine(_wwwroot, "board.html");
            }
            else if (lower == "/kiosk" || lower == "/terminal")
            {
                filePath = Path.Combine(_wwwroot, "kiosk.html");
            }
            else if (lower == "/courier" || lower == "/delivery")
            {
                filePath = Path.Combine(_wwwroot, "courier.html");
            }
            else if (lower == "/admin" || lower == "/dashboard")
            {
                filePath = Path.Combine(_wwwroot, "admin.html");
            }
            else
            {
                string safePath = req.Path.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                filePath = Path.Combine(_wwwroot, safePath);
            }

            if (!File.Exists(filePath))
            {
                // Fallback для SPA
                filePath = Path.Combine(_wwwroot, "index.html");
            }

            if (File.Exists(filePath))
            {
                string ext = Path.GetExtension(filePath);
                string mime;
                if (!_mimeTypes.TryGetValue(ext, out mime)) mime = "application/octet-stream";

                byte[] fileBytes = File.ReadAllBytes(filePath);
                SendHeaders(stream, 200, mime, fileBytes.Length);
                stream.Write(fileBytes, 0, fileBytes.Length);
                stream.Flush();
            }
            else
            {
                byte[] notFound = Encoding.UTF8.GetBytes("404 Not Found");
                SendHeaders(stream, 404, "text/plain", notFound.Length);
                stream.Write(notFound, 0, notFound.Length);
                stream.Flush();
            }
        }

        private void SendHeaders(NetworkStream stream, int statusCode, string contentType, long contentLength)
        {
            string statusText;
            switch (statusCode)
            {
                case 200: statusText = "OK"; break;
                case 400: statusText = "Bad Request"; break;
                case 401: statusText = "Unauthorized"; break;
                case 403: statusText = "Forbidden"; break;
                case 404: statusText = "Not Found"; break;
                default: statusText = "Internal Server Error"; break;
            }

            string header = string.Format(
                "HTTP/1.1 {0} {1}\r\n" +
                "Content-Type: {2}\r\n" +
                "Content-Length: {3}\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS\r\n" +
                "Access-Control-Allow-Headers: Content-Type, Authorization\r\n" +
                "X-Content-Type-Options: nosniff\r\n" +
                "X-Frame-Options: SAMEORIGIN\r\n" +
                "X-XSS-Protection: 1; mode=block\r\n" +
                "Referrer-Policy: strict-origin-when-cross-origin\r\n" +
                "Connection: close\r\n\r\n",
                statusCode, statusText, contentType, contentLength);

            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
        }

        public void Stop()
        {
            _isRunning = false;
            try { _listener.Stop(); } catch { }
        }
    }
}
