using log4net;
using log4net.Core;

namespace DS.GroundControl.RockBlock9603.Service.Log4Net
{
    public class Logger : ILogger
    {
        private ILog Log { get; }

        public Logger(ILog log) => Log = log;

        public void Debug(object message, Exception exception = null)
        {
            Log.Logger.Log(typeof(Logger), Level.Debug, message, exception);
        }
        public void DebugFormat(IFormatProvider provider, string format, params object[] args)
        {
            Log.Logger.Log(typeof(Logger), Level.Debug, string.Format(provider, format, args), null);
        }
        public void DebugFormat(string format, params object[] args)
        {
            Log.Logger.Log(typeof(Logger), Level.Debug, string.Format(format, args), null);
        }
        public void Info(object message, Exception exception = null)
        {
            Log.Logger.Log(typeof(Logger), Level.Info, message, exception);
        }
        public void InfoFormat(IFormatProvider provider, string format, params object[] args)
        {
            Log.Logger.Log(typeof(Logger), Level.Info, string.Format(provider, format, args), null);
        }
        public void InfoFormat(string format, params object[] args)
        {
            Log.Logger.Log(typeof(Logger), Level.Info, string.Format(format, args), null);
        }
        public void Warn(object message, Exception exception = null)
        {
            Log.Logger.Log(typeof(Logger), Level.Warn, message, exception);
        }
        public void WarnFormat(IFormatProvider provider, string format, params object[] args)
        {
            Log.Logger.Log(typeof(Logger), Level.Warn, string.Format(provider, format, args), null);
        }
        public void WarnFormat(string format, params object[] args)
        {
            Log.Logger.Log(typeof(Logger), Level.Warn, string.Format(format, args), null);
        }
        public void Error(object message, Exception exception = null)
        {
            Log.Logger.Log(typeof(Logger), Level.Error, message, exception);
        }
        public void ErrorFormat(IFormatProvider provider, string format, params object[] args)
        {
            Log.Logger.Log(typeof(Logger), Level.Error, string.Format(provider, format, args), null);
        }
        public void ErrorFormat(string format, params object[] args)
        {
            Log.Logger.Log(typeof(Logger), Level.Error, string.Format(format, args), null);
        }
        public void Fatal(object message, Exception exception = null)
        {
            Log.Logger.Log(typeof(Logger), Level.Fatal, message, exception);
        }
        public void FatalFormat(IFormatProvider provider, string format, params string[] args)
        {
            Log.Logger.Log(typeof(Logger), Level.Fatal, string.Format(provider, format, args), null);
        }
        public void FatalFormat(string format, params object[] args)
        {
            Log.Logger.Log(typeof(Logger), Level.Fatal, string.Format(format, args), null);
        }
        public void Trace(object message, Exception exception = null)
        {
            Log.Logger.Log(typeof(Logger), Level.Trace, message, exception);
        }
        public void TraceFormat(IFormatProvider provider, string format, params object[] args)
        {
            Log.Logger.Log(typeof(Logger), Level.Trace, string.Format(provider, format, args), null);
        }
        public void TraceFormat(string format, params object[] args)
        {
            Log.Logger.Log(typeof(Logger), Level.Trace, string.Format(format, args), null);
        }
    }
}