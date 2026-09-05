# Conformance corpus

Real VB6 projects the compiler is measured against. They are test input only — no compiler code
is derived from them, and nothing here is built or shipped as part of VB6Compiler.

Their job is to keep the project honest. Hand-written test cases only cover constructs somebody
already thought of; a real codebase surfaces the ones nobody did. The feature order in
`docs/ROADMAP.md` comes from measuring this corpus rather than from a generic VB6 feature list.

## VISIA 4.8.7.1

An IDE and compiler for the Linley language, itself written in VB6. Third-party software,
included here with the understanding that it is free to use. Not authored by this project.

It is a deliberately hard target: a systems program with heavy bit manipulation, 234 Win32 API
`Declare`s, binary file I/O, user-defined types, dynamic arrays, class modules with properties
and events, and four of its own ActiveX UserControls. 10,152 lines across 42 source files.

Kept unmodified, including IDE artifacts and resources, so that it stays a faithful sample of
what a real `.vbp` looks like.

## Measuring

```text
vb6c conformance/VISIA/4.8.7.1/prjVisia.vbp --report
```

`ConformanceCorpusTests` runs the same analysis in CI. The measured baseline on 2026-09-05 is
**40/40 project items, 0 diagnostics**: 27 modules, 6 forms, 4 UserControls and 3 classes. These
are the declared project items; the corpus directory contains additional source files.
The tests assert this baseline, including zero total diagnostics, and also emit an assembly and
Portable PDB for the complete project. Historical parser-error measurements remain in the changelog.

Successful analysis and emission do not establish that the complete application behaves correctly.
R6 in [the roadmap](../docs/ROADMAP.md) adds explicit runtime scenarios: startup, opening a project,
central windows/controls, file operations and shutdown, with fixed output/file/event expectations.
VISIA remains unchanged. A planned repo-owned VB6 business-logic reference project will complement
it with Forms, classes, file I/O and ADO; that fixture will not be described as third-party corpus.
