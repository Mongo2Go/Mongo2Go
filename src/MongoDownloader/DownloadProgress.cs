using System;
using System.Collections.Generic;
using System.Linq;
using ByteSizeLib;
using HttpProgress;
using Spectre.Console;

namespace MongoDownloader
{
    public class DownloadProgress : IProgress<ICopyProgress>
    {
        private readonly ProgressTask _archiveProgress;
        private readonly ProgressTask _globalProgress;
        private readonly IEnumerable<ProgressTask> _allArchiveProgresses;
        private readonly Download _download;
        private readonly string _completedDescription;

        public DownloadProgress(ProgressTask archiveProgress, ProgressTask globalProgress, IEnumerable<ProgressTask> allArchiveProgresses, Download download, string completedDescription)
        {
            _archiveProgress = archiveProgress ?? throw new ArgumentNullException(nameof(archiveProgress));
            _globalProgress = globalProgress ?? throw new ArgumentNullException(nameof(globalProgress));
            _allArchiveProgresses = allArchiveProgresses ?? throw new ArgumentNullException(nameof(allArchiveProgresses));
            _download = download ?? throw new ArgumentNullException(nameof(download));
            _completedDescription = completedDescription ?? throw new ArgumentNullException(nameof(completedDescription));
        }

        public void Report(ICopyProgress progress)
        {
            var speed = ByteSize.FromBytes(progress.BytesTransferred / progress.TransferTime.TotalSeconds);
            _archiveProgress.Description = $"Downloading {_download.Product} for {_download.Platform} from {_download.Archive.Url} at {speed.ToString("0.0")}/s";

            lock (_globalProgress)
            {
                _globalProgress.Value = _allArchiveProgresses.Sum(e => e.Value);
                _globalProgress.MaxValue = _allArchiveProgresses.Sum(e => e.MaxValue);
            }
            _archiveProgress.MaxValue = progress.ExpectedBytes;

            if (progress.PercentComplete <= 0.99)
            {
                _archiveProgress.Value = progress.BytesTransferred;
            }
            else
            {
                _archiveProgress.Description = $"Extracting {_download.Product} for {_download.Platform}";
                _archiveProgress.IsIndeterminate = true;
                lock (_globalProgress)
                {
                    _globalProgress.IsIndeterminate = _allArchiveProgresses.All(e => e.IsFinished || e.IsIndeterminate);
                }
            }
        }

        public void ReportCompleted()
        {
            _archiveProgress.Description = $"Extracted {_download.Product} for {_download.Platform}";
            _archiveProgress.Value = _archiveProgress.MaxValue;
            lock (_globalProgress)
            {
                if (_allArchiveProgresses.All(e => e.IsFinished))
                {
                    _globalProgress.Description = _completedDescription;
                    _globalProgress.Value = _globalProgress.MaxValue;
                }
            }
        }
    }
}