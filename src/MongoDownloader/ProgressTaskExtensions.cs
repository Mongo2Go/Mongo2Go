using System;
using Spectre.Console;

namespace MongoDownloader
{
    public static class ProgressTaskExtensions
    {
        public static IProgress<T> AsProgress<T>(this ProgressTask progressTask, Func<T, string>? description = null, Func<T, double?>? percentage = null)
        {
            return new Progress<T>(progressTask, description, percentage);
        }

        private class Progress<T> : IProgress<T>
        {
            private readonly WeakReference<ProgressTask> _progressTask;
            private readonly Func<T, string>? _description;
            private readonly Func<T, double?>? _percentage;

            public Progress(ProgressTask progressBar, Func<T, string>? description, Func<T, double?>? percentage)
            {
                _progressTask = new WeakReference<ProgressTask>(progressBar);
                _description = description;
                _percentage = percentage ?? (value => value as double? ?? value as float?);
            }

            public void Report(T value)
            {
                if (!_progressTask.TryGetTarget(out var progressTask))
                {
                    return;
                }

                var description = _description?.Invoke(value);
                if (description != null)
                {
                    progressTask.Description = description;
                }

                var percentage = _percentage?.Invoke(value);
                if (percentage.HasValue)
                {
                    ((IProgress<double>)progressTask).Report(percentage.Value * progressTask.MaxValue);
                }
            }
        }
    }
}