namespace DS.GroundControl.RockBlock9603.Log4Net
{
    public interface ILoggerFactory
    {
        ILogger GetLogger(Type type);
    }
}