export const env = {
  postServiceUrl: __ENV.POST_SERVICE_URL || 'http://localhost:8081',
  graphServiceUrl: __ENV.GRAPH_SERVICE_URL || 'http://localhost:8082',
  feedServiceUrl: __ENV.FEED_SERVICE_URL || 'http://localhost:8083',
  defaultUserId: __ENV.DEFAULT_USER_ID || 'user-1',
  defaultAuthorId: __ENV.DEFAULT_AUTHOR_ID || 'author-1',
  celebrityAuthorId: __ENV.CELEBRITY_AUTHOR_ID || 'celebrity-1',
  requestTimeoutMs: Number(__ENV.REQUEST_TIMEOUT_MS || 5000),
  eventualConsistencyTimeoutMs: Number(__ENV.EVENTUAL_CONSISTENCY_TIMEOUT_MS || 15000),
  eventualConsistencyIntervalMs: Number(__ENV.EVENTUAL_CONSISTENCY_INTERVAL_MS || 500),
  feedPageLimit: Number(__ENV.FEED_PAGE_LIMIT || 10),
  graphPageLimit: Number(__ENV.GRAPH_PAGE_LIMIT || 10),
  authorTimelineLimit: Number(__ENV.AUTHOR_TIMELINE_LIMIT || 10),
  latencyP95Ms: Number(__ENV.LATENCY_P95_MS || 800),
  latencyP99Ms: Number(__ENV.LATENCY_P99_MS || 1500),
  scaleReadRate: Number(__ENV.SCALE_READ_RATE || 25),
  scaleWriteRate: Number(__ENV.SCALE_WRITE_RATE || 10),
  scaleDuration: __ENV.SCALE_DURATION || '30s',
};

export default env;
