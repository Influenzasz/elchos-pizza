// EventBus.cs - Real-time SSE (Server-Sent Events) менеджер на базе TCP сокетов
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace PizzaSystem.Realtime
{
    public class EventBus
    {
        private static readonly object _lock = new object();
        private static readonly List<NetworkStream> _subscribers = new List<NetworkStream>();
        private static readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private static Thread _heartbeatThread;
        private static bool _running = true;

        static EventBus()
        {
            _heartbeatThread = new Thread(HeartbeatLoop)
            {
                IsBackground = true,
                Name = "SSE-Heartbeat"
            };
            _heartbeatThread.Start();
        }

        public static void Subscribe(TcpClient client, NetworkStream stream)
        {
            try
            {
                string header = "HTTP/1.1 200 OK\r\n" +
                                "Content-Type: text/event-stream; charset=utf-8\r\n" +
                                "Cache-Control: no-cache, no-transform\r\n" +
                                "Connection: keep-alive\r\n" +
                                "Access-Control-Allow-Origin: *\r\n\r\n";
                byte[] headerBytes = Encoding.UTF8.GetBytes(header);
                stream.Write(headerBytes, 0, headerBytes.Length);

                // Приветственный пакет
                var initMsg = new { type = "connected", message = "Подключение к шине событий пиццерии успешно", time = DateTime.Now.ToString("HH:mm:ss") };
                byte[] initBytes = Encoding.UTF8.GetBytes("data: " + _json.Serialize(initMsg) + "\n\n");
                stream.Write(initBytes, 0, initBytes.Length);
                stream.Flush();

                lock (_lock)
                {
                    _subscribers.Add(stream);
                }
            }
            catch
            {
                try { client.Close(); } catch { }
            }
        }

        public static void Publish(string eventType, object data)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var payload = new
                {
                    type = eventType,
                    data = data,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                string jsonStr = _json.Serialize(payload);
                byte[] bytes = Encoding.UTF8.GetBytes("data: " + jsonStr + "\n\n");

                List<NetworkStream> clients;
                lock (_lock)
                {
                    clients = new List<NetworkStream>(_subscribers);
                }

                var toRemove = new List<NetworkStream>();
                foreach (var stream in clients)
                {
                    try
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush();
                    }
                    catch
                    {
                        toRemove.Add(stream);
                    }
                }

                if (toRemove.Count > 0)
                {
                    lock (_lock)
                    {
                        foreach (var dead in toRemove)
                        {
                            _subscribers.Remove(dead);
                            try { dead.Close(); } catch { }
                        }
                    }
                }
            });
        }

        private static void HeartbeatLoop()
        {
            byte[] pingBytes = Encoding.UTF8.GetBytes(": ping\n\n");
            while (_running)
            {
                Thread.Sleep(15000); // 15 секунд

                List<NetworkStream> clients;
                lock (_lock)
                {
                    clients = new List<NetworkStream>(_subscribers);
                }

                var toRemove = new List<NetworkStream>();
                foreach (var stream in clients)
                {
                    try
                    {
                        stream.Write(pingBytes, 0, pingBytes.Length);
                        stream.Flush();
                    }
                    catch
                    {
                        toRemove.Add(stream);
                    }
                }

                if (toRemove.Count > 0)
                {
                    lock (_lock)
                    {
                        foreach (var dead in toRemove)
                        {
                            _subscribers.Remove(dead);
                            try { dead.Close(); } catch { }
                        }
                    }
                }
            }
        }

        public static void Stop()
        {
            _running = false;
            lock (_lock)
            {
                foreach (var client in _subscribers)
                {
                    try { client.Close(); } catch { }
                }
                _subscribers.Clear();
            }
        }
    }
}
