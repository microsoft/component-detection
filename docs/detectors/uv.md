# uv Detection
## Requirements
[uv](https://docs.astral.sh/uv/) detection relies on a [uv.lock](https://docs.astral.sh/uv/concepts/projects/layout/#the-lockfile) file being present.

## Detection strategy
uv detection is performed by parsing a <em>uv.lock</em> found under the scan directory.
