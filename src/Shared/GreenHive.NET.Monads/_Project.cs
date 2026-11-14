// GreenHive.NET.Monads
// Self-written monads of the Green Hive monad architecture (no dependencies):
// Unit, Error, Option<T>, Result<T>, Validation<T>, Try, async/LINQ extensions and combinators.
// Expected outcomes ("not found", "invalid", "forbidden", "conflict") are values, not exceptions.
// Use cases are railway pipelines: Bind continues on success, the first failure short-circuits.
