# Releasing Suwagcms.Sdk and Suwagcms.Cli

Both packages are released together, under one version, so a CLI and an SDK of the same number belong together.

The owner or an agent releases it, from a commit of `master` whose CI is green: `just sdk-publish 0.1.0` runs the workflow sdk.yml on `master`
(`gh workflow run`) and says how to follow the run; it stops first on a bad version, another branch, uncommitted changes or a `master` that
differs from the remote master. The workflow refuses anything but `master`, checks the version, refuses a commit whose CI run has not
passed (no green tree stamp of its tree, the commit status suwgate's gate workflow writes after a green run: the whole gate, SDK tests
included, ran on exactly that tree, so the release does not test it again; wait for the run and start the release again), packs both packages at that version (`-p:Version=`, built with `ContinuousIntegrationBuild`,
so the symbols hold no local paths) and pushes them, the SDK with its `.snupkg`, to nuget.org. A version on nuget.org can never be deleted, only
unlisted, so a bad release is followed by the next version, never replaced. The csproj says `0.0.0-local`, which is what a pack without a
version produces.

**Why in CI, with no key.** [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) is how nuget.org gives
a key to a CI job instead of to a person: the job's OIDC token is exchanged (the NuGet login action) for a key that lives one hour, so the
repository holds no key to leak or rotate, and only the job that pushes, which runs no code of the repository, can ask for one.

**One-time setup.** On nuget.org, *Trusted Publishing*: a policy with publisher GitHub Actions, owner `Manomenu`, repository `suwagcms`,
workflow file sdk.yml (the file's name, so renaming the file breaks releases), no environment, scope to push new packages and package
versions, glob `Suwagcms.*` (it covers both packages). In this repository, the secret `NUGET_USER`: the nuget.org profile name of the policy's package owner, which is
not the GitHub owner and not an email address.

**A private repository activates the policy for 7 days only.** nuget.org needs the repository's and owner's ids from a first successful
publish to lock the policy to them. A policy with no publish in those 7 days turns inactive; the window can be restarted at any time on the
same page. After the first successful publish it stays active.

**The fallback, `just sdk-publish-local <version>`,** packs and pushes from the machine with a key from the gitignored `.env`, and is not the
normal way. It stays for one case: nuget.org refuses the workflow's first push of the package. Then set `SDK_NUGET_API_KEY` to a key from
nuget.org, API Keys, Create: scope Push (new packages and package versions), glob `Suwagcms.*`, and remove the key once that release is
out. Run it on a clean `master` too: it does not check that for you.
