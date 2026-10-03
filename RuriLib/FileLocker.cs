using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuriLib
{
    /// <summary>
    /// Singleton class that manages application-wide file locking to avoid cross thread IO operations on the same file.
    /// </summary>
    public static class FileLocker
    {
        /// <summary>
        /// Maps normalized file names to lockable objects with thread-safe atomic access.
        /// </summary>
        private static readonly ConcurrentDictionary<string, object> Locks = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets a lock by file name or creates one if it doesn't exist.
        /// </summary>
        /// <param name="fileName">The name of the file to access</param>
        /// <returns>An object that can be used in a lock statement.</returns>
        public static object GetLock(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return Locks.GetOrAdd(string.Empty, _ => new object());
            try
            {
                string normalized = Path.GetFullPath(fileName);
                return Locks.GetOrAdd(normalized, _ => new object());
            }
            catch
            {
                return Locks.GetOrAdd(fileName, _ => new object());
            }
        }
    }
}
