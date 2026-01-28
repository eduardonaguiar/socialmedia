export const thresholds = {
  http_req_failed: ['rate<0.01'],
  http_req_duration: [`p(95)<${__ENV.LATENCY_P95_MS || 800}`, `p(99)<${__ENV.LATENCY_P99_MS || 1500}`],
  feed_items_returned: ['count>0'],
  duplicate_items_detected: ['count==0'],
  partial_feed_responses: ['count<1'],
  fanout_latency_ms: [`p(95)<${__ENV.FANOUT_P95_MS || 5000}`],
  eventual_consistency_delay_ms: [`p(95)<${__ENV.EVENTUAL_CONSISTENCY_P95_MS || 8000}`],
};

export default thresholds;
