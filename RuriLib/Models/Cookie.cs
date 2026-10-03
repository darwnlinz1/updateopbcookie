using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;

namespace RuriLib.Models
{
    public class Cookie : Persistable<Guid>
    {
        private string name;
        /// <summary>The name of the Cookie.</summary>
        public string Name 
        { 
            get => name; 
            set { name = value; OnPropertyChanged(); } 
        }

        private string pathOwnerFolder;
        /// <summary>The path where the file is stored on disk.</summary>
        public string PathOwnerFolder 
        { 
            get => pathOwnerFolder; 
            set { pathOwnerFolder = value; OnPropertyChanged(); } 
        }

        /// <summary>In-memory file paths list. Excluded from LiteDB BSON to avoid 8MB document limit.</summary>
        [BsonIgnore]
        public List<string> PathAllCookiesFolders { get; set; }

        private int totalCookiesFolders;
        /// <summary>The total number of cookie files discovered.</summary>
        public int TotalCookiesFolders 
        { 
            get => totalCookiesFolders; 
            set { totalCookiesFolders = value; OnPropertyChanged(); } 
        }

        /// <summary>Needed for NoSQL deserialization.</summary>
        public Cookie()
        {
            if (Id == Guid.Empty) Id = Guid.NewGuid();
            PathAllCookiesFolders = new List<string>();
        }

        /// <summary>
        /// Creates an instance of Cookie list with FastScan and disk index persistence.
        /// </summary>
        /// <param name="name">The name of the Cookie collection</param>
        /// <param name="pathownerfolder">The root directory of logs</param>
        /// <param name="scanNow">Whether to scan immediately upon instantiation</param>
        public Cookie(string name, string pathownerfolder, bool scanNow = true)
        {
            Id = Guid.NewGuid();
            Name = name;
            PathOwnerFolder = pathownerfolder;
            PathAllCookiesFolders = new List<string>();

            if (scanNow && !string.IsNullOrEmpty(pathownerfolder))
            {
                string probePath = EnsureExtendedPrefix(pathownerfolder);
                if (Directory.Exists(probePath) || Directory.Exists(pathownerfolder))
                {
                    var files = FastScanCookieFiles(pathownerfolder);
                    PathAllCookiesFolders = files;
                    TotalCookiesFolders = files.Count;
                    SavePathsToIndex(files);
                }
            }
        }

