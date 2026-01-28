import env from '../config/env.local.js';
import { post, get } from '../helpers/http.js';
import { authHeaders } from '../helpers/auth.js';
import { assertStatus, assertNoDuplicates, assertArrayNotEmpty } from '../helpers/assertions.js';
import { waitFor, metrics } from '../helpers/utils.js';

export default function fanoutWorkerScenario() {
  const followerId = `fanout-follower-${__VU}-${__ITER}`;
  const authorId = `fanout-author-${__VU}-${__ITER}`;

  const followRes = post(`${env.graphServiceUrl}/follow/${authorId}`, {}, {
    headers: authHeaders(followerId),
    tags: { scenario: 'fanout-worker', endpoint: 'follow' },
  });
  assertStatus(followRes, 200, 'follow author');

  const postRes = post(`${env.postServiceUrl}/posts`, { content: `fanout-post-${__VU}-${__ITER}` }, {
    headers: authHeaders(authorId),
    tags: { scenario: 'fanout-worker', endpoint: 'create-post' },
  });
  assertStatus(postRes, 201, 'create post');
  const postBody = postRes.json();

  const feedResult = waitFor(() => {
    const feedRes = get(`${env.feedServiceUrl}/feed?limit=${env.feedPageLimit}`, {
      headers: authHeaders(followerId),
      tags: { scenario: 'fanout-worker', endpoint: 'feed' },
    });
    if (feedRes.status !== 200) {
      return null;
    }
    const body = feedRes.json();
    const items = body.items || [];
    metrics.feedItemsReturned.add(items.length);
    const matches = items.filter((item) => item.post_id === postBody.post_id);
    if (matches.length > 0) {
      return items;
    }
    return null;
  });

  assertArrayNotEmpty(feedResult.result || [], 'fanout feed');
  if (feedResult.result) {
    metrics.eventualConsistencyDelayMs.add(feedResult.elapsedMs);
    metrics.fanoutLatencyMs.add(feedResult.elapsedMs);
    const matches = feedResult.result.filter((item) => item.post_id === postBody.post_id);
    assertNoDuplicates(matches, (item) => item.post_id, 'fanout dedupe');
  }

  const repeatRes = get(`${env.feedServiceUrl}/feed?limit=${env.feedPageLimit}`, {
    headers: authHeaders(followerId),
    tags: { scenario: 'fanout-worker', endpoint: 'feed-repeat' },
  });
  assertStatus(repeatRes, 200, 'feed repeat');
  const repeatItems = repeatRes.json().items || [];
  const occurrences = repeatItems.filter((item) => item.post_id === postBody.post_id);
  assertNoDuplicates(occurrences, (item) => item.post_id, 'repeat dedupe');
}
