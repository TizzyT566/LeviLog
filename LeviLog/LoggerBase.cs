using System;
using System.Net;

public static partial class LeviLog
{
    public abstract class LoggerBase
    {
        internal int _lock = 0;

        internal Guid _sessionId = Guid.Empty;
        internal HttpListenerResponse? _resp = null!;

        public abstract string HTML();
        public abstract string Encode(params object[] obj);
    }
}
