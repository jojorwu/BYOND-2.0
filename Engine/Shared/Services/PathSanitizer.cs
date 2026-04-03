namespace Shared;
    public static class PathSanitizer
    {
        public static string Sanitize(IProject project, string userProvidedPath, string expectedRootFolder)
        {
            if (userProvidedPath == null) throw new System.ArgumentNullException(nameof(userProvidedPath));

            // Get the full path of the project's root for the given type (e.g., /tmp/proj/scripts)
            var fullRootPath = System.IO.Path.GetFullPath(project.GetFullPath(expectedRootFolder));

            // Ensure the root path ends with a directory separator to prevent prefix-based bypasses
            var rootWithSeparator = fullRootPath;
            if (!rootWithSeparator.EndsWith(System.IO.Path.DirectorySeparatorChar))
            {
                rootWithSeparator += System.IO.Path.DirectorySeparatorChar;
            }

            // Get the full path of the user-provided file relative to the project root
            var fullUserPath = System.IO.Path.GetFullPath(project.GetFullPath(userProvidedPath));

            var relativePath = System.IO.Path.GetRelativePath(fullRootPath, fullUserPath);
            if (relativePath.StartsWith("..") || System.IO.Path.IsPathRooted(relativePath))
            {
                // Edge case: exactly the root path is allowed
                if (fullUserPath != fullRootPath)
                {
                    throw new System.Security.SecurityException("Access to path is denied.");
                }
            }
            return fullUserPath;
        }
    }
