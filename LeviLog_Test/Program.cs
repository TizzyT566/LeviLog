using System.Drawing;
using System.Text;
using static LeviLog;

public sealed class FirstLogger : WebConsole;
public sealed class SecondLogger : WebDocument;
public sealed class FancyLogger : WebDocument
{
    public override string Encode(params object[] obj)
    {
        StringBuilder sb = new();
        foreach (var o in obj)
        {
            if (o != null && o.GetType().IsGenericType && o.GetType().GetGenericTypeDefinition() == typeof(ValueTuple<,>))
            {
                object firstItem = o.GetType().GetField("Item1")?.GetValue(o)!;
                Color c = (Color)o.GetType().GetField("Item2")?.GetValue(o)!;
                sb.Append($"<font color=\"#{c.R:X2}{c.G:X2}{c.B:X2}\">{firstItem.ToString()}</font>");
            }
            else
            {
                sb.Append(o?.ToString() ?? "");
            }
        }
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
    }
}

class LeviLog_Test
{
    static async Task Main()
    {
        InitLeviLog(port: 5000, debug: false, index: (list) =>
        {
            StringBuilder sb = new();
            sb.AppendLine($"<h1>Loggers</h1>");
            foreach (string type in list)
            {
                sb.AppendLine($"<a href=\"http://localhost:$LEVILOGGER_LOGGER_PORT$/{type}/\">{type}</a><br/>");
            }
            return sb.ToString();
        });

        ulong counter = 0;

        while (true)
        {
            Log<FirstLogger>(counter++);
            Log<SecondLogger>(counter++);
            Log<FancyLogger>((DateTime.Now, Color.LimeGreen), " \n", (counter, Color.FromArgb(255, 133, 240, 128)), "\n", counter);
            await Task.Delay(1000);
        }
    }
}