﻿namespace DS.GroundControl.Lib.Extensions
{
    public static class TaskExtensions
    {
        public static async Task ThrowOnTimeoutAsync(this Task task, TimeSpan timespan)
        {
            using var cts = new CancellationTokenSource();
            if (await Task.WhenAny(task, Task.Delay(timespan, cts.Token)) == task)
            {
                cts.Cancel();
                return;
            }
            throw new TimeoutException();
        }
        public static async Task<bool> TimeoutAfterAsync(this Task task, TimeSpan timespan)
        {
            using var cts = new CancellationTokenSource();
            if (await Task.WhenAny(task, Task.Delay(timespan, cts.Token)) == task)
            {
                cts.Cancel();
                return false;
            }
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