# HarmonyWeaver

A library that searches for `[HarmonyPatch]` attributes and uses Mono.Cecil to weave Prefix, Postfix, and Finalizer methods by directly applying changes to target .dll IL files.

## Goals

HarmonyWeaver provides a static patching solution that applies Harmony-style patches directly to .dll files at build time or deployment time, rather than at runtime. This approach offers several advantages:

- **Performance**: No runtime overhead from Harmony's dynamic patching
- **Compatibility**: Works with AOT scenarios where runtime IL generation may be restricted
- **Deployment**: Pre-patched assemblies can be deployed without requiring the patching framework
- **Analysis**: Static analysis tools can examine the patched code

## Architecture

The library is divided into four main components:

### 1. HarmonyWeaver.Core
The main library containing:
- **Interfaces**: `IAssemblyLoader`, `IPatchScanner`, `IILWeaver`, `IAssemblySaver`, `IHarmonyWeaver`, `IRuntimeAssemblyLoader`
- **Models**: `PatchInfo`, `HarmonyPatchAttribute`, `PatchMethodInfo`, `PatchParameterInfo`
- **Implementation**: Default implementations of all interfaces
- **Main API**: `HarmonyWeaver` class that orchestrates the entire process

### 2. HarmonyWeaver.Examples
Contains example classes that serve as patch targets for testing and demonstration:
- `Calculator`: Simple math operations
- `StringProcessor`: String manipulation methods

### 3. HarmonyWeaver.Tests.Patches
Contains patch classes with `[HarmonyPatch]` attributes:
- `CalculatorPatches`: Patches for Calculator methods
- `StringProcessorPatches`: Patches for StringProcessor methods

### 4. HarmonyWeaver.Tests
Unit and integration tests for the library functionality.

## Implementation Plan

### Phase 1: Core Infrastructure ✅
- [x] Project structure setup
- [x] Core interfaces and models
- [x] Basic implementations with stubs
- [x] Example classes and patches
- [x] Initial test framework

### Phase 2: HarmonyPatch Attribute Discovery 🚧
- [ ] Parse HarmonyPatch attributes from assemblies
- [ ] Support for different target specification methods:
  - Type name strings
  - Direct type references
  - Dynamic type resolution methods
- [ ] Method resolution with overload support
- [ ] Parameter analysis for patch methods

### Phase 3: IL Weaving Implementation 🚧
- [ ] Prefix method weaving
  - Inject calls at method start
  - Handle return value to skip original method
  - Pass correct arguments (__instance, parameters)
- [ ] Postfix method weaving  
  - Inject calls before method returns
  - Pass __result and other parameters
  - Handle result modification
- [ ] Finalizer method weaving
  - Wrap methods in try-catch blocks
  - Call finalizers in exception handlers
  - Pass __exception parameter

### Phase 4: Advanced Features 🚧
- [ ] Support for special parameter types:
  - `__instance`: The instance being patched
  - `__result`: Method return value
  - `__originalMethod`: Reference to original method
  - `__args`: Array of all arguments
  - `__exception`: Exception thrown (finalizers)
  - `__state`: State object for patch communication
  - `__runOriginal`: Control original method execution
- [ ] Generic method support
- [ ] Constructor and property patching
- [ ] Multiple patches per method

### Phase 5: Testing and Validation 🚧
- [ ] Comprehensive unit tests
- [ ] Integration tests with real assemblies
- [ ] Performance benchmarks
- [ ] Error handling and validation

### Phase 6: Documentation and Examples 🚧
- [ ] Complete API documentation
- [ ] Usage examples and tutorials
- [ ] Best practices guide
- [ ] Migration guide from runtime Harmony

## Current Status

### ✅ Completed
- Basic project structure with 4 projects
- Core API design with interfaces and models
- Stub implementations of all major components
- Example classes for testing (Calculator, StringProcessor)
- Example patches using HarmonyPatch attributes
- Basic unit test framework
- Development environment setup

### 🚧 In Progress
- README documentation and project overview

### ⏳ Todo
- HarmonyPatch attribute parsing and discovery
- Target type and method resolution
- IL weaving implementation for Prefix/Postfix/Finalizer
- Parameter analysis and argument passing
- Comprehensive testing
- Performance optimization

## Usage Example (Planned)

