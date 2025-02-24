using System;
using System.Net;

public static partial class LeviLog
{
    public abstract class LoggerBase
    {
        internal int _lock = 0;

        internal Guid _sessionId = Guid.Empty;
        internal HttpListenerResponse? Stream = null!; // strangely renaming this to anything other than Stream changes the behavior

        public abstract string HTML();
        public abstract string Encode(params object[] obj);
    }
}
