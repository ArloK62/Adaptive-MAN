/**
 * Axios + native fetch interceptor helpers. Opt-in — no global monkey-patching.
 * Captures status_code, correlation_id, endpoint_group, method, is_network_error.
 *
 * Ported from sch-ui/src/services/apiClient.ts.
 */

import type { AxiosError, AxiosInstance, AxiosResponse, InternalAxiosRequestConfig } from "axios";
import { captureFailedRequest } from "./index.js";
import { endpointGroupForUrl } from "./route.js";

const CORRELATION_HEADER = "x-correlation-id";

export function attachAxiosInterceptor(instance: AxiosInstance): () => void {
  const id = instance.interceptors.response.use(
    (response: AxiosResponse) => response,
    (error: AxiosError) => {
      try {
        const cfg: InternalAxiosRequestConfig | undefined = error.config;
        const url = cfg?.url ?? "";
        const method = (cfg?.method ?? "get").toUpperCase();
        const status = error.response?.status ?? 0;
        const isNetwork = !error.response;
        const correlationId = readHeader(error.response?.headers, CORRELATION_HEADER);
        captureFailedRequest({
          url,
          method,
          httpStatusCode: status,
          isNetworkError: isNetwork,
          ...(correlationId ? { correlationId } : {}),
          endpointGroup: endpointGroupForUrl(url),
        });
      } catch {
        /* never throw from interceptor */
      }
      return Promise.reject(error);
    },
  );
  return () => instance.interceptors.response.eject(id);
}

/**
 * Wraps `fetch` to emit api_request_failed on non-2xx + network errors.
 * Returns a fetch-compatible function; the caller chooses whether to install it globally.
 */
export function wrapFetch(originalFetch: typeof fetch = fetch): typeof fetch {
  return async function wrapped(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
    const url = typeof input === "string" ? input : input instanceof URL ? input.toString() : input.url;
    const method = (init?.method ?? (typeof input !== "string" && !(input instanceof URL) ? input.method : "GET")).toUpperCase();
    try {
      const res = await originalFetch(input as RequestInfo, init);
      if (!res.ok) {
        const correlationId = res.headers.get(CORRELATION_HEADER);
        captureFailedRequest({
          url,
          method,
          httpStatusCode: res.status,
          isNetworkError: false,
          ...(correlationId ? { correlationId } : {}),
        });
      }
      return res;
    } catch (err) {
      captureFailedRequest({
        url,
        method,
        httpStatusCode: 0,
        isNetworkError: true,
      });
      throw err;
    }
  };
}

function readHeader(headers: unknown, name: string): string | undefined {
  if (!headers) return undefined;
  if (typeof (headers as { get?: unknown }).get === "function") {
    const v = (headers as { get: (k: string) => string | null }).get(name);
    return v ?? undefined;
  }
  const obj = headers as Record<string, unknown>;
  const direct = obj[name] ?? obj[name.toLowerCase()];
  if (typeof direct === "string") return direct;
  if (Array.isArray(direct) && typeof direct[0] === "string") return direct[0] as string;
  return undefined;
}
