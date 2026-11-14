// GreenHive.NET.Backend.Core
// Shared infrastructure of the monad architecture for every backend service and module backend:
//   Hosting   - AddGreenHiveDefaults / UseGreenHiveDefaults: JSON, health checks, JWT, correlation ID, internal key
//   Security  - self-written HS256 JWT (TokenService + authentication handler), GreenHiveUser, internal endpoints
//   Data      - Db: ADO.NET access returning Result/Option, transactions that commit only on Success
//   Pipeline  - use-case shape and reusable steps (validate, authorize, transaction, outbox, idempotency, audit)
//   Http      - ResultHttpMapper (Result -> HTTP / Problem Details), ExceptionBoundaryMiddleware, ServiceClient
//   Events    - transactional outbox + dispatcher to the NotificationPoint
// The only external package is the PostgreSQL driver (Data/PostgresDataSource.cs).
