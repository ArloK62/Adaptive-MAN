# Migration: PostHog → adaptive-observability

Phase 6 work. Placeholder. The migration strategy is summarized in [DEVELOPMENT_PLAN.md §Phase 6](../../DEVELOPMENT_PLAN.md).

## Strategy

1. Implement `IAnalyticsService` against the new platform in `observability-client-dotnet`.
2. Register both PostHog and adaptive-observability as a composite `IAnalyticsService` in SCH_API UAT for a 5-business-day dual-write window.
3. Compare counts daily; require <1% variance per event type for 5 consecutive days.
4. Flip composite to adaptive-only in UAT, soak 48 hours with zero `SafetyViolations`.
5. Repeat in Prod.
6. Remove `PostHog.AspNetCore` and `posthog-js` after 1 week of stable Prod traffic.

## Cutover prerequisites

See Issue 6.1. Deferred PostHog hardening items must land before/alongside cutover.
