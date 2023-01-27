namespace Microsoft.ComponentDetection.Detectors.Pip;
using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

[Export(typeof(IPythonCommandService))]
public class PythonCommandService : IPythonCommandService
{
    [Import]
    public ICommandLineInvocationService CommandLineInvocationService { get; set; }

    public async Task<bool> PythonExistsAsync(string pythonPath = null) => !string.IsNullOrEmpty(await this.ResolvePythonAsync(pythonPath));

    public async Task<IList<(string PackageString, GitComponent Component)>> ParseFileAsync(string filePath, string pythonPath = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return new List<(string, GitComponent)>();
        }

        if (filePath.EndsWith(".py"))
        {
            return (await this.ParseSetupPyFileAsync(filePath, pythonPath))
                .Select<string, (string, GitComponent)>(component => (component, null))
                .ToList();
        }
        else if (filePath.EndsWith(".txt"))
        {
            return this.ParseRequirementsTextFile(filePath);
        }
        else
        {
            return new List<(string, GitComponent)>();
        }
    }

    private async Task<IList<string>> ParseSetupPyFileAsync(string filePath, string pythonExePath = null)
    {
        var pythonExecutable = await this.ResolvePythonAsync(pythonExePath);

        if (string.IsNullOrEmpty(pythonExecutable))
        {
            throw new PythonNotFoundException();
        }

        // This calls out to python and prints out an array like: [ packageA, packageB, packageC ]
        // We need to have python interpret this file because install_requires can be composed at runtime
        var command = await this.CommandLineInvocationService.ExecuteCommandAsync(pythonExecutable, null, $"-c \"import distutils.core; setup=distutils.core.run_setup('{filePath.Replace('\\', '/')}'); print(setup.install_requires)\"");

        if (command.ExitCode != 0)
        {
            return new List<string>();
        }

        var result = command.StdOut;

        result = result.Trim('[', ']', '\r', '\n').Trim();

        // For Python2 if there are no packages (Result: "None") skip any parsing
        if (result.Equals("None", StringComparison.OrdinalIgnoreCase) && !command.StdOut.StartsWith('['))
        {
            return new List<string>();
        }

        return result.Split(new string[] { "'," }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().Trim('\'').Trim()).ToList();
    }

    private IList<(string PackageString, GitComponent Component)> ParseRequirementsTextFile(string path)
    {
        var items = new List<(string, GitComponent)>();
        foreach (var line in File.ReadAllLines(path).Select(x => x.Trim().TrimEnd('\\')).Where(x => !x.StartsWith("#") && !x.StartsWith("-") && !string.IsNullOrWhiteSpace(x)))
        {
            // We technically shouldn't be ignoring information after the ;
            // It's used to indicate environment markers like specific python versions
            // https://www.python.org/dev/peps/pep-0508/#environment-markers
            var toAdd = line.Split(';')[0].Trim();
            var url = toAdd.Split(' ')[0];

            if (url.StartsWith("git+") && Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                // A (potentially non exhaustive) list of possible Url formats
                // git+git://github.com/path/to/package-two@41b95ec#egg=package-two
                // git+git://github.com/path/to/package-two@master#egg=package-two
                // git+git://github.com/path/to/package-two@0.1#egg=package-two
                // git+git://github.com/path/to/package-two@releases/tag/v3.7.1#egg=package-two
                // Source: https://stackoverflow.com/questions/16584552/how-to-state-in-requirements-txt-a-direct-github-source
                // Since it's possible not to have a commit id for the git component
                // We're just going to skip it instead of doing something weird
                var parsedUrl = new Uri(url);
                var pathParts = parsedUrl.PathAndQuery.Split("@");
                if (pathParts.Length < 2)
                {
                    // This is no bueno
                    continue;
                }

                var repoProject = pathParts[0];

                var packageParts = pathParts[1];
                var possibleCommit = packageParts.Split("#")[0];

                // A best effort attempt to see if we're working with something that _could_ be a commit hash
                var shortCommitHash = 7;
                var fullCommitHash = 40;
                var hexRegex = new Regex("([a-z]|[A-Z]|[0-9])+");

                if ((possibleCommit.Length == shortCommitHash || possibleCommit.Length == fullCommitHash)
                    && hexRegex.IsMatch(possibleCommit))
                {
                    var gitComponent = new GitComponent(new Uri($"https://{parsedUrl.Host}{repoProject}"), possibleCommit);
                    items.Add((null, gitComponent));
                }
            }
            else
            {
                toAdd = toAdd.Split("#")[0]; // Remove comment from the line that contains the component name and version.
                toAdd = toAdd.Replace(" ", string.Empty);
                items.Add((toAdd, null));
            }
        }

        return items;
    }

    private async Task<string> ResolvePythonAsync(string pythonPath = null)
    {
        var pythonCommand = string.IsNullOrEmpty(pythonPath) ? "python" : pythonPath;

        if (await this.CanCommandBeLocatedAsync(pythonCommand))
        {
            return pythonCommand;
        }

        return null;
    }

    private async Task<bool> CanCommandBeLocatedAsync(string pythonPath) => await this.CommandLineInvocationService.CanCommandBeLocatedAsync(pythonPath, new List<string> { "python3", "python2" }, "--version");
}
