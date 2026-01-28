import env from './config/env.local.js';
import thresholds from './config/thresholds.js';

import postServiceScenario from './scenarios/01-post-service.js';
import graphServiceScenario from './scenarios/02-graph-service.js';
import fanoutWorkerScenario from './scenarios/03-fanout-worker.js';
import feedServiceScenario from './scenarios/04-feed-service.js';
import hybridCelebrityScenario from './scenarios/05-hybrid-celebrity.js';
import resilienceScenario from './scenarios/06-resilience.js';
import duplicationScenario from './scenarios/07-duplication.js';
import scaleReadScenario from './scenarios/08-scale-read.js';
import scaleWriteScenario from './scenarios/09-scale-write.js';

const scenarioGroups = {
  feed: ['feed_service', 'scale_read'],
  fanout: ['fanout_worker', 'duplication', 'scale_write'],
  hybrid: ['hybrid_celebrity'],
  resilience: ['resilience'],
  all: [
    'post_service',
    'graph_service',
    'fanout_worker',
    'feed_service',
    'hybrid_celebrity',
    'resilience',
    'duplication',
    'scale_read',
    'scale_write',
  ],
};

const selected = scenarioGroups[__ENV.SCENARIO || 'all'] || scenarioGroups.all;

const scenarioDefinitions = {
  post_service: {
    executor: 'per-vu-iterations',
    exec: 'postServiceScenario',
    vus: 1,
    iterations: 1,
    tags: { suite: 'functional', scenario: 'post-service' },
  },
  graph_service: {
    executor: 'per-vu-iterations',
    exec: 'graphServiceScenario',
    vus: 1,
    iterations: 1,
    tags: { suite: 'functional', scenario: 'graph-service' },
  },
  fanout_worker: {
    executor: 'per-vu-iterations',
    exec: 'fanoutWorkerScenario',
    vus: 1,
    iterations: 1,
    tags: { suite: 'e2e', scenario: 'fanout-worker' },
  },
  feed_service: {
    executor: 'per-vu-iterations',
    exec: 'feedServiceScenario',
    vus: 1,
    iterations: 1,
    tags: { suite: 'functional', scenario: 'feed-service' },
  },
  hybrid_celebrity: {
    executor: 'per-vu-iterations',
    exec: 'hybridCelebrityScenario',
    vus: 1,
    iterations: 1,
    tags: { suite: 'e2e', scenario: 'hybrid-celebrity' },
  },
  resilience: {
    executor: 'per-vu-iterations',
    exec: 'resilienceScenario',
    vus: 1,
    iterations: 1,
    tags: { suite: 'resilience', scenario: 'resilience' },
  },
  duplication: {
    executor: 'per-vu-iterations',
    exec: 'duplicationScenario',
    vus: 1,
    iterations: 1,
    tags: { suite: 'resilience', scenario: 'duplication' },
  },
  scale_read: {
    executor: 'constant-arrival-rate',
    exec: 'scaleReadScenario',
    rate: env.scaleReadRate,
    timeUnit: '1s',
    duration: env.scaleDuration,
    preAllocatedVUs: Math.max(2, env.scaleReadRate),
    tags: { suite: 'scale', scenario: 'scale-read' },
  },
  scale_write: {
    executor: 'constant-arrival-rate',
    exec: 'scaleWriteScenario',
    rate: env.scaleWriteRate,
    timeUnit: '1s',
    duration: env.scaleDuration,
    preAllocatedVUs: Math.max(2, env.scaleWriteRate),
    tags: { suite: 'scale', scenario: 'scale-write' },
  },
};

export const options = {
  thresholds,
  scenarios: Object.fromEntries(
    Object.entries(scenarioDefinitions).filter(([name]) => selected.includes(name))
  ),
};

export {
  postServiceScenario,
  graphServiceScenario,
  fanoutWorkerScenario,
  feedServiceScenario,
  hybridCelebrityScenario,
  resilienceScenario,
  duplicationScenario,
  scaleReadScenario,
  scaleWriteScenario,
};
