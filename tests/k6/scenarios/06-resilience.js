import env from '../config/env.local.js';
import { post, get } from '../helpers/http.js';
import { authHeaders } from '../helpers/auth.js';
import { assertStatus, assertLatency } from '../helpers/assertions.js';
import { waitFor, metrics } from '../helpers/utils.js';

export default function resilienceScenario() {
  const followerId = `resilience-follower-${__VU}-${__ITER}`;
  const normalAuthorId = `resilience-author-${__VU}-${__ITER}`;
  const celebrityAuthorId = env.celebrityAuthorId;

  post(`${env.graphServiceUrl}/follow/${normalAuthorId}`, {}, {
    headers: authHeaders(followerId),
    tags: { scenario: 'resilience', endpoint: 'follow-normal' },
  });
  post(`${env.graphServiceUrl}/follow/${celebrityAuthorId}`, {}, {
    headers: authHeaders(followerId),
    tags: { scenario: 'resilience', endpoint: 'follow-celebrity' },
  });

  const normalPost = post(`${env.postServiceUrl}/posts`, { content: `resilience-post-${__VU}-${__ITER}` }, {
    headers: authHeaders(normalAuthorId),
    tags: { scenario: 'resilience', endpoint: 'normal-post' },
  });
  assertStatus(normalPost, 201, 'create normal post');

  const celebPost = post(`${env.postServiceUrl}/posts`, { content: `resilience-celebrity-${__VU}-${__ITER}` }, {
    headers: authHeaders(celebrityAuthorId),
    tags: { scenario: 'resilience', endpoint: 'celebrity-post' },
  });
  assertStatus(celebPost, 201, 'create celebrity post');

  const normalPostId = normalPost.json().post_id;
  const celebrityPostId = celebPost.json().post_id;

  const feedResult = waitFor(() => {
    const feedRes = get(`${env.feedServiceUrl}/feed?limit=${env.feedPageLimit}`, {
      headers: authHeaders(followerId),
      tags: { scenario: 'resilience', endpoint: 'feed' },
    });
    if (feedRes.status !== 200) {
      return null;
    }
    assertLatency(feedRes, env.latencyP99Ms, 'feed latency stabilization');
    const body = feedRes.json();
    const items = body.items || [];
    const hasNormal = items.some((item) => item.post_id === normalPostId);
    if (hasNormal) {
      const hasCelebrity = items.some((item) => item.post_id === celebrityPostId);
      if (!hasCelebrity && __ENV.RESILIENCE_EXPECT_PARTIAL === 'true') {
        metrics.partialFeedResponses.add(1);
      }
      return items;
    }
    return null;
  });

  if (feedResult.result) {
    metrics.feedItemsReturned.add(feedResult.result.length);
  }
}
