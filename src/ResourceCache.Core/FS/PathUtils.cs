using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ResourceCache.Core.FS
{
    public static class PathUtils
    {
        /// <summary>
        /// Normalize a path string, replacing all directory separators with a forward slash
        /// </summary>
        public static string NormalizePathString(string path)
        {
            return path.Replace('\\', '/');
        }

        /// <summary>
        /// Asserts that the given path stays within the given folder, throws if path points to a location outside of the given folder
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <param name="relativePath">The path it should be relative to</param>
        public static void AssertPathRelative(string path, string relativePath)
        {
            if (!Path.GetFullPath(path).StartsWith(Path.GetFullPath(relativePath)))
            {
                throw new IOException($"Given path {path} points to a location outside of its relative folder {relativePath}");
            }
        }

        /// <summary>
        /// Make one path relative to another
        /// </summary>
        /// <param name="relativeTo">The path to make it relative to</param>
        /// <param name="path">The path to make relative</param>
        /// <returns>The relative path</returns>
        public static string MakePathRelative(string relativeTo, string path)
        {
            if (!relativeTo.EndsWith("/"))
            {
                relativeTo += "/";
            }

            Uri a = new Uri(relativeTo);
            Uri b = new Uri(path);
            return a.MakeRelativeUri(b).ToString();
        }
    }
}
