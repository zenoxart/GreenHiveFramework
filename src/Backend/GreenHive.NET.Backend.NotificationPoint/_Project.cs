// NotificationPoint (Docker container "notification-point", route /api/notifications)
// Receives events from every service's outbox (POST /internal/events), stores them with a sequence number,
// streams them to clients as server-sent events (catch-up via Last-Event-ID), forwards them to backend subscribers
// (workflow triggers) and, if configured, to WNS push channels of users without a live connection.
