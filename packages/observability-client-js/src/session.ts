/**
 * Anonymous session ID stored in sessionStorage. Same ID exposed to the replay adapter slot
 * (Phase 4.9) and to backend session endpoints (Phase 5).
 */

const KEY = "adaptive_observability_session_id";

function generateId(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }
  return "s_" + Math.random().toString(36).slice(2) + Date.now().toString(36);
}

export function getOrCreateSessionId(): string {
  if (typeof sessionStorage === "undefined") return generateId();
  try {
    const existing = sessionStorage.getItem(KEY);
    if (existing) return existing;
    const fresh = generateId();
    sessionStorage.setItem(KEY, fresh);
    return fresh;
  } catch {
    return generateId();
  }
}

export function resetSessionId(): string {
  const fresh = generateId();
  if (typeof sessionStorage !== "undefined") {
    try {
      sessionStorage.setItem(KEY, fresh);
    } catch {
      /* ignore */
    }
  }
  return fresh;
}
