namespace DS.GroundControl.Lib.Extensions
{
    public static class CancellationTokenExtensions
    {
        public static Task WhenCanceledAsync(this CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }
    }
}