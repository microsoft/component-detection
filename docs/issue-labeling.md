# Issue labeling

We try to keep issues well-classified through use of labels.
Any repository collaborator can apply labels according to the below guidelines.

The general idea is that we have:

- status (`status:`)
- type (`type:`)
- detector (`detector:`)
- version (`version:`)

## Label categories

### Status

<details>
    <summary>Status labels</summary>

    status:requirements
    status:blocked
    status:ready
    status:in-progress
    status:waiting-on-response

</details>

Use these to label the status of an issue.
For example, use `status:requirements` to mean that an issue is not yet ready for development to begin.
If we need the original poster or somebody else to respond to a query of ours, apply the `status:waiting-on-response` label.
All open issues should have some `status:*` label applied, and [this search](https://github.com/microsoft/component-detection/issues?q=is%3Aissue+is%3Aopen+sort%3Aupdated-desc+-label%3Astatus%3Arequirements+-label%3Astatus%3Aready+-label%3Astatus%3Ain-progress+-label%3Astatus%3Ablocked+-label%3Astatus%3Awaiting-on-response) can identify any which are missing a status label.

### Type

<details>
    <summary>Type labels</summary>

    type:bug
    type:docs
    type:feature
    type:refactor
    type:help
    type:ci

</details>

Use these to label the type of issue.
For example, use `type:bug` to label a bug type issue, and use `type:feature` for feature requests.
Only use `type:refactor` for code changes, don't use `type:refactor` for documentation type changes.
Use the `type:help` label for issues which should be converted to a discussion post.
The `type:ci` label is for issues related to builds or GitHub Actions.

Any issue which has the label `status:ready` should also have a `type:*` label, and [this search](https://github.com/microsoft/component-detection/issues?q=is%3Aissue+is%3Aopen+sort%3Aupdated-desc+-label%3Atype%3Abug+label%3Astatus%3Aready+-label%3Atype%3Afeature+-label%3Atype%3Adocs+-label%3Atype%3Arefactor+-label%3Atype%3Aci) can identify any which are missing one.

### Detector

Add the relevant `detector:` labels to the issue.
If there are multiple detectors affected, add labels for all of them.

### Version

<details>
    <summary>Version labels</summary>

    version:major
    version:minor
    version:patch

</details>

We use [release drafter](https://github.com/release-drafter/release-drafter) to automatically create new releases.
It generates the next version based on labels of the PRs since the last release.
If no label is applied the default is `version:patch`.

### Housekeeping

<details>
    <summary>Housekeeping</summary>

    good first issue
    help wanted
    duplicate

</details>

Add a label `good first issue` to issues that are small, easy to fix, and do-able for a newcomer.
This label is sometimes picked up by tools or websites that try to encourage people to contribute to open source.

Add the label `help wanted` to indicate that we need the original poster or someone else to do some work or it is unlikely to get done.

Add a label `duplicate` to issues/PRs that are a duplicate of an earlier issue/PR.
