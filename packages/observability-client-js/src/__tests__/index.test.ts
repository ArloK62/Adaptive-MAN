import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import * as obs from "../index.js";

describe("init / track", () => {
  beforeEach(() => {
    obs.__test.reset();
    vi.stubGlobal("fetch", vi.fn(async () => new Response(null, { status: 202 })));
    vi.stubGlobal("sessionStorage", makeMemoryStorage());
  });

  afterEach(() => {
    obs.__test.reset();
    vi.unstubAllGlobals();
  });

  it("no-ops if init is not called", () => {
    obs.track("auth_logout");
    expect(fetch).not.toHaveBeenCalled();
  });

  it("identify accepts only string", () => {
    obs.init({ ingestUrl: "http://x", apiKey: "k", flushIntervalMs: 10, batchSize: 1 });
    obs.identify("user-1");
    expect(obs.__test.getState()?.distinctId).toBe("user-1");
    obs.identify("");
    expect(obs.__test.getState()?.distinctId).toBe("user-1");
  });

  it("getSessionId returns a stable id", () => {
    obs.init({ ingestUrl: "http://x", apiKey: "k", batchSize: 1, flushIntervalMs: 10 });
    const a = obs.getSessionId();
    const b = obs.getSessionId();
    expect(a).toBeTruthy();
    expect(a).toBe(b);
  });

  it("track sends an event", async () => {
    obs.init({ ingestUrl: "http://x", apiKey: "k", batchSize: 1, flushIntervalMs: 10, trackSessions: false });
    obs.identify("u1");
    obs.track("auth_logout");
    await Promise.resolve();
    await Promise.resolve();
    expect(fetch).toHaveBeenCalledTimes(1);
    const [, init] = (fetch as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0]!;
    const body = JSON.parse(init.body as string);
    expect(body.event).toBe("auth_logout");
    expect(body.distinct_id).toBe("u1");
  });

  it("replay.enabled with no-op adapter is a no-op, not a throw", () => {
    expect(() => {
      obs.init({
        ingestUrl: "http://x",
        apiKey: "k",
        batchSize: 1,
        flushIntervalMs: 10,
        replay: { enabled: true, captureOnError: true },
      });
    }).not.toThrow();
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
