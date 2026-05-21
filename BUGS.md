# Known Bugs & Operational Hazards

Defects and production hazards awaiting a fix. Discovered during the 2026-05-21
outage investigation (root cause active since ~2026-05-07).

---

## BUG-1 — `/labler` returns 500 on unrecoverable Gmail errors → infinite Pub/Sub retry storm

- **Severity**: High (caused a silent multi-day outage)
- **Status**: Open

**Symptom**: Every Pub/Sub push to `/labler` returns HTTP 500. Because 500 is
retryable, Pub/Sub redelivers the same messages indefinitely (~0.13 req/s
observed) until they expire at the 7-day retention. No alert fires — only
visible via `docker logs`.

**Root cause**: `HandleLabler` calls `GetNewMessageIdsAsync` and `GetEmailAsync`
without catching Gmail failures ([LablerEndpoints.cs:67](src/EmailLabeler/Endpoints/LablerEndpoints.cs),
[GmailRepository.cs:95](src/EmailLabeler/Adapters/GmailRepository.cs) /
[:31](src/EmailLabeler/Adapters/GmailRepository.cs)). Any Gmail/auth error
propagates as an unhandled 500. Two known triggers: an expired refresh token
(`invalid_grant`, see BUG-2) and a stale `startHistoryId` (Gmail History API
returns 404 once the historyId is older than its retention window).

**Fix**:
- New provider-agnostic exception (e.g. `EmailAuthenticationException`) in the
  Ports/Domain layer; the Gmail adapter translates `TokenResponseException` /
  `invalid_grant` into it (keeps `Google.Apis.*` types out of the endpoint).
- `GetNewMessageIdsAsync`: catch `GoogleApiException` 404 (stale `startHistoryId`)
  → log a warning and return empty so the push is acked (200) instead of looping
  forever. Follow-up: full re-sync of recent messages to recover the missed window.
- `HandleLabler`: catch `EmailAuthenticationException` → one actionable Error log
  ("Gmail credentials rejected — re-mint GMAIL_REFRESH_TOKEN / publish consent
  screen") and return 503 (retryable, so messages are preserved, not dropped).
  Decode errors stay 400.
- Tests: unit (endpoint returns 503 + logs on auth exception); integration
  (WireMock 404 on `history.list` ⇒ 200/ack; token endpoint `invalid_grant` ⇒ 503).

---

## BUG-2 — OAuth refresh token expires every 7 days (consent screen in "Testing")

- **Severity**: High (root cause of the 2026-05-21 outage)
- **Status**: Open

**Symptom**: After ~7 days the `GMAIL_REFRESH_TOKEN` stops working
(`invalid_grant`). The watch can't renew so Gmail stops publishing, and `/labler`
500s on every push (see BUG-1). Re-minting the token only restores service for
another ~7 days.

**Root cause**: Google expires refresh tokens after 7 days while the OAuth consent
screen is in **Testing** publishing status.

**Fix**:
- Set the OAuth consent screen Publishing status to **In production** in Google
  Cloud Console (personal use can publish without full verification). This stops
  the expiry.
- Document the re-mint procedure and the publish-to-production requirement in the
  README / `task setup:gmail` guide.

---

**Related roadmap work**: Phase 6 (Observability & Alerting) in [TODO.md](TODO.md)
would have surfaced both of these proactively — real `/health` dependency checks
and a watch-renewal heartbeat alert. BUG-2 is the root cause behind Phase 6's
"watch expires silently" risk; BUG-1 is the request-path counterpart to its
"static `/health`" / "nobody watches stdout" notes.
