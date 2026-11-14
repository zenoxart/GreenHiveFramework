# Module packages

A package is a ZIP file containing:

- `manifest.json`
- the module's entry assembly (+ `.pdb`) and module-specific dependencies — framework and contract assemblies are
  **not** included (the host provides them; module clients reference them with `Private=false`)
- optional resources (images referenced as `image:<file>` icons)

Packages are immutable: a new version always produces a new package. The ModuleDelivery service stores them under
`/data/packages/<moduleId>/<version>/`, computes the SHA-256 hash and signs it (ECDSA P-256); the client checks hash
and signature before it extracts a package to `Modules\<id>\<version>\` in the app's local data folder.

## Build, upload, publish

```powershell
# build and zip all modules (output: Deployment\ModulePackages\out)
.\pack-modules.ps1

# ... and upload the packages to the running backend (docker compose up)
.\pack-modules.ps1 -Upload -Password 'GreenHive!2026'

# a new version: raise "version" in src\Modules\<Module>\manifest.json, then
.\pack-modules.ps1 -Module document-management -Upload -Publish -Password 'GreenHive!2026'
```

Publishing runs the workflow impact analysis and starts a rollout (Pilot → Broad → Complete) in the
UpdateOrchestrator. Clients of the assigned users receive `module.version.available` and the HotSwapManager swaps
the module at runtime (hotSwap = supported) or asks for a restart.

Without uploaded packages the client still works: the host build copies the module clients as *development modules*
next to the exe (`Modules\<id>\`), and the ModuleLoader uses them when the catalogue lists a version without package.

## demo-updates

`demo-updates/document-management-1.1.0.json` is a manifest of a newer document-management version with a breaking
change (renamed step type). Publish it (`POST /api/modules`, see the main README) to watch the impact analysis
classify the dependent workflows and the rollout hold.
