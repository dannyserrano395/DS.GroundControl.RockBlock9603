namespace DS.GroundControl.RockBlock9603.Service.Log4Net
{
    public class LoggerFactory : ILoggerFactory
    {
        public ILogger GetLogger(Type type) => new Logger(log4net.LogManager.GetLogger(type));
    }
}