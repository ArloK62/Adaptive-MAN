/**
 * Replay adapter slot. Phase 4.9 reserves the public shape so Phase 9 can drop in an
 * rrweb-backed implementation without breaking SemVer or call sites.
 *
 * No rrweb dependency is added in Phase 4 — this file ships only the no-op adapter
 * and the type contract.
 */

export interface ReplayConfig {
  enabled: boolean;
  sampleRate: number;
  captureOnError: boolean;
  maskAllInputs: boolean;
  blockSelectors: string[];
  maxSessionMinutes: number;
}

export const defaultReplayConfig: ReplayConfig = {
  enabled: false,
  sampleRate: 0,
  captureOnError: false,
  maskAllInputs: true,
  blockSelectors: [],
  maxSessionMinutes: 30,
};

export interface ReplayAdapter {
  start(ctx: ReplayContext): void;
  stop(): void;
  flush(): Promise<void>;
}

export interface ReplayContext {
  sessionId: string;
  config: ReplayConfig;
  debug: boolean;
}

export const noopReplayAdapter: ReplayAdapter = {
  start(ctx) {
    if (ctx.debug && ctx.config.enabled) {
      console.info("[adaptive-observability] replay adapter slot active (no-op); Phase 9 ships rrweb implementation");
    }
  },
  stop() {
    /* no-op */
  },
  async flush() {
    /* no-op */
  },
};
