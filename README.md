# Prelude

Applies [Harmony](https://github.com/pardeike/Harmony) patches by rewriting IL on disk before
the CLR loads the target assembly, instead of detouring methods at runtime.

The library is `PreludeLib`; the solution is `Prelude.sln`.

## Why

Harmony patches a live method: it compiles a replacement and detours the original, so the
method's code moves after the process is already running. Black Myth: Wukong caches native
pointers to managed methods. If it cached a pointer before a patch was applied, that pointer
was left aimed at code Harmony had replaced, which is how the mod picked up crashes that were
essentially impossible to reproduce on demand.

Prelude sidesteps the race rather than trying to win it. Patch call sites are woven into the
target assembly before it is ever loaded, so the method body is final before the JIT sees it
and its address never changes for the lifetime of the process.

## How

Two halves, either side of assembly load.

**Compile time** (`CompileTimePrelude`, Mono.Cecil). Scans mod assemblies for Harmony patch
attributes, resolves each target, and rewrites the target's IL. For every patched method it
generates a companion type next to it:

```
<Container>__<Method>__Callback          // holds a Callback event
<Container>__<Method>__DelegateType      // its delegate signature
```

The woven call site raises that event at the appropriate point in the method body. Patched
assemblies are then written back out.

**Runtime** (`RuntimePrelude`). Resolves the generated callback type by name, finds its
`Callback` event, and subscribes the mod's patch method to it. Enabling or disabling a patch is
an event subscription, not a code rewrite, so nothing is detoured at any point.

In [wukong-modloader](https://github.com/readycodeio/wukong-modloader) the weaving step runs in
`ReadyM.Loader.Wukong.Bootstrap`, which preprocesses every enabled mod's assemblies during
loader startup, before the game's managed assemblies are handed to the runtime.

## Runtime backends

`IRuntimeBackend` decides how a registered patch is actually applied:

| backend | |
|---|---|
| `RuntimeWeaverBackend` | subscribes to the woven callback events. The reason this library exists |
| `RuntimeHarmonyBackend` | classic Harmony detours, one `Harmony` instance per id, supports unpatching |
| `RuntimeDummyBackend` | no-op, for tests |

## Harmony compatibility

Patches are ordinary Harmony patches. `prefix`, `postfix` and `finalizer` are supported, with
the usual injected parameters: `__instance`, `__originalMethod`, `__args`, `__result`
including by-ref, `__state`, `__exception`, `__runOriginal`. `In`, `Out` and `Ref` narrow how
an argument is passed, `HarmonyTargetMethodWithArgs` disambiguates overloads, and `Category`
groups patches so they can be committed selectively.

**Transpilers are not supported.** There is no live IL stream to hand a transpiler when the
patch is a woven call site, so `Patch` throws `NotSupportedException` for them.

## Build

```bash
git clone --recursive https://github.com/readycodeio/harmony-prepatcher.git
dotnet build Prelude.sln
```

`--recursive` matters: `src/Harmony` is our fork of Harmony, and `PreludeLib` builds against it
rather than the published package. Targets `net472`, `netstandard2.0` and `net8.0`.

Tests under `tests/` run the whole path end to end: a payload assembly is woven by
`PreludeLib.Tests.Preprocess`, loaded, and then checked against the expected patch behaviour.
