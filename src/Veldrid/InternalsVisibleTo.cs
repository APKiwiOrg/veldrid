using System.Runtime.CompilerServices;

// Fork addition. The per-draw bookkeeping types that back resource set binding are internal and have no
// device-free public surface, so the headless unit tests that cover them need to see inside the assembly.
[assembly: InternalsVisibleTo("Veldrid.Tests")]
