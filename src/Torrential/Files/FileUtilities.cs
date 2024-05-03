using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Torrential.Files
{
    internal static class FileUtilities
    {
        internal static FileInfo TouchFile(string path, long fileSize = -1)
        {
            if (File.Exists(path))
                return new FileInfo(path);

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using var fs = File.Create(path);

            if (fileSize > 0)
                fs.SetLength(fileSize);

            fs.Close();

            return new FileInfo(path);
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
