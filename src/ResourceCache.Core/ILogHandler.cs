using System;
using System.Collections.Generic;
using System.Text;

namespace ResourceCache.Core
{
    /// <summary>
    /// Interface for a class which can handle log output from ResourceCache
    /// </summary>
    public interface ILogHandler
    {
        /// <summary>
        /// Log non-critical debug information
        /// </summary>
        void LogInfo(string message);

        /// <summary>
        /// Log warning information
        /// </summary>
        void LogWarn(string message);

        /// <summary>
        /// Log error information
        /// </summary>
        void LogError(string message);
    }

    /// <summary>
    /// Default log handler which just logs to console
    /// </summary>
    public class DefaultLogHandler : ILogHandler
    {
        public void LogInfo(string message)
        {
            Console.WriteLine("[INFO] " + message);
        }

        public void LogWarn(string message)
        {
            Console.WriteLine("[WARNING] " + message);
        }

        public void LogError(string message)
        {
            Console.WriteLine("[ERROR] " + message);
        }
    }
}
