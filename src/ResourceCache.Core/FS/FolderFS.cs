using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ResourceCache.Core.FS
{
    /// <summary>
    /// Simple IGameFS implementation which just wraps the OS filesystem within a given folder
    /// </summary>
    public class FolderFS : IGameFS, IDisposable
    {
        public bool IsThreadSafe => true;

        public event AssetChangedHandler OnFileChanged;
        public event AssetChangedHandler OnFileDeleted;

        public readonly string rootFolder;

        private FileSystemWatcher _fsWatcher;

        public FolderFS(string rootFolder)
        {
            this.rootFolder = Path.GetFullPath(rootFolder);

            _fsWatcher = new FileSystemWatcher(rootFolder);
            _fsWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            _fsWatcher.Changed += _fsWatcher_Changed;
            _fsWatcher.Renamed += _fsWatcher_Deleted;
            _fsWatcher.Deleted += _fsWatcher_Deleted;
            _fsWatcher.Filter = "*";
            _fsWatcher.IncludeSubdirectories = true;
            _fsWatcher.EnableRaisingEvents = true;
        }

        public bool Exists(string filepath)
        {
            string path = Path.Combine(rootFolder, filepath);
            PathUtils.AssertPathRelative(path, rootFolder);
            return File.Exists(path);
        }

        public Stream OpenRead(string filepath)
        {
            string path = Path.Combine(rootFolder, filepath);
            PathUtils.AssertPathRelative(path, rootFolder);
            return File.OpenRead(path);
        }

        private void _fsWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            OnFileDeleted?.Invoke(PathUtils.NormalizePathString(e.FullPath));
        }

        private void _fsWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            OnFileChanged?.Invoke(PathUtils.NormalizePathString(e.FullPath));
        }

        public void Dispose()
        {
            _fsWatcher.Changed -= _fsWatcher_Changed;
            _fsWatcher.Deleted -= _fsWatcher_Deleted;
            _fsWatcher.Dispose();
        }
    }
}
