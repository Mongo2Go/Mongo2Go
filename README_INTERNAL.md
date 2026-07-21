# Mongo2Go - Knowledge for Maintainers

## Creating a Release

Mongo2Go uses [MinVer](https://github.com/adamralph/minver) for versioning.
Releases are fully automated via GitHub Actions and triggered by tagging a commit with the desired semantic version number.
This process involves two steps to ensure reliable deployments.

### Steps to Create a Release

1. **Push Your Changes**
   - Commit and push your changes to the main branch. This will trigger a CI build to validate the changes.
     ```bash
     git commit -m "Your commit message"
     git push
     ```

2. **Wait for the CI Build**
   - Ensure that the GitHub Actions workflow completes successfully. This confirms your changes are valid.

3. **Tag the Commit**
   - Once the CI build passes, create a lightweight tag with the desired version number
   - Use an **annotated tag** to ensure the release is properly versioned and auditable (`-a` flag):
     ```bash
     git tag -a v4.0.0
     ```
   - Push the tag to trigger the deployment workflow:
     ```bash
     git push --tags
     ```

4. **Draft Release Created**
   - The workflow will:
     1. Create a multi-target NuGet package.
     2. Publish the package to nuget.org.
     3. Create a **draft release** on GitHub with a placeholder note.

5. **Review and Finalize the Release**
   - Visit the [Releases page](https://github.com/Mongo2Go/Mongo2Go/releases).
   - Open the draft release, update the release notes with details about the changes (e.g., changelog, features, fixes), and publish the release manually.


## Workflow Details

- **Two-Step Process**:
  1. The first push (commit) triggers a CI build to validate the changes.
  2. The second push (tag) triggers the deployment workflow.

- **Triggers**:
  - Commits are validated for all branches.
  - Pull requests are validated too, including those from forks.
  - Tags starting with `v` trigger deployment.

- **Draft Releases**:
  - Releases are created as drafts, allowing maintainers to review and add release notes before publishing.

- **Automation**:
  - The workflow automates building, testing, publishing to nuget.org, and creating a draft GitHub release.


## The Bundled MongoDB Binaries

Mongo2Go ships `mongod`, `mongoimport` and `mongoexport` inside the NuGet package. Everyone who
installs Mongo2Go executes those binaries. There is no sandbox between what sits in `tools/` and a
consumer's machine, which makes this the most security-sensitive part of the repository.

**We never accept binaries from anyone but ourselves.** A contributor pull request must never add or
modify anything under `tools/`. If someone proposes a MongoDB upgrade we take the *request* and
discard the *bytes*, re-downloading from MongoDB ourselves. No code review can spot a backdoor in a
stripped 80 MB executable; only provenance can. See RULE 1 in `CLAUDE.md`.

### Updating the binaries

```bash
cd src/MongoDownloader
dotnet run
```

This deletes everything under `tools/`, downloads the MongoDB release, extracts only the three
executables (no licence files — those terms are reproduced in `README.md` instead, to keep the
package under the 250 MiB nuget.org limit), strips them, re-signs the macOS arm64 binary ad-hoc so
it will run on Apple Silicon, writes the checksum manifest, and finally writes a gzip copy
(`<binary>.gz`) of each executable. **Only the `.gz` files are committed** — GitHub rejects files
over 100 MB and the 8.x `mongod` binaries are larger — and the build unpacks them on demand (the
`PrepareMongoBinaries` target in `Mongo2Go.csproj`); the decompressed executables are git-ignored.
Commit the `tools/**/*.gz` files and `src/Mongo2Go/MongoBinaries.sha256` **together**, then push
directly to a branch — the CI guard rejects binary changes arriving through a pull request, which
is the intended behaviour.

Stripping needs `llvm-strip` (`brew install llvm`, `apt-get install llvm`, `scoop install llvm`).
Use `--no-strip` to skip it, but note the result is what ships, so the package grows.

Two things to know before running it:

- By default it fetches the **latest production release**. To reproduce or pin an exact version —
  as the 8.0.26 release does — pass `--server-version 8.0.26 --tools-version 100.14.0`.
- It resolves `tools/` by walking **up from the current working directory**. Run it from inside the
  repository.

### The trust chain

Four links, each verified, so that no single compromise is sufficient:

| Link | Mechanism | Enforced by |
|---|---|---|
| MongoDB's release → the archive we download | SHA-256 published in MongoDB's release JSON, checked before a single entry is read; mismatch deletes the file and aborts | `MongoDbDownloader.VerifyChecksum` |
| The archive → what lands in `tools/` | Every archive entry must resolve inside the extraction directory | `ArchiveExtractor.ResolveContainedFile` |
| `tools/` → the committed manifest | Set comparison of every bundled binary against `MongoBinaries.sha256` | CI job `verify-binary-manifest` |
| The manifest → what actually runs | Binaries found by the default search are used only if their checksum matches | `MongoBinaryManifest` |

The last link is why the search can look in many places without that being a risk. `MongoBinaryLocator`
walks upward from several starting points toward the filesystem root, so it can reach directories we
do not control. Rather than deciding which *locations* are trustworthy, we check the *contents*: a
candidate that fails is skipped and the search continues, and every rejection is logged through the
`ILogger` passed to `MongoDbRunner.Start`. Where the binaries were found stops mattering.

Binaries the caller supplies explicitly, via `binariesSearchDirectory` or
`binariesSearchPatternOverride`, are used **without** verification. Pointing Mongo2Go at your own
MongoDB is supported — it is the existing workaround for a newer server (#132) and for native arm64
(#127) — and those binaries will legitimately not match our manifest.

### The checksum manifest

`src/Mongo2Go/MongoBinaries.sha256` is embedded in the assembly and lists the SHA-256 of every
bundled binary. **It is generated, not hand-written.** `dotnet run` writes it as the last step of a
download. To repair it without re-downloading several hundred megabytes:

```bash
dotnet run --project src/MongoDownloader -- --write-manifest
```

A file name maps to a *set* of checksums, because a platform can ship more than one architecture and
both are legitimately ours.

If `tools/` and the manifest ever disagree, Mongo2Go rejects its own binaries — and the failure
appears on a consumer's machine, not here. That is what `verify-binary-manifest` exists to prevent.

### CI guards

| Job | Blocks |
|---|---|
| `guard-binaries` | Any pull request touching `tools/` |
| `verify-lockfile` | A `packages.lock.json` whose hashes differ from what nuget.org serves when regenerated independently |
| `verify-binary-manifest` | `tools/` and `MongoBinaries.sha256` drifting apart, on push as well as pull request |

Do not weaken or bypass these. If one blocks a legitimate change, fix the process instead.

> **Note:** these jobs only *block a merge* once they are configured as required status checks in the
> branch protection settings. Verify that they are.

## Best Practices for Maintainers

- **Semantic Versioning**: Ensure that tags follow the [semantic versioning](https://semver.org/) format (`vMAJOR.MINOR.PATCH`).
- **Pre-Releases**: Use pre-release tags for non-final versions (e.g., `v4.0.0-rc.1`).
- **Detailed Release Notes**: Always add detailed information to the GitHub release, highlighting major changes, fixes, and improvements.
- **Final Review**: Review the draft release to ensure all details are correct before publishing.
- **Dependency Age**: Do not adopt a dependency version younger than about 30 days. A fresh release
  has had no community exposure, which is exactly when a compromised publish goes unnoticed. Override
  only for a security fix, and say so in the commit message.
- **Lockfiles**: Never trust a contributor-supplied `packages.lock.json`. Regenerate it with
  `dotnet restore --force-evaluate` and compare — identical means they were honest, different is the
  finding.

