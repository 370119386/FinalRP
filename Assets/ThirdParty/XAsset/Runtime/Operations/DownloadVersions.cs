using System;
using System.Collections.Generic;

namespace VEngine
{
    public sealed class DownloadVersions : Operation
    {
        private readonly List<Download> downloaded = new List<Download>();
        public readonly List<Download> errors = new List<Download>();
        public readonly List<DownloadInfo> items = new List<DownloadInfo>();

        private readonly List<Download> progressing = new List<Download>();
        public Action<DownloadVersions> updated;

        public long totalSize { get; private set; }
        public long downloadedBytes { get; private set; }

        public override void Start()
        {
            base.Start();
            downloadedBytes = 0;
            progressing.Clear();
            downloaded.Clear();
            foreach (var info in items)
            {
                totalSize += info.size;
            }
            if (items.Count > 0)
            {
                foreach (var item in items)
                {
                    var download = Download.DownloadAsync(item);
                    progressing.Add(download);
                    download.retryEnabled = false;
                }
            }
            else
            {
                Finish();
            }
        }

        public void Retry()
        {
            base.Start();
            foreach (var download in errors)
            {
                Download.Retry(download);
                progressing.Add(download);
            }
            errors.Clear();
        }

        protected override void Update()
        {
            if (status == OperationStatus.Processing)
            {
                if (progressing.Count > 0)
                {
                    var len = 0L;
                    for (var index = 0; index < progressing.Count; index++)
                    {
                        var item = progressing[index];
                        if (item.isDone)
                        {
                            progressing.RemoveAt(index);
                            index--;
                            downloaded.Add(item);
                            if (item.status == DownloadStatus.Failed)
                            {
                                errors.Add(item);
                            }
                        }
                        else
                        {
                            len += item.downloadedBytes;
                        }
                    }
                    foreach (var item in downloaded)
                    {
                        len += item.downloadedBytes;
                    }
                    downloadedBytes = len;
                    progress = downloadedBytes * 1f / totalSize;
                    updated?.Invoke(this);
                    return;
                }
                updated = null;
                Finish(errors.Count > 0 ? "部分文件下载失败。" : null);
            }
        }
    }
}