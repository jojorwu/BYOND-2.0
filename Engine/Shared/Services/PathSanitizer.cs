using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Shared.Services
{
    public static class PathSanitizer
    {
        public static string Sanitize(IProject project, string userProvidedPath, string expectedRootFolder)
        {
            // Get the full path of the project's root for the given type (e.g., /tmp/proj/scripts)
            var fullRootPath = System.IO.Path.GetFullPath(project.GetFullPath(expectedRootFolder));

            // Get the full path of the user-provided file relative to the project root
            var fullUserPath = System.IO.Path.GetFullPath(project.GetFullPath(userProvidedPath));

            if (!fullUserPath.StartsWith(fullRootPath))
            {
                throw new System.Security.SecurityException("Access to path is denied.");
            }
            return fullUserPath;
        }
    }
}
