using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.ComponentDetection.TestsUtilities
{
    public static class ResourceUtilities
    {
        public static async Task<string> LoadTextAsync(string path)
        {
            var fullPath = Path.Join(Environment.CurrentDirectory, "Resources", path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException();
            }

            return await File.OpenText(fullPath).ReadToEndAsync();
        }
    }
}