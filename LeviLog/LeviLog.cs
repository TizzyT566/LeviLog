using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public static class LeviLog
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
        Dictionary<string, LoggerBase> loggers = [];
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

                    logger.Stream?.Close();
                    logger.Stream = null;

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
                        logger.Stream = response;
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

        if (logger.Stream is not null)
        {
            try
            {
                string msg = $"data: {logger.Encode(obj)}\n\n";
                if (_debug)
                {
                    Console.WriteLine($"DEBUG::Log\n{msg}");
                }
                byte[] data = Encoding.UTF8.GetBytes(msg);
                logger.Stream.OutputStream.Write(data, 0, data.Length);
                logger.Stream.OutputStream.Flush();
            }
            catch (Exception)
            {
                logger.Stream?.Close();
                logger.Stream = null!;
            }
        }

        Interlocked.Exchange(ref logger._lock, 0);
    }

    public static async Task LogAsync<T>(params object[] obj) where T : LoggerBase
    {
        LoggerBase logger = loggers[typeof(T).FullName];

        while (Interlocked.CompareExchange(ref logger._lock, 1, 0) == 1) ;

        if (logger.Stream is not null)
        {
            try
            {
                string msg = $"data: {logger.Encode(obj)}\n\n";
                if (_debug)
                {
                    Console.WriteLine($"DEBUG::LogAsync\n{msg}");
                }
                byte[] data = Encoding.UTF8.GetBytes(msg);
                await logger.Stream.OutputStream.WriteAsync(data, 0, data.Length);
                await logger.Stream.OutputStream.FlushAsync();
            }
            catch (Exception)
            {
                logger.Stream?.Close();
                logger.Stream = null!;
            }
        }

        Interlocked.Exchange(ref logger._lock, 0);
    }

    public abstract class LoggerBase
    {
        internal int _lock = 0;

        internal Guid _sessionId = Guid.Empty;
        internal HttpListenerResponse? Stream = null!; // strangely renaming this to anything other than Stream changes the behavior

        public abstract string HTML();
        public abstract string Encode(params object[] obj);
    }

    public abstract class WebConsole : LoggerBase
    {
        public override string HTML() =>
            """
            <!DOCTYPE html>
            <html lang="en">
                <head>
                    <link rel="icon" href="data:,">
                    <title>$LEVILOGGER_LOGGER_NAME$</title>
                </head>
                <body>
                    <script>
                        const event_source = new EventSource("http://localhost:$LEVILOGGER_LOGGER_PORT$/$LEVILOGGER_LOGGER_NAME$/$LEVILOGGER_SESSION_ID$");
                        event_source.onmessage = function(event)
                        {
                            console.log(atob(event.data));
                        };
                    </script>
                </body>
            </html>
            """;

        public override string Encode(params object[] obj)
        {
            StringBuilder sb = new();
            foreach (object o in obj)
            {
                sb.AppendLine(o?.ToString() ?? string.Empty);
            }
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
        }
    }

    public abstract class WebDocument : LoggerBase
    {
        public override string HTML() =>
            """
            <!DOCTYPE html>
            <html lang="en">
                <head>
                    <meta charset="UTF-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1.0">
                    <link rel="icon" href="data:,">
                    <link href="https://fonts.cdnfonts.com/css/cascadia-code" rel="stylesheet">
                    <title>$LEVILOGGER_LOGGER_NAME$</title>
                    <style>
                        body {
                            background-color: rgb(12, 12, 12);
                            color: rgb(204, 204, 204);
                            font-family: 'Cascadia Mono', sans-serif;
                            font-size: 12pt;
                            margin: 0px;
                            white-space: pre-wrap;
                            overflow-y: scroll;
                            display: flex;
                            flex-direction: column;
                        }
                        #terminal {
                            font-size: 12pt;
                            margin: 0px;
                        }
                        #scrollButton {
                            position: fixed;
                            top: 10px;
                            right: 10px;
                            width: 32px;
                            height: 32px;
                            background-color: rgba(255, 255, 255, 1.0);
                            border-radius: 50%;
                            border: none;
                            cursor: pointer;
                            display: flex;
                            align-items: center;
                            justify-content: center;
                            font-size: 24px;
                            color: black;
                            transition: opacity 0.3s;
                            opacity: 0.33;
                            user-select: none;
                        }
                        #scrollButton:hover {
                            opacity: 0.8;
                        }
                    </style>
                </head>
                <body>
                    <div id="terminal"></div>
                    <button id="scrollButton">⤓</button>
                    <script>
                        document.getElementById("scrollButton").onclick = scrollToBottom;
                        const event_source = new EventSource("http://localhost:$LEVILOGGER_LOGGER_PORT$/$LEVILOGGER_LOGGER_NAME$/$LEVILOGGER_SESSION_ID$");
                        const terminal = document.getElementById("terminal");
                        const capacity = 2500;
                        const batch = 250;
                        let messages = 0;
                        let crnt_batch;
                        event_source.onmessage = function(event)
                        {
                            const atBottom = document.body.scrollHeight - window.scrollY <= window.innerHeight + 1;
                            const msg = atob(event.data);
                            if (messages % batch == 0)
                            {
                                if (messages >= capacity)
                                {
                                    const removing_div = terminal.firstChild;
                                    messages -= removing_div.childElementCount;
                                    terminal.removeChild(removing_div);
                                }
                                const new_batch = document.createElement("div");
                                terminal.appendChild(new_batch);
                                crnt_batch = new_batch;
                            }
                            const messageElement = document.createElement("div");
                            messageElement.style.margin = '8px 8px 0px 8px';
                            messageElement.innerHTML = msg;
                            crnt_batch.appendChild(messageElement);
                            messages++;
                            if (atBottom)
                            {
                                scrollToBottom();
                            }
                        };
                        function scrollToBottom()
                        {
                            window.scrollTo({ top: document.body.scrollHeight, behavior: 'instant' });
                        }
                    </script>
                </body>
            </html>
            """;

        public override string Encode(params object[] obj)
        {
            StringBuilder sb = new();
            foreach (object o in obj)
            {
                sb.AppendLine(o?.ToString() ?? string.Empty);
            }
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
        }
    }
}
