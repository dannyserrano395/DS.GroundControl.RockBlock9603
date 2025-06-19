namespace DS.GroundControl.RockBlock9603.Service.Log4Net
{
    public interface ILoggerFactory
    {
        ILogger GetLogger(Type type);
    }
}