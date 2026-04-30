/**
 * Route normalization. Ported from sch-ui/src/utils/routeUtils.ts.
 * Strips query strings entirely, replaces numeric IDs / UUIDs / ULIDs / opaque tokens
 * with `:id`, then maps to a feature area.
 *
 * Token-threshold tuning is deliberately conservative so segments like `posthog-500-test`
 * stay literal — only segments that are *purely* an identifier get replaced.
 */

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const ULID_RE = /^[0-9A-HJKMNP-TV-Z]{26}$/;
const NUMERIC_RE = /^\d+$/;
const HEX_TOKEN_RE = /^[0-9a-f]{16,}$/i;
const BASE64URL_TOKEN_RE = /^[A-Za-z0-9_-]{32,}$/;

function isIdentifierSegment(segment: string): boolean {
  if (!segment) return false;
  if (NUMERIC_RE.test(segment)) return true;
  if (UUID_RE.test(segment)) return true;
  if (ULID_RE.test(segment)) return true;
  if (HEX_TOKEN_RE.test(segment)) return true;
  if (BASE64URL_TOKEN_RE.test(segment) && /\d/.test(segment) && /[A-Za-z]/.test(segment)) {
    return true;
  }
  return false;
}

export function normalizeRoute(input: string): string {
  if (!input) return "/";
  let path = input;
  const queryIndex = path.indexOf("?");
  if (queryIndex >= 0) path = path.slice(0, queryIndex);
  const hashIndex = path.indexOf("#");
  if (hashIndex >= 0) path = path.slice(0, hashIndex);

  if (!path.startsWith("/")) path = "/" + path;

  const parts = path.split("/").map((seg, i) => {
    if (i === 0) return seg;
    return isIdentifierSegment(seg) ? ":id" : seg;
  });

  return parts.join("/") || "/";
}

const FEATURE_AREA_RULES: Array<[RegExp, string]> = [
  [/^\/auth(\/|$)/, "auth"],
  [/^\/login(\/|$)/, "auth"],
  [/^\/logout(\/|$)/, "auth"],
  [/^\/dashboard(\/|$)/, "dashboard"],
  [/^\/settings(\/|$)/, "settings"],
  [/^\/admin(\/|$)/, "admin"],
  [/^\/orders(\/|$)/, "orders"],
  [/^\/reports(\/|$)/, "reports"],
];

export function getFeatureArea(normalizedRoute: string): string {
  for (const [re, area] of FEATURE_AREA_RULES) {
    if (re.test(normalizedRoute)) return area;
  }
  return "other";
}

const ENDPOINT_RULES: Array<[RegExp, string]> = [
  [/^\/api\/auth\//, "auth"],
  [/^\/api\/users?\//, "users"],
  [/^\/api\/orders?\//, "orders"],
  [/^\/api\/reports?\//, "reports"],
  [/^\/api\/admin\//, "admin"],
];

export function endpointGroupForUrl(url: string): string {
  let path = url;
  try {
    const parsed = new URL(url, "http://placeholder");
    path = parsed.pathname;
  } catch {
    /* relative path already */
  }
  const normalized = normalizeRoute(path);
  for (const [re, group] of ENDPOINT_RULES) {
    if (re.test(normalized)) return group;
  }
  if (normalized.startsWith("/api/")) {
    const seg = normalized.split("/")[2];
    return seg || "api";
  }
  return "other";
}
