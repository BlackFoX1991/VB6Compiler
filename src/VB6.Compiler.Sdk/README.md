# VB6 Compiler MSBuild SDK

The SDK supplies a small MSBuild contract for projects that keep their VB6 project file as the
source of truth. Set `VB6CompilerPath` to a published `vb6c` executable and import this SDK from a
modern SDK-style project:

```xml
<Project Sdk="VB6.Compiler.Sdk/1.0.0">
  <PropertyGroup>
    <VB6Project>$(MSBuildProjectDirectory)\LegacyApp.vbp</VB6Project>
    <VB6CompilerPath>$(RepoRoot)\tools\vb6c.exe</VB6CompilerPath>
  </PropertyGroup>
</Project>
```

The target delegates project parsing and emission to the compiler CLI. The target is incremental:
the `.vbp`, source files (`.bas`, `.cls`, `.frm`, `.ctl`, `.pag`, `.dob`) and designer resources
(`.frx`, `.res`) are inputs; the emitted assembly, PDB, runtimeconfig and `VB6.Runtime.dll` are
outputs. An unchanged project is skipped by MSBuild, while a changed legacy source or designer
resource triggers a new compile. It does not add a designer or replace the `.vbp` project model.

Set `VB6EnableComHosting=true` for a library project to pass `--com-host` and produce the adjacent
native .NET `*.comhost.dll` artifact. After emission, register or remove that artifact for classic
COM consumers with `vb6c path\Library.comhost.dll --register-com --x86` or
`--unregister-com`. COM hosting is limited to Managed library output.

For a Visual Basic 6 project group, set `VB6ProjectGroup` instead of `VB6Project`:

```xml
<Project Sdk="VB6.Compiler.Sdk/1.0.0">
  <PropertyGroup>
    <VB6ProjectGroup>$(MSBuildProjectDirectory)\LegacySuite.vbg</VB6ProjectGroup>
    <VB6CompilerGroupOutputDirectory>$(TargetDir)legacy</VB6CompilerGroupOutputDirectory>
    <VB6CompilerPath>$(RepoRoot)\tools\vb6c.exe</VB6CompilerPath>
  </PropertyGroup>
</Project>
```

The group target invokes `vb6c <group>.vbg --emit-assembly <output-directory>` and emits each
declared `.vbp` into that directory in dependency order. The group file, project files, source
files and designer resources are tracked as MSBuild inputs; a group compile stamp makes unchanged
groups incremental. A companion output manifest invalidates the stamp when a previously emitted
assembly, apphost, runtime file, PDB or runtime configuration is missing, so incomplete output
directories are repaired by the next build. `VB6EnableComHosting=true` remains available for
library projects in the group.
