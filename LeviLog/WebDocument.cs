using System;
using System.Text;

public static partial class LeviLog
{
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
                        const maxTries = 3;
                        let tries = 0;
                        let messages = 0;
                        let crnt_batch;
                        event_source.onmessage = function(event)
                        {
                            tries = 0;
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
                        event_source.onerror = function(event)
                        {
                            if (tries < maxTries)
                            {
                                tries++;
                            }
                            else
                            {
                                event_source.close();
                                console.log("Logger closed, retries exhausted.");
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