        #region Disk Index Persistence
        public static string GetIndexDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "CookieIndex");
        }

        public string GetIndexFilePath()
        {
            return Path.Combine(GetIndexDirectory(), $"{Id}.idx");
        }

        public void SavePathsToIndex(IEnumerable<string> paths)
        {
            try
            {
                var dir = GetIndexDirectory();
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllLines(GetIndexFilePath(), paths, Encoding.UTF8);
            }
            catch { }
        }

        public List<string> LoadPathsFromIndex()
        {
            var filePath = GetIndexFilePath();
            if (File.Exists(filePath))
            {
                try
                {
                    PathAllCookiesFolders = File.ReadAllLines(filePath, Encoding.UTF8).ToList();
                    TotalCookiesFolders = PathAllCookiesFolders.Count;
                    return PathAllCookiesFolders;
                }
                catch { }
            }

            // Fallback: If index missing on disk but root folder exists, rescan and persist
            if (!string.IsNullOrEmpty(PathOwnerFolder) && Directory.Exists(PathOwnerFolder))
            {
                var scanned = FastScanCookieFiles(PathOwnerFolder);
                PathAllCookiesFolders = scanned;
                TotalCookiesFolders = scanned.Count;
                SavePathsToIndex(scanned);
                return scanned;
            }

            PathAllCookiesFolders = new List<string>();
            TotalCookiesFolders = 0;
            return PathAllCookiesFolders;
        }

        public void DeleteIndexFile()
        {
            try
            {
                var filePath = GetIndexFilePath();
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch { }
        }
        #endregion

        #region Win32 P/Invoke Declarations
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WIN32_FIND_DATAW
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        private enum FINDEX_INFO_LEVELS
        {
            FindExInfoStandard = 0,
            FindExInfoBasic = 1,
            FindExInfoMaxInfoLevel
        }

        private enum FINDEX_SEARCH_OPS
        {
            FindExSearchNameMatch = 0,
            FindExSearchLimitToDirectories = 1,
            FindExSearchLimitToDevices = 2,
            FindExSearchMaxSearchOp
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindFirstFileExW(
            string lpFileName,
            FINDEX_INFO_LEVELS fInfoLevelId,
            out WIN32_FIND_DATAW lpFindFileData,
            FINDEX_SEARCH_OPS fSearchOp,
            IntPtr lpSearchFilter,
            uint dwAdditionalFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool FindNextFileW(IntPtr hFindFile, out WIN32_FIND_DATAW lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindClose(IntPtr hFindFile);

        private const uint FIND_FIRST_EX_LARGE_FETCH = 2;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        private struct DirEntry
        {
            public string Path;
            public bool InTargetTree;
            public int Depth;

            public DirEntry(string path, bool inTargetTree, int depth)
            {
                Path = path;
                InTargetTree = inTargetTree;
                Depth = depth;
            }
        }

        private static string EnsureExtendedPrefix(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (path.StartsWith(@"\\?\")) return path;
            if (path.StartsWith(@"\\")) return @"\\?\UNC\" + path.Substring(2);
            return @"\\?\" + path;
        }

        private static string StripExtendedPrefix(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (path.StartsWith(@"\\?\UNC\")) return @"\\" + path.Substring(8);
            if (path.StartsWith(@"\\?\")) return path.Substring(4);
            return path;
        }

        private static readonly HashSet<string> ExactPrunedFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".svn", "node_modules", "temp", "tmp", "recycle.bin", "$recycle.bin",
            "telegram", "wallets", "discord", "steam", "screenshots", "autofill", "history"
        };

        private static readonly string[] TargetFolderKeywords = new[]
        {
            "cookie", "browser", "chrome", "edge", "brave", "opera", "firefox", "user data", "default", "network", "profile"
        };

        private static readonly string[] IgnoredFilePrefixes = new[]
        {
            "system_info", "information", "all_passwords", "passwords", "url_uniq_", "seed"
        };

        private static bool ShouldPruneDirectory(string dirName)
        {
            if (string.IsNullOrEmpty(dirName)) return false;
            return ExactPrunedFolderNames.Contains(dirName);
        }

        private static bool IsTargetFolder(string dirName)
        {
            if (string.IsNullOrEmpty(dirName)) return false;
            for (int i = 0; i < TargetFolderKeywords.Length; i++)
            {
                if (dirName.IndexOf(TargetFolderKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static bool IsCookieFile(string fileName, bool inTargetTree)
        {
            for (int i = 0; i < IgnoredFilePrefixes.Length; i++)
            {
                if (fileName.IndexOf(IgnoredFilePrefixes[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }

            bool hasValidExt = fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

            if (!hasValidExt) return false;

            if (inTargetTree)
            {
                return true;
            }

            return fileName.IndexOf("cookie", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        #endregion

        #region Ultra-Fast Parallel Scanner
        /// <summary>
        /// Scans rootFolder using native Win32 FindFirstFileExW with FIND_FIRST_EX_LARGE_FETCH,
        /// adaptive top-level branch expansion, work-stealing parallelism, and \\?\ prefix.
        /// </summary>
        public static List<string> FastScanCookieFiles(string rootFolder, IProgress<int> progress = null, CancellationToken ct = default(CancellationToken))
        {
            var results = new ConcurrentBag<List<string>>();
            if (string.IsNullOrEmpty(rootFolder) || !Directory.Exists(rootFolder))
            {
                return new List<string>();
            }

            var cleanRoot = Path.GetFullPath(rootFolder).TrimEnd('\\');
            var extRoot = EnsureExtendedPrefix(cleanRoot);

            // Phase 1: Adaptive Top-Level Branch Expansion (BFS)
            var branchQueue = new Queue<DirEntry>();
            bool rootIsTarget = IsTargetFolder(Path.GetFileName(cleanRoot));
            branchQueue.Enqueue(new DirEntry(extRoot, rootIsTarget, 0));

            int targetBranchCount = Math.Max(32, Environment.ProcessorCount * 4);
            var initialFiles = new List<string>();

            while (branchQueue.Count > 0 && branchQueue.Count < targetBranchCount)
            {
                if (ct.IsCancellationRequested) break;
                var current = branchQueue.Dequeue();
                ScanDirectorySingle(current.Path, current.InTargetTree, current.Depth, branchQueue, initialFiles);
            }

            if (initialFiles.Count > 0)
            {
                results.Add(initialFiles);
            }

            // Phase 2: Parallel Work-Stealing Processing across Workers
            var branches = branchQueue.ToList();
            if (branches.Count > 0)
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount * 2,
                    CancellationToken = ct
                };

                Parallel.ForEach(branches, parallelOptions, branch =>
                {
                    var workerQueue = new Queue<DirEntry>();
                    var localFiles = new List<string>(1024);
                    workerQueue.Enqueue(branch);

                    while (workerQueue.Count > 0)
                    {
                        if (ct.IsCancellationRequested) break;
                        var dir = workerQueue.Dequeue();
                        ScanDirectorySingle(dir.Path, dir.InTargetTree, dir.Depth, workerQueue, localFiles);
                    }

                    if (localFiles.Count > 0)
                    {
                        results.Add(localFiles);
                    }
                });
            }

            // Phase 3: Deduplication & Clean Path Normalization
            var uniqueFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var bag in results)
            {
                for (int i = 0; i < bag.Count; i++)
                {
                    uniqueFiles.Add(StripExtendedPrefix(bag[i]));
                }
            }

            // Fallback: If 0 files found (e.g. non-standard folder layout), search specifically for cookie files
            if (uniqueFiles.Count == 0)
            {
                try
                {
                    var fallbackFiles = Directory.GetFiles(cleanRoot, "*cookie*.txt*", SearchOption.AllDirectories);
                    foreach (var f in fallbackFiles)
                    {
                        uniqueFiles.Add(f);
                    }
                }
                catch { }
            }

            return uniqueFiles.ToList();
        }

        private static void ScanDirectorySingle(
            string dirPath, 
            bool inTargetTree, 
            int depth, 
            Queue<DirEntry> subDirCollector, 
            List<string> fileCollector)
        {
            if (depth > 128) return;

            string searchPattern = dirPath.EndsWith("\\") ? dirPath + "*" : dirPath + "\\*";
            WIN32_FIND_DATAW findData;
            IntPtr hFind = FindFirstFileExW(
                searchPattern,
                FINDEX_INFO_LEVELS.FindExInfoBasic,
                out findData,
                FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                IntPtr.Zero,
                FIND_FIRST_EX_LARGE_FETCH);

            if (hFind == INVALID_HANDLE_VALUE) return;

            try
            {
                do
                {
                    string fileName = findData.cFileName;
                    if (fileName == "." || fileName == "..") continue;

                    bool isDir = (findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
                    bool isReparse = (findData.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0;

                    if (isReparse) continue; // Skip junctions and symlinks to prevent infinite loops

                    string fullPath = dirPath.EndsWith("\\") ? dirPath + fileName : dirPath + "\\" + fileName;

                    if (isDir)
                    {
                        if (ShouldPruneDirectory(fileName))
                        {
                            continue; // Always prune blacklisted directories (History, Autofill, Passwords, etc.)
                        }

                        bool childInTarget = inTargetTree || IsTargetFolder(fileName);
                        subDirCollector.Enqueue(new DirEntry(fullPath, childInTarget, depth + 1));
                    }
                    else
                    {
                        if (IsCookieFile(fileName, inTargetTree))
                        {
                            fileCollector.Add(fullPath);
                        }
                    }
                }
                while (FindNextFileW(hFind, out findData));
            }
            catch { }
            finally
            {
                FindClose(hFind);
            }
        }
        #endregion
    }
}
