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

The target delegates project parsing and emission to the compiler CLI. It does not add a designer
or replace the `.vbp` project model.
