// ApiGateway (Docker container "api-gateway") - the single public entry point for clients.
// Self-written reverse proxy (no external package): static routes from configuration plus the module backend routes
// from the Module Registry (refreshed every 30 seconds), longest-prefix matching, streaming responses (server-sent
// events), correlation ID propagation, rate limiting, and /internal endpoints are never routed.
