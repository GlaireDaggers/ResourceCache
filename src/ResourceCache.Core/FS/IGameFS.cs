using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ResourceCache.Core.FS
{
    /// <summary>
    /// Delegate for asset changed or deleted events
    /// </summary>
    /// <param name="assetPath">Relative path to the modified or deleted assets</param>
    public delegate void AssetChangedHandler(string assetPath);

    /// <summary>
    /// Interface for an abstract read-only filesystem
    /// </summary>
    public interface IGameFS
    {
        /// <summary>
        /// Event handler for when files in this filesystem are modified
        /// </summary>
        event AssetChangedHandler OnFileChanged;
        
        /// <summary>
        /// Event handler for when files in this filesystem are deleted
        /// </summary>
        event AssetChangedHandler OnFileDeleted;

        /// <summary>
        /// Gets whether this filesystem can safely be accessed in a multithreaded way
        /// </summary>
        bool IsThreadSafe { get; }

        /// <summary>
        /// Check if the given file exists in this filesystem
        /// </summary>
        /// <param name="filepath">The relative path to the file</param>
        /// <returns>True if the file exists, false otherwise</returns>
        bool Exists(string filepath);

        /// <summary>
        /// Open a read-only stream for the given file
        /// </summary>
        /// <param name="filepath">The relative path to the file</param>
        /// <returns>A read-only stream for the file</returns>
        Stream OpenRead(string filepath);
    }
}
