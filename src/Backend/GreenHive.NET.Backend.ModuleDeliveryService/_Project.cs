// ModuleDeliveryService (Docker container "module-delivery", route /api/packages)
// Stores and serves the client packages (zip) of module versions. Every package is hashed (SHA-256) and signed
// (ECDSA P-256); the client verifies hash and signature before it loads a module.
// Packages are dropped into the volume (/data/packages/<moduleId>/<version>/<file>.zip) by the build script or
// uploaded with PUT /api/packages/{moduleId}/{version}.
