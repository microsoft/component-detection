namespace Microsoft.ComponentDetection.Orchestrator
{
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using CommandLine;
    using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

    [Export(typeof(IArgumentHelper))]
    public class ArgumentHelper : IArgumentHelper
    {
        public ArgumentHelper()
        {
            this.ArgumentSets = Enumerable.Empty<IScanArguments>();
        }

        [ImportMany]
        public IEnumerable<IScanArguments> ArgumentSets { get; set; }

        public static IDictionary<string, string> GetDetectorArgs(IEnumerable<string> detectorArgsList)
        {
            var detectorArgs = new Dictionary<string, string>();

            foreach (var arg in detectorArgsList)
            {
                var keyValue = arg.Split('=');

                if (keyValue.Length != 2)
                {
                    continue;
                }

                detectorArgs.Add(keyValue[0], keyValue[1]);
            }

            return detectorArgs;
        }

        public ParserResult<object> ParseArguments(string[] args)
        {
            return Parser.Default.ParseArguments(args, this.ArgumentSets.Select(x => x.GetType()).ToArray());
        }

        public ParserResult<T> ParseArguments<T>(string[] args, bool ignoreInvalidArgs = false)
        {
            var p = new Parser(x =>
            {
                x.IgnoreUnknownArguments = ignoreInvalidArgs;

                // This is not the main argument dispatch, so we don't want console output.
                x.HelpWriter = null;
            });

            return p.ParseArguments<T>(args);
        }
    }
}
