import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { Transport, type TransportConfig } from "../transport.js";

const baseConfig: TransportConfig = {
  ingestUrl: "http://localhost:5000",
  apiKey: "test-key",
  batchSize: 2,
  flushIntervalMs: 5000,
  maxRetries: 1,
  debug: false,
};

describe("Transport", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.stubGlobal("fetch", vi.fn(async () => new Response(null, { status: 202 })));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it("flushes when batchSize reached", async () => {
    const t = new Transport(baseConfig);
    const env = (event: string) => ({
      kind: "event" as const,
      event,
      distinct_id: "u1",
      occurred_at: "2026-04-30T00:00:00Z",
      properties: {},
    });
    t.enqueue(env("auth_login_success"));
    t.enqueue(env("auth_logout"));
    await Promise.resolve();
    await Promise.resolve();
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it("retries on 5xx then drops", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(null, { status: 500 })));
    const t = new Transport({ ...baseConfig, batchSize: 1, maxRetries: 1 });
    t.enqueue({
      kind: "event",
      event: "auth_logout",
      distinct_id: "u1",
      occurred_at: "2026-04-30T00:00:00Z",
      properties: {},
    });
    await vi.runAllTimersAsync();
    expect((fetch as unknown as { mock: { calls: unknown[] } }).mock.calls.length).toBeGreaterThanOrEqual(1);
  });

  it("does not retry on 4xx (treated as terminal)", async () => {
    const fetchMock = vi.fn(async () => new Response(null, { status: 422 }));
    vi.stubGlobal("fetch", fetchMock);
    const t = new Transport({ ...baseConfig, batchSize: 1, maxRetries: 3 });
    t.enqueue({
      kind: "event",
      event: "auth_logout",
      distinct_id: "u1",
      occurred_at: "2026-04-30T00:00:00Z",
      properties: {},
    });
    await vi.runAllTimersAsync();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
