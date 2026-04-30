/**
 * React error boundary helper. NEVER sends `error.message`, `error.stack`, or
 * React `componentStack` text — only `error_type`, `source`, `component_stack_depth`.
 *
 * Ported from sch-ui/src/components/common/ErrorBoundary.tsx.
 */

import { Component, type ErrorInfo, type ReactNode } from "react";
import { captureException } from "./index.js";

export interface ObservabilityErrorBoundaryProps {
  children: ReactNode;
  fallback?: ReactNode | ((error: { errorType: string }) => ReactNode);
  source?: string;
}

interface State {
  hasError: boolean;
  errorType: string;
}

export class ObservabilityErrorBoundary extends Component<ObservabilityErrorBoundaryProps, State> {
  override state: State = { hasError: false, errorType: "" };

  static getDerivedStateFromError(error: unknown): State {
    return { hasError: true, errorType: errorTypeOf(error) };
  }

  override componentDidCatch(error: unknown, info: ErrorInfo): void {
    const depth = countStackDepth(info.componentStack ?? "");
    captureException({
      errorType: errorTypeOf(error),
      source: this.props.source ?? "react_error_boundary",
      componentStackDepth: depth,
    });
  }

  override render(): ReactNode {
    if (!this.state.hasError) return this.props.children;
    const { fallback } = this.props;
    if (typeof fallback === "function") return fallback({ errorType: this.state.errorType });
    return fallback ?? null;
  }
}

function errorTypeOf(error: unknown): string {
  if (error && typeof error === "object" && "name" in error) {
    const n = (error as { name?: unknown }).name;
    if (typeof n === "string" && n) return n;
  }
  if (error instanceof Error) return error.constructor.name;
  return "UnknownError";
}

function countStackDepth(componentStack: string): number {
  if (!componentStack) return 0;
  return componentStack.split("\n").filter((line) => line.trim().startsWith("in ") || line.trim().startsWith("at ")).length;
}
