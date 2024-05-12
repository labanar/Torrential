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
            return fileName.ToString();
        }
    }
}
