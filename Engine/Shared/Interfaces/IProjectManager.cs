using System.Threading.Tasks;

namespace Shared;
    /// <summary>
    /// Defines the contract for a service that manages project-related operations.
    /// </summary>
    public interface IProjectManager
    {
        /// <summary>
        /// Creates a new project structure at the specified path.
        /// </summary>
        /// <param name="projectName">The name of the project.</param>
        /// <param name="projectPath">The absolute path where the project directory will be created.</param>
        /// <returns>True if the project was created successfully, false otherwise.</returns>
        Task<bool> CreateProjectAsync(string projectName, string projectPath);
    }
