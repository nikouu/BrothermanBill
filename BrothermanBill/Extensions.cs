using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BrothermanBill
{
    public static class Extensions
    {
        public static T ElementAtOrDefault<T>(this T[] array, int index, T @default)
        {
            return index >= 0 && index < array.Count() ? array[index] : @default;
        }

        // hmm somehow turn this into an awaitable where a procss can be started as a fire and forget. but we can do that with Task.Run
        // but im interested in how to do that here, though because we are not concerned with after the process runs, maybe this isnt it
        // https://devblogs.microsoft.com/pfxteam/await-anything/
        public static TaskAwaiter GetAwaiter(this Process process)
        {
            var taskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            process.EnableRaisingEvents = true;
            process.Exited += (sender, e) =>
            {
                var senderProcess = sender as Process;
                if (senderProcess is null)
                {
                    return;
                }

                taskCompletionSource.SetResult();
            };

            return taskCompletionSource.Task.GetAwaiter();

        }
    }
}
