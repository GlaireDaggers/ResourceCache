using System;
using System.Collections.Generic;
using System.Text;

namespace ResourceCache.Core
{
    /// <summary>
    /// Exception representing a failure to load a resource
    /// </summary>
    public class ResourceException : Exception
    {
        public readonly string resourcePath;

        public ResourceException(string path, string message) : base(message)
        {
            resourcePath = path;
        }

        public override string ToString()
        {
            return $"{Message} ({resourcePath})";
        }
    }
}