```csharp
using HarmonyWeaver.Core;
using HarmonyWeaver.Core.Implementation;

// Create the weaver
var assemblyLoader = new AssemblyLoader();
var runtimeLoader = new RuntimeAssemblyLoader(); // For runtime isolation when needed
var patchScanner = new PatchScanner();
var ilWeaver = new ILWeaver();
var assemblySaver = new AssemblySaver();

using var weaver = new HarmonyWeaver(assemblyLoader, patchScanner, ilWeaver, assemblySaver);

// Process patches
var patchedFiles = weaver.ProcessPatches(
    patchAssemblyPaths: new[] { "MyPatches.dll" },
    targetAssemblyPaths: new[] { "TargetLibrary.dll" },
    outputDirectory: "patched_output"
);

Console.WriteLine($"Created patched assemblies: {string.Join(", ", patchedFiles)}");
```

### Optional: Runtime loading without AppDomain (for testing/inspection)

When you need to load both original and patched assemblies in the same process (e.g., to validate behavior), do not rely on `AppDomain` (deprecated in modern .NET). Use `IRuntimeAssemblyLoader` which is based on `AssemblyLoadContext`:

```csharp
using HarmonyWeaver.Core.Implementation;
using System.Runtime.Loader;

var runtimeLoader = new RuntimeAssemblyLoader();

// One context for original assemblies
var originalCtx = runtimeLoader.CreateContext(
    name: "original",
    probingPaths: new[] { "/path/to/originals", "/path/to/shared/deps" },
    isCollectible: true,
    preferDefaultLoad: true);

// Another context for patched assemblies
var patchedCtx = runtimeLoader.CreateContext(
    name: "patched",
    probingPaths: new[] { "/path/to/patched", "/path/to/shared/deps" },
    isCollectible: true,
    preferDefaultLoad: true);

var original = runtimeLoader.LoadFromPath(originalCtx, "/path/to/originals/TargetLibrary.dll");
var patched = runtimeLoader.LoadFromPath(patchedCtx, "/path/to/patched/TargetLibrary.dll");

// ... run tests via reflection ...

runtimeLoader.Unload(originalCtx);
runtimeLoader.Unload(patchedCtx);
```

This ensures the runtime doesn't resolve to a previously loaded assembly by identity, avoiding the need to rename assemblies. Prefer separate `AssemblyLoadContext`s to isolate bindings and keep the default load context clean.

## API Design

### Core Workflow
1. **Load** patch assemblies using `IAssemblyLoader`
2. **Scan** for HarmonyPatch attributes using `IPatchScanner`
3. **Resolve** target types and methods in target assemblies
4. **Weave** IL code using `IILWeaver` to inject patch calls
5. **Save** modified assemblies using `IAssemblySaver`

### Key Classes

#### PatchInfo
Represents a complete patch with target method and patch methods (Prefix/Postfix/Finalizer).

#### HarmonyPatchAttribute  
Parsed information from `[HarmonyPatch]` attributes including target type, method name, and parameters.

#### PatchMethodInfo
Details about individual patch methods including parameter analysis.

## Known Issues

- **Stub Implementation**: Current implementations are mostly stubs and need full implementation
- **Attribute Parsing**: HarmonyPatch attribute parsing not yet implemented
- **IL Weaving**: Core IL manipulation functionality not yet implemented
- **Target Resolution**: Dynamic type/method resolution not implemented
- **Testing**: Need integration tests with actual compiled assemblies

## Dependencies

- **.NET 8.0**: Target framework
- **Mono.Cecil 0.11.5**: For IL manipulation
- **Lib.Harmony 2.3.3**: For HarmonyPatch attribute definitions (patches project only)
- **xUnit**: Testing framework

## Contributing

This project is in early development. Key areas needing implementation:

1. **HarmonyPatch Attribute Parser**: Parse attributes from Cecil TypeDefinitions
2. **Target Resolution**: Find target types/methods in assemblies, including dynamic resolution
3. **IL Weaver**: Core IL manipulation to inject patch calls
4. **Parameter Handling**: Analyze and pass special parameters like __instance, __result
5. **Testing**: Create comprehensive tests with real scenarios

## Development Setup

```bash
# Clone and build
git clone <repository>
cd HarmonyWeaver
dotnet restore
dotnet build

# Run tests
dotnet test

# Build examples
dotnet build examples/HarmonyWeaver.Examples
dotnet build tests/HarmonyWeaver.Tests.Patches
```

## License

[License information to be added]