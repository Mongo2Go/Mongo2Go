# Mongo2Go — Working Agreement

Mongo2Go ships **executable MongoDB binaries** (`mongod`, `mongoimport`, `mongoexport`) inside its
NuGet package. Every consumer runs those binaries on their machine or CI. There is no sandbox
between what lands in `tools/` and a user's laptop.

That makes this repository a supply-chain target, and it makes the maintainers the last line of
defence. The rules below are not style preferences. They are the security model.

---

## RULE 1 — Zero trust for binaries. No exceptions.

**We never accept a binary from anyone but ourselves.**

- A contributor PR must **never** introduce or modify anything under `tools/`. Not a new
  executable, not a "rebuilt" one, not a "just updated the version" one.
- If someone proposes a MongoDB upgrade, we **discard their artifacts entirely** and re-download
  from MongoDB's own distribution endpoints using `src/MongoDownloader`. We take the *intent* of
  the request, never the *bytes*.
- Code review cannot detect a backdoored 30 MB stripped ELF. Only provenance can. Do not pretend
  otherwise, and do not approve on the basis of "the diff looks fine."
- CI enforces this (`guard-binaries` job). If the guard ever blocks a legitimate maintainer
  change, fix the process — **do not weaken or bypass the guard.**

Legitimate binary updates are pushed to a branch directly by a maintainer (push event), never
merged from a fork PR.

## RULE 2 — Never trust a contributor-supplied `packages.lock.json`

`packages.lock.json` is a **trust anchor**, not a text file. CI restores with `--locked-mode` and
pins to whatever `contentHash` values it contains. A contributor-supplied hash is a
contributor-supplied trust anchor.

When a PR touches a lockfile:

```bash
dotnet restore --force-evaluate    # regenerate from nuget.org ourselves
git diff --exit-code -- '**/packages.lock.json'
```

Identical → they were honest, and we lost nothing. Different → that *is* the finding. CI enforces
this too (`verify-lockfile` job).

Note: only `Mongo2Go.csproj` currently sets `RestorePackagesWithLockFile`. Adding it to
`MongoDownloader.csproj` is desirable — it is our provenance tool and should be hash-pinned.

## RULE 3 — Audit every new dependency before adopting it

Minimum checklist, verified with real commands, not assumptions:

- [ ] Provenance chain: nuspec `repository commit` == PDB SourceLink commit == a real,
      ideally GPG-verified commit in the public repository
- [ ] Licence is genuinely permissive — read the bundled `LICENSE`, do not trust the label
- [ ] **No build-time hooks**: no `.targets`, `.props`, `.ps1`, `.sh` in the package.
      These execute during build and are the cheapest attack vector
- [ ] Transitive dependency surface is understood and minimal
- [ ] Static check of the shipped assembly for capability it has no business having
      (network, process spawn, registry, dynamic payload decoding)
- [ ] `dotnet list package --vulnerable --include-transitive` is clean
- [ ] No open security advisories
- [ ] Package is signed, and note whether author-signed or only repository-signed

## RULE 4 — No dependency version younger than ~30 days

A freshly published release has had **zero community exposure**. That window is exactly when a
compromised publish goes undetected. Prefer the previous stable release.

Override only for a security fix that forces it — and say so explicitly in the commit message.

## RULE 5 — Verify, never assert

Never state something as fact unless it was directly observed by running a command and reading
the output.

- Do not invent plausible-sounding explanations to fill gaps. "Unknown" is a valid answer.
- If a check produces a surprising result, **suspect your own check first.** Stale
  `project.assets.json` after a branch switch, a broken subshell, a `$` anchor defeated by CRLF
  or a UTF-8 BOM — all of these have produced false findings in this repo.
- Distinguish "we observed X" from "X happens because Y".

---

## Repository map

| Path | Purpose |
|---|---|
| `src/Mongo2Go/` | The shipped library (net472 + netstandard2.1) |
| `src/MongoDownloader/` | **Provenance tool.** Downloads MongoDB binaries into `tools/` |
| `src/Mongo2GoTests/` | Tests. `IsPackable=false` — never ships |
| `tools/` | Vendored MongoDB binaries, packed into the nupkg |

`Mongo2Go.csproj` packs `tools/` via:

```xml
<None Include="../../tools/mongodb*/**" Visible="false">
  <Pack>true</Pack>
  <PackagePath>tools</PackagePath>
</None>
```

Whatever sits in `tools/` at pack time is what every consumer executes.

## Release process

Releases are tag-triggered and **publish straight to nuget.org**. See `README_INTERNAL.md`.

Because `main` is one `git tag vX.Y.Z` away from being published, **`main` must always be
releasable.** Do not merge anything unverified into it.

## Known context

- Shipped MongoDB binaries are **4.4.4**; latest 4.4.x is **4.4.31**. Upgrading within 4.4 is
  non-breaking and picks up years of security fixes.
- Upgrading to 8.x is a **major** version bump. It would resolve the `libcrypto.so.1.1` issues
  (#149, #135) and let the `libssl1.1` workaround be removed from CI.
- The committed binaries are **stripped derivatives**, so they cannot be checked against
  MongoDB's published archive checksums directly. Establishing provenance means re-downloading,
  verifying the archive sha256, and comparing.
- `MongoDownloader` currently always fetches the *latest production* release and **deletes every
  subdirectory of `tools/` before downloading**. It is destructive; git is the only undo.
