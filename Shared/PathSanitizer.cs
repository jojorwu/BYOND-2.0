using System;
using System.IO;
using System.Security;

namespace Shared
{
    public static class PathSanitizer
    {
        /// <summary>
        /// Sanitizes a relative path to ensure it falls within a specified base directory.
        /// Prevents path traversal attacks.
        /// </summary>
        /// <param name="basePath">The absolute path of the directory to contain the file.</param>
        /// <param name="relativePath">The relative path provided by a user or script.</param>
        /// <returns>The full, sanitized absolute path.</returns>
        /// <exception cref="SecurityException">Thrown if the path is malicious or invalid.</exception>
        public static string Sanitize(string basePath, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new SecurityException("Path cannot be empty or whitespace.");
            }

            // Ensure the base path is absolute
            string fullBasePath = Path.GetFullPath(basePath);

            // Combine the base path with the relative path
            string combinedPath = Path.Combine(fullBasePath, relativePath);

            // Get the canonicalized, absolute path
            string fullCombinedPath = Path.GetFullPath(combinedPath);

            // The core security check: ensure the resulting path is still within the base directory.
            // The check for DirectorySeparatorChar is to prevent cases like "/base/path" and "/base/path_extra".
            if (!fullCombinedPath.StartsWith(fullBasePath + Path.DirectorySeparatorChar) && fullCombinedPath != fullBasePath)
            {
                throw new SecurityException($"Path traversal attempt detected. The path '{relativePath}' attempts to escape the base directory.");
            }

            return fullCombinedPath;
        }
    }
}
