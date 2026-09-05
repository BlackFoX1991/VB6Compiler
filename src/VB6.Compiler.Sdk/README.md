# VB6 Compiler MSBuild SDK

The SDK supplies a small MSBuild contract for projects that keep their VB6 project file as the
source of truth. Set `VB6CompilerPath` to a published `vb6c` executable and import this SDK from a
modern SDK-style project:

```xml
<Project Sdk="VB6.Compiler.Sdk/1.0.0">
  <PropertyGroup>
    <VB6Project>$(MSBuildProjectDirectory)\LegacyApp.vbp</VB6Project>
    <VB6CompilerPath>$(RepoRoot)\tools\vb6c.exe</VB6CompilerPath>
    <VB6TargetPlatform>x86</VB6TargetPlatform>
    <VB6CompatibilityProfile>deterministic</VB6CompatibilityProfile>
  </PropertyGroup>
</Project>
```

Emission is delegated to the compiler CLI. Before compilation, `ResolveVB6Project` or
`ResolveVB6ProjectGroup` uses the packaged `WriteVB6InputManifest` task for an exact input
manifest; the CLI resolver is the fallback when that task is unavailable or disabled. The
manifest contains the `.vbp`/`.vbg`, only the declared source and designer files (including `.frx`
sidecars), `RESFILE` resources and files named by `Reference=`/`Object=` entries. Each existing
file is fingerprinted with SHA-256; missing declared files remain visible as `MISSING` entries.
Undeclared files in the project directory are therefore ignored instead of being picked up by a
recursive glob. The manifest itself is the MSBuild input, so a changed declared file triggers a
compile while an unrelated file does not. The SDK-style wrapper project is tracked alongside the
manifest so changing `VB6CompilerOutput` or another build property also reconciles stale outputs.
It does not add a designer or replace the `.vbp` project model.
The SDK fails the build when the configured `.vbp` or `.vbg` does not exist, and when both project
properties are supplied; this prevents a normal .NET build from silently replacing the requested
VB6 compilation.
Single projects use a compile stamp and output manifest, just like project groups, so stale runtime
copy timestamps do not force a rebuild and deleted generated artifacts are repaired automatically.
`GetVB6ProjectOutputs` exposes the stable single-project output set (TargetPath, PDB, runtime,
runtimeconfig and optional TypeLib/COM-host/manifest files); `GetVB6ProjectGroupOutputs` exposes the
previously emitted group manifest when one exists.
The SDK also hooks `Clean` for single projects and project groups: it reads the last output
manifest, deletes every generated artifact plus the input/output manifests and compile stamp, and
therefore makes the standard `Rebuild` target deterministic without deleting unrelated files.

Legacy projects default to the x86 target because classic ActiveX controls are commonly 32-bit.
Set `VB6TargetPlatform` to `x64` or `anycpu` to opt into another managed target; values other than
`x86`, `x64` and `anycpu` fail before the compiler is invoked. The setting applies to single projects
and project groups.

`VB6CompatibilityProfile` defaults to `deterministic`, which preserves the compiler's existing
managed behavior. Set it to `vb6-sp6` to opt into the documentation-based VB6 SP6 compatibility
contract; this profile selects x86 and fails validation for `x64` or `anycpu`. Because VB6 is not
installed in the development environment, this profile is verified against the published VB6
contract rather than a local VB6 oracle.

Set `VB6EnableComHosting=true` for a library project to pass `--com-host` and produce the adjacent
native .NET `*.comhost.dll` artifact. Set `VB6EnableComManifest=true` as well (or by itself) to
also emit a side-by-side `*.manifest` that maps the generated CLSIDs without registry changes.
After emission, register or remove that artifact for classic
COM consumers with `vb6c path\Library.comhost.dll --register-com --x86` or
`--unregister-com`. COM hosting also emits the adjacent `.tlb`, tracked by the SDK output
manifest and Clean/Rebuild. ActiveX EXE projects use the existing local-server path; side-by-side
COM manifests apply to libraries and are rejected for local servers. Full signature metadata
and compatibility with the declared older binary remain R4 work.

For a Visual Basic 6 project group, set `VB6ProjectGroup` instead of `VB6Project`:

```xml
<Project Sdk="VB6.Compiler.Sdk/1.0.0">
  <PropertyGroup>
    <VB6ProjectGroup>$(MSBuildProjectDirectory)\LegacySuite.vbg</VB6ProjectGroup>
    <VB6CompilerGroupOutputDirectory>$(TargetDir)legacy</VB6CompilerGroupOutputDirectory>
    <VB6CompilerPath>$(RepoRoot)\tools\vb6c.exe</VB6CompilerPath>
    <VB6TargetPlatform>x86</VB6TargetPlatform>
  </PropertyGroup>
</Project>
```

The group target invokes `vb6c <group>.vbg --emit-assembly <output-directory>` and emits each
declared `.vbp` into that directory in dependency order. Its exact manifest follows every declared
project and dependency; a group compile stamp makes unchanged groups incremental. A companion
output manifest invalidates the stamp when a previously emitted assembly, apphost, runtime file,
PDB or runtime configuration is missing, so incomplete output directories are repaired by the next
build. `VB6EnableComHosting=true` remains available for library projects in the group.

`DesignTimeBuild=true` still validates the configured project/group and resolves no compiler output;
the compile targets are skipped, which keeps the SDK usable by headless design-time callers without
requiring Visual Studio or the VB6 IDE.

## Completion status

The packaged resolver task, exact manifests, incremental builds, DesignTimeBuild, Clean/Rebuild
and TypeLib output tracking are implemented. R6 in [the roadmap](../../docs/ROADMAP.md)
covers their joint application/deployment acceptance; these existing capabilities are not open
implementation tasks. Language-semantic corrections planned for R1–R5 apply to both profiles.
