namespace DS.GroundControl.Lib.Extensions
{
    public static class TaskExtensions
    {
        public static async Task<bool> TimeoutAfterAsync(this Task task, TimeSpan timespan, CancellationToken cancellationToken = default)
        {
            using var cts = cancellationToken != default
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : new CancellationTokenSource();

            if (await Task.WhenAny(task, Task.Delay(timespan, cts.Token)) == task)
            {
                cts.Cancel();
                return false;
            }
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }
        public static bool TimeoutAfter(this Task task, TimeSpan timespan)
        {
            using var cts = new CancellationTokenSource();
            if (Task.WaitAny([task, Task.Delay(timespan, cts.Token)]) == 0)
            {
                cts.Cancel();
                return false;
            }
            return true;
        }
    }
}