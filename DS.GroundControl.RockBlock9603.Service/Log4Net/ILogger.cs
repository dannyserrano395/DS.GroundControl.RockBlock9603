namespace DS.GroundControl.RockBlock9603.Service.Log4Net
{
    public interface ILogger
    {
        void Debug(object message, Exception exception = null);
        void DebugFormat(IFormatProvider provider, string format, params object[] args);
        void DebugFormat(string format, params object[] args);
        void Info(object message, Exception exception = null);
        void InfoFormat(IFormatProvider provider, string format, params object[] args);
        void InfoFormat(string format, params object[] args);
        void Warn(object message, Exception exception = null);
        void WarnFormat(IFormatProvider provider, string format, params object[] args);
        void WarnFormat(string format, params object[] args);
        void Error(object message, Exception exception = null);
        void ErrorFormat(IFormatProvider provider, string format, params object[] args);
        void ErrorFormat(string format, params object[] args);
        void Fatal(object message, Exception exception = null);
        void FatalFormat(IFormatProvider provider, string format, params string[] args);
        void FatalFormat(string format, params object[] args);
        void Trace(object message, Exception exception = null);
        void TraceFormat(IFormatProvider provider, string format, params object[] args);
        void TraceFormat(string format, params object[] args);
    }
}