import env from '../config/env.local.js';
import { get } from '../helpers/http.js';
import { authHeaders } from '../helpers/auth.js';
import { assertStatus } from '../helpers/assertions.js';
import { metrics } from '../helpers/utils.js';

export default function scaleReadScenario() {
  const hotUserId = env.defaultUserId;
  const userId = __ITER % 2 === 0 ? hotUserId : `scale-reader-${__VU}-${__ITER}`;

  const feedRes = get(`${env.feedServiceUrl}/feed?limit=${env.feedPageLimit}`, {
    headers: authHeaders(userId),
    tags: { scenario: 'scale-read', endpoint: 'feed' },
  });
  assertStatus(feedRes, 200, 'scale read feed');
  const items = feedRes.json().items || [];
  metrics.feedItemsReturned.add(items.length);
}
