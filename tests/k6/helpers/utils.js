import { Trend, Counter } from 'k6/metrics';
import { sleep } from 'k6';
import exec from 'k6/execution';
import env from '../config/env.local.js';

export const metrics = {
  feedItemsReturned: new Counter('feed_items_returned'),
  duplicateItemsDetected: new Counter('duplicate_items_detected'),
  partialFeedResponses: new Counter('partial_feed_responses'),
  fanoutLatencyMs: new Trend('fanout_latency_ms'),
  eventualConsistencyDelayMs: new Trend('eventual_consistency_delay_ms'),
};

export function uniqueId(prefix) {
  return `${prefix}-${exec.vu.idInTest}-${exec.vu.iterationInScenario}`;
}

export function nowMs() {
  return Date.now();
}

export function waitFor(conditionFn, timeoutMs = env.eventualConsistencyTimeoutMs, intervalMs = env.eventualConsistencyIntervalMs) {
  const start = nowMs();
  while (nowMs() - start < timeoutMs) {
    const result = conditionFn();
    if (result) {
      return { result, elapsedMs: nowMs() - start };
    }
    sleep(intervalMs / 1000);
  }
  return { result: null, elapsedMs: nowMs() - start };
}

export function buildContent(label) {
  return `k6-${label}-${exec.vu.idInTest}-${exec.vu.iterationInScenario}`;
}
