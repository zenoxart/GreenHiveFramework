// ModuleRegistryService (Docker container "module-registry", route /api/modules)
// Catalogue of module versions (manifests incl. contract surfaces). Publishing a version registers its permissions,
// runs the workflow impact analysis (diff of the contract surfaces) and starts a staged rollout.
// The catalogue answers per user: the version assigned by the rollout, and whether it fits the caller's host.
