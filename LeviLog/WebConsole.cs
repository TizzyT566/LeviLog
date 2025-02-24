using System;
using System.Text;

public static partial class LeviLog
{
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
}
