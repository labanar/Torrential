using System.Text;

namespace Torrential.Files
{
    internal static class FileUtilities
    {
        internal static void TouchFile(string path, long fileSize = -1)
        {
            if (File.Exists(path))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var fs = File.Create(path);

            if (fileSize <= 0)
                return;

            fs.SetLength(fileSize);
            fs.Close();
        }

        internal static string GetPathSafeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var fileNameBuilder = new StringBuilder();
            foreach (var c in fileName)
            {
                if (invalidChars.Contains(c)) continue;
                fileNameBuilder.Append(c);
            }
            return fileNameBuilder.ToString();
        }

        internal static string GetSafeRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            var invalidPathChars = Path.GetInvalidPathChars();
            var invalidFileNameChars = Path.GetInvalidFileNameChars();
            var normalizedPath = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            var segments = normalizedPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var sanitizedSegments = new List<string>(segments.Length);

            foreach (var segment in segments)
            {
                if (segment is "." or "..")
                    continue;

                var segmentBuilder = new StringBuilder(segment.Length);
                foreach (var c in segment)
                {
                    if (invalidPathChars.Contains(c))
                        continue;

                    if (invalidFileNameChars.Contains(c))
                        continue;

                    segmentBuilder.Append(c);
                }

                var sanitized = segmentBuilder.ToString().Trim().TrimEnd('.');
                if (sanitized.Length == 0)
                    continue;

                sanitizedSegments.Add(sanitized);
            }

            return string.Join(Path.DirectorySeparatorChar, sanitizedSegments);
        }

        internal static bool TryResolvePathUnderRoot(string rootPath, string relativePath, out string resolvedPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
            resolvedPath = string.Empty;

            var safeRelativePath = GetSafeRelativePath(relativePath);
            if (string.IsNullOrWhiteSpace(safeRelativePath))
                return false;

            var rootFullPath = Path.GetFullPath(rootPath);
            var destinationFullPath = Path.GetFullPath(Path.Combine(rootFullPath, safeRelativePath));
            var rootPrefix = rootFullPath.EndsWith(Path.DirectorySeparatorChar)
                ? rootFullPath
                : rootFullPath + Path.DirectorySeparatorChar;

            if (!destinationFullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            resolvedPath = destinationFullPath;
            return true;
        }
        
        internal static string AppDataPath { get; } = Environment.GetEnvironmentVariable("APP_DATA_PATH") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "torrential");
    }
}
