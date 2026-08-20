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

`ConformanceCorpusTests` runs the same analysis in CI. It asserts that the compiler survives the
input, that the number of cleanly analyzed files never drops, and that parser errors never rise
above their current baseline — see the comments there for why the total error count is
deliberately not asserted.

Clean files are the honest long-term metric but sit at zero until whole dependency chains parse,
so parser errors carry the ratchet in the meantime. They have fallen at every slice so far: 3183
at M0, 1758 at the M2 closeout, 1214 after the UDT type space, 480 after `With` and member access.
