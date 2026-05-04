import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import * as obs from "../index.js";

type FetchCall = [string, RequestInit];

function fetchCalls(): FetchCall[] {
  return (fetch as unknown as { mock: { calls: FetchCall[] } }).mock.calls;
}

function startCalls(): FetchCall[] {
  return fetchCalls().filter(([url]) => url.includes("/sessions/start"));
}

function endCalls(): FetchCall[] {
  return fetchCalls().filter(([url]) => url.includes("/sessions/end"));
}

describe("session bracketing (Issue 4.11)", () => {
  beforeEach(() => {
    obs.__test.reset();
    vi.stubGlobal("fetch", vi.fn(async () => new Response(null, { status: 202 })));
    vi.stubGlobal("sessionStorage", makeMemoryStorage());
  });

  afterEach(() => {
    obs.__test.reset();
    vi.unstubAllGlobals();
  });

  it("first track() fires /sessions/start exactly once", async () => {
    obs.init({ ingestUrl: "http://x", apiKey: "k", batchSize: 100, flushIntervalMs: 10_000, releaseSha: "abc123" });
    obs.identify("u1");
    obs.track("auth_logout");
    obs.track("auth_logout");
    await Promise.resolve();

    const starts = startCalls();
    expect(starts).toHaveLength(1);
    const [url, init] = starts[0]!;
    expect(url).toBe("http://x/api/ingest/sessions/start");
    expect((init.headers as Record<string, string>)["X-Observability-Key"]).toBe("k");
    const body = JSON.parse(init.body as string);
    expect(body.session_id).toBeTruthy();
    expect(body.distinct_id).toBe("u1");
    expect(body.release_sha).toBe("abc123");
  });

  it("first capturePageView() also brackets the session", async () => {
    obs.init({ ingestUrl: "http://x", apiKey: "k", batchSize: 100, flushIntervalMs: 10_000 });
    obs.capturePageView("/dashboard");
    await Promise.resolve();
    expect(startCalls()).toHaveLength(1);
  });

  it("first captureException() also brackets the session", async () => {
    obs.init({ ingestUrl: "http://x", apiKey: "k", batchSize: 100, flushIntervalMs: 10_000 });
    obs.captureException({ errorType: "render", source: "boundary" });
    await Promise.resolve();
    expect(startCalls()).toHaveLength(1);
  });

  it("trackSessions: false suppresses bracket calls", async () => {
    obs.init({ ingestUrl: "http://x", apiKey: "k", batchSize: 100, flushIntervalMs: 10_000, trackSessions: false });
    obs.track("auth_logout");
    await obs.shutdown();
    expect(startCalls()).toHaveLength(0);
    expect(endCalls()).toHaveLength(0);
  });

  it("shutdown() sends /sessions/end after a started session", async () => {
    obs.init({ ingestUrl: "http://x", apiKey: "k", batchSize: 100, flushIntervalMs: 10_000 });
    obs.track("auth_logout");
    await obs.shutdown();
    const ends = endCalls();
    expect(ends).toHaveLength(1);
    const [url, init] = ends[0]!;
    expect(url).toBe("http://x/api/ingest/sessions/end");
    const body = JSON.parse(init.body as string);
    expect(body.session_id).toBeTruthy();
  });

  it("shutdown() without any prior event does not send /sessions/end", async () => {
    obs.init({ ingestUrl: "http://x", apiKey: "k", batchSize: 100, flushIntervalMs: 10_000 });
    await obs.shutdown();
    expect(endCalls()).toHaveLength(0);
  });

  it("reset() lets the next track() start a new session", async () => {
    obs.init({ ingestUrl: "http://x", apiKey: "k", batchSize: 100, flushIntervalMs: 10_000 });
    obs.track("auth_logout");
    obs.reset();
    obs.track("auth_logout");
    await Promise.resolve();
    expect(startCalls()).toHaveLength(2);
  });
});

function makeMemoryStorage(): Storage {
  const map = new Map<string, string>();
  return {
    get length() {
      return map.size;
    },
    key(i) {
      return [...map.keys()][i] ?? null;
    },
    getItem(k) {
      return map.get(k) ?? null;
    },
    setItem(k, v) {
      map.set(k, v);
    },
    removeItem(k) {
      map.delete(k);
    },
    clear() {
      map.clear();
    },
  };
}
