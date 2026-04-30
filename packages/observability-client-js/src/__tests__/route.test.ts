import { describe, expect, it } from "vitest";
import { endpointGroupForUrl, getFeatureArea, normalizeRoute } from "../route.js";

describe("normalizeRoute", () => {
  it("strips query string", () => {
    expect(normalizeRoute("/users/42?tab=info")).toBe("/users/:id");
  });

  it("strips hash", () => {
    expect(normalizeRoute("/users/42#tab")).toBe("/users/:id");
  });

  it("replaces numeric ids", () => {
    expect(normalizeRoute("/orders/12345")).toBe("/orders/:id");
  });

  it("replaces UUIDs", () => {
    expect(normalizeRoute("/sessions/550e8400-e29b-41d4-a716-446655440000")).toBe("/sessions/:id");
  });

  it("preserves literal segments containing digits but not pure ids", () => {
    expect(normalizeRoute("/posthog-500-test")).toBe("/posthog-500-test");
  });

  it("preserves /api prefix", () => {
    expect(normalizeRoute("/api/orders/9")).toBe("/api/orders/:id");
  });

  it("returns / for empty input", () => {
    expect(normalizeRoute("")).toBe("/");
  });
});

describe("getFeatureArea", () => {
  it("maps /auth to auth", () => {
    expect(getFeatureArea("/auth/login")).toBe("auth");
  });
  it("maps unknown to other", () => {
    expect(getFeatureArea("/random/page")).toBe("other");
  });
});

describe("endpointGroupForUrl", () => {
  it("returns auth for /api/auth/*", () => {
    expect(endpointGroupForUrl("/api/auth/login")).toBe("auth");
  });
  it("falls back to first segment for /api/*", () => {
    expect(endpointGroupForUrl("/api/widgets/42")).toBe("widgets");
  });
  it("returns other for non-api urls", () => {
    expect(endpointGroupForUrl("/dashboard")).toBe("other");
  });
});
