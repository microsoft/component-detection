using System;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class GitComponent : TypedComponent
    {
        private GitComponent()
        {
            /* Reserved for deserialization */
        }

        public GitComponent(Uri repositoryUrl, string commitHash)
        {
            RepositoryUrl = ValidateRequiredInput(repositoryUrl, nameof(RepositoryUrl), nameof(ComponentType.Git));
            CommitHash = ValidateRequiredInput(commitHash, nameof(CommitHash), nameof(ComponentType.Git));
        }

        public GitComponent(Uri repositoryUrl, string commitHash, string tag)
            : this(repositoryUrl, commitHash)
        {
            Tag = tag;
        }

        public Uri RepositoryUrl { get; set; }

        public string CommitHash { get; set; }

        public string Tag { get; set; }

        public override ComponentType Type => ComponentType.Git;

        public override string Id => $"{RepositoryUrl} : {CommitHash} - {Type}";
    }
}
