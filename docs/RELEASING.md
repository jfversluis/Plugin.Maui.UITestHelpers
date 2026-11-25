# Publishing releases

The [release workflow](../.github/workflows/release-nuget.yml) publishes all four packages using
[NuGet Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing).
Before releasing, sign in to NuGet.org as `jfversluis` and create a Trusted Publishing policy
for repository owner `jfversluis`, repository `Plugin.Maui.UITestHelpers`, and workflow file
`release-nuget.yml` (filename only). Leave the environment empty because this workflow does not
use a GitHub Actions environment. Select the owner of the packages and allow pushing new
packages and package versions with package scope `Plugin.Maui.UITestHelpers.*`. Pushing new
packages is required whenever a release adds a package ID that is not on NuGet.org yet.

Push a tag such as `v1.2.3` or `v1.2.3-preview1` on a commit in `main` to release.
The workflow exchanges its GitHub OIDC token for a short-lived NuGet API key; no
`NUGET_API_KEY` repository secret is required. After a successful trusted publish, remove
the old repository secret and revoke its NuGet API key if it is no longer used elsewhere.
