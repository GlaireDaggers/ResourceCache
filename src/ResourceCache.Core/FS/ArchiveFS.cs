using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.IO.Compression;

namespace ResourceCache.Core.FS
{
    /// <summary>
    /// Implementation of IGameFS which wraps a ZIP archive
    /// </summary>
    public class ArchiveFS : IGameFS, IDisposable
    {
        public bool IsThreadSafe => false;

        public string MountPoint { get; set; }

        public event AssetChangedHandler OnFileChanged;
        public event AssetChangedHandler OnFileDeleted;

        private string _archivePath;
        private ZipArchive _archive;
        private Dictionary<string, ZipArchiveEntry> _entryTable;
        private FileSystemWatcher _fsWatcher;

        /// <summary>
        /// Creates a new ArchiveFS instance
        /// </summary>
        /// <param name="archivePath">Path to the ZIP archive to use</param>
        public ArchiveFS(string archivePath)
        {
            _archivePath = Path.GetFullPath(archivePath);
            _archive = ZipFile.OpenRead(_archivePath);

            // cache all entries in a table by path string
            _entryTable = new Dictionary<string, ZipArchiveEntry>();

            foreach (var entry in _archive.Entries)
            {
                _entryTable[entry.FullName] = entry;
            }

            _fsWatcher = new FileSystemWatcher(Path.GetDirectoryName(_archivePath));
            _fsWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
            _fsWatcher.Filter = Path.GetFileName(_archivePath);
            _fsWatcher.Changed += _fsWatcher_Changed;
            _fsWatcher.EnableRaisingEvents = true;
        }

        public bool Exists(string filepath)
        {
            if (_entryTable.ContainsKey(filepath))
            {
                return true;
            }

            return false;
        }

        public Stream OpenRead(string filepath)
        {
            if (!_entryTable.ContainsKey(filepath))
            {
                throw new FileNotFoundException($"Could not find file in archive: {filepath}");
            }

            return _entryTable[filepath].Open();
        }

        private void _fsWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            // if the archive is modified, we need to completely reload it and rebuild a new entry
            // for any file that exists in both the old and new entry tables, a Changed event needs to be raised
            // for any file that exists in the old table, but not the new table, a Deleted event needs to be raised

            var newArchive = ZipFile.OpenRead(_archivePath);
            var newEntryTable = new Dictionary<string, ZipArchiveEntry>();

            foreach (var entry in newArchive.Entries)
            {
                newEntryTable[entry.FullName] = entry;
            }

            foreach (var kvp in _entryTable)
            {
                if (newEntryTable.ContainsKey(kvp.Key))
                {
                    OnFileChanged?.Invoke(kvp.Key);
                }
                else
                {
                    OnFileDeleted?.Invoke(kvp.Key);
                }
            }

            // dispose of old archive and replace with new archive & entry table
            _archive.Dispose();
            _archive = newArchive;
            _entryTable = newEntryTable;
        }

        public void Dispose()
        {
            _fsWatcher.Changed -= _fsWatcher_Changed;
            _fsWatcher.Dispose();
            _archive.Dispose();
        }
    }
}
