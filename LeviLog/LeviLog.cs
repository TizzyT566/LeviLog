using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public static partial class LeviLog
{
    private static IReadOnlyDictionary<string, LoggerBase> loggers = null!;
    private static HttpListener listener = null!;
    private static int _port = 54881;
    private static bool _debug = false;
    private static Func<IReadOnlyList<string>, string> _index = (loggerList) =>
        {
            StringBuilder sb = new();
            sb.AppendLine($"<h1>Loggers</h1>");
            foreach (string type in loggerList)
            {
                sb.AppendLine($"<a href=\"http://localhost:{_port}/{type}/\">{type}</a><br/>");
            }
            return sb.ToString();
        };

    public static void InitLeviLog(int port = 54881, bool debug = false, Func<IReadOnlyList<string>, string>? index = null)
    {
        if (listener is not null)
        {
            throw new InvalidOperationException($"{nameof(LeviLog)} is already running.");
        }

        _port = port;
        _debug = debug;

        Type recordType = typeof(LoggerBase);
        SortedDictionary<string, LoggerBase> loggers = [];
        foreach (Type type in Assembly.GetEntryAssembly().GetTypes())
        {
            if (!type.IsAbstract && recordType.IsAssignableFrom(type) && !string.IsNullOrEmpty(type.FullName))
            {
                loggers.Add(type.FullName, (LoggerBase)Activator.CreateInstance(type));
                if (_debug)
                {
                    Console.WriteLine($"DEBUG::Init\n{type.FullName} added\n");
                }
            }
        }
        LeviLog.loggers = loggers;
        if (index is not null)
        {
            _index = index;
        }

        string url = $"http://localhost:{_port}/";
        listener = new();
        listener.Prefixes.Add(url);
        listener.Start();

        if (_debug)
        {
            Console.WriteLine($"DEBUG::Init\n{nameof(LeviLog)} started at {url}\n");
        }

        ThreadPool.QueueUserWorkItem(_ => HandleClients());
    }

    private static void HandleClients()
    {
        HttpListenerContext context = listener.GetContext();
        ThreadPool.QueueUserWorkItem(_ => HandleClients());
        HttpListenerResponse response = context.Response;
        response.Headers.Add("Cache-Control", "no-cache");
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.ContentEncoding = Encoding.UTF8;

        if (_debug)
        {
            Console.WriteLine($"DEBUG::RequestURL\n{context.Request.Url}\n");
        }

        switch (context.Request.Url!.Segments.Length)
        {
            case 1:
                {
                    try
                    {
                        response.ContentType = "text/html";
                        response.KeepAlive = false;
                        response.Headers.Add("Connection", "close");
                        byte[] bytes = Encoding.UTF8.GetBytes(_index([.. loggers.Keys])
                            .Replace("$LEVILOGGER_LOGGER_PORT$", _port.ToString()));
                        response.ContentLength64 = bytes.Length;
                        response.OutputStream.Write(bytes, 0, bytes.Length);
                        response.OutputStream.Flush();
                    }
                    finally
                    {
                        response.Close();
                    }
                    break;
                }
            case 2:
                {
                    if (!loggers.TryGetValue(context.Request.Url.Segments[1].Trim('/'), out LoggerBase logger))
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        response.Close();
                        return;
                    }

                    response.ContentType = "text/html";
                    response.KeepAlive = false;
                    response.Headers.Add("Connection", "close");

                    while (Interlocked.CompareExchange(ref logger._lock, 1, 0) == 1) ;

                    logger._resp?.Close();
                    logger._resp = null;

                    byte[] bytes = Encoding.UTF8.GetBytes(logger.HTML()
                        .Replace("$LEVILOGGER_LOGGER_PORT$", _port.ToString())
                        .Replace("$LEVILOGGER_LOGGER_NAME$", logger.GetType().FullName)
                        .Replace("$LEVILOGGER_SESSION_ID$", (logger._sessionId = Guid.NewGuid()).ToString())); ;
                    response.ContentLength64 = bytes.Length;
                    response.OutputStream.Write(bytes, 0, bytes.Length);
                    response.OutputStream.Flush();

                    Interlocked.Exchange(ref logger._lock, 0);
                    break;
                }
            case 3:
                {
                    if (!loggers.TryGetValue(context.Request.Url.Segments[1].Trim('/'), out LoggerBase logger))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        context.Response.Close();
                        return;
                    }

                    response.ContentType = "text/event-Stream";
                    response.Headers.Add("Connection", "keep-alive");

                    while (Interlocked.CompareExchange(ref logger._lock, 1, 0) == 1) ;

                    if (context.Request.Url.Segments[2].Trim('/') == logger._sessionId.ToString())
                    {
                        logger._resp = response;
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        response.Close();
                    }

                    Interlocked.Exchange(ref logger._lock, 0);
                    break;
                }
            default:
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Close();
                    break;
                }
        }
    }

    public static void Log<T>(params object[] obj) where T : LoggerBase
    {
        LoggerBase logger = loggers[typeof(T).FullName];

        while (Interlocked.CompareExchange(ref logger._lock, 1, 0) == 1) ;

        if (logger._resp is not null)
        {
            try
            {
                string msg = $"data: {logger.Encode(obj)}\n\n";
                if (_debug)
                {
                    Console.WriteLine($"DEBUG::Out\n{msg}");
                }
                byte[] data = Encoding.UTF8.GetBytes(msg);
                logger._resp.OutputStream.Write(data, 0, data.Length);
                logger._resp.OutputStream.Flush();
            }
            catch (Exception)
            {
                logger._resp?.Close();
                logger._resp = null!;
            }
        }

        Interlocked.Exchange(ref logger._lock, 0);
    }

    public static async Task LogAsync<T>(params object[] obj) where T : LoggerBase
    {
        LoggerBase logger = loggers[typeof(T).FullName];

        while (Interlocked.CompareExchange(ref logger._lock, 1, 0) == 1) ;

        if (logger._resp is not null)
        {
            try
            {
                string msg = $"data: {logger.Encode(obj)}\n\n";
                if (_debug)
                {
                    Console.WriteLine($"DEBUG::OutAsync\n{msg}");
                }
                byte[] data = Encoding.UTF8.GetBytes(msg);
                await logger._resp.OutputStream.WriteAsync(data, 0, data.Length);
                await logger._resp.OutputStream.FlushAsync();
            }
            catch (Exception)
            {
                logger._resp?.Close();
                logger._resp = null!;
            }
        }

        Interlocked.Exchange(ref logger._lock, 0);
    }
}