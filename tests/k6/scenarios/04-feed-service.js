import env from '../config/env.local.js';
import { post, get } from '../helpers/http.js';
import { authHeaders } from '../helpers/auth.js';
import {
  assertStatus,
  assertNoDuplicates,
  assertOrderedByScoreDesc,
  assertCursorProgression,
} from '../helpers/assertions.js';
import { getCursorFromResponse } from '../helpers/cursors.js';
import { waitFor, metrics } from '../helpers/utils.js';

export default function feedServiceScenario() {
  const emptyUserId = `feed-empty-${__VU}-${__ITER}`;
  const emptyRes = get(`${env.feedServiceUrl}/feed?limit=${env.feedPageLimit}`, {
    headers: authHeaders(emptyUserId),
    tags: { scenario: 'feed-service', endpoint: 'empty-feed' },
  });
  assertStatus(emptyRes, 200, 'empty feed');
  const emptyBody = emptyRes.json();
  if (Array.isArray(emptyBody.items)) {
    metrics.feedItemsReturned.add(emptyBody.items.length);
  }

  const followerId = `feed-follower-${__VU}-${__ITER}`;
  const authorId = `feed-author-${__VU}-${__ITER}`;

  const followRes = post(`${env.graphServiceUrl}/follow/${authorId}`, {}, {
    headers: authHeaders(followerId),
    tags: { scenario: 'feed-service', endpoint: 'follow' },
  });
  assertStatus(followRes, 200, 'follow for feed');

  const postRes = post(`${env.postServiceUrl}/posts`, { content: `feed-post-${__VU}-${__ITER}` }, {
    headers: authHeaders(authorId),
    tags: { scenario: 'feed-service', endpoint: 'create-post' },
  });
  assertStatus(postRes, 201, 'create post for feed');
  const postBody = postRes.json();

  const feedResult = waitFor(() => {
    const feedRes = get(`${env.feedServiceUrl}/feed?limit=${env.feedPageLimit}`, {
      headers: authHeaders(followerId),
      tags: { scenario: 'feed-service', endpoint: 'feed' },
    });
    if (feedRes.status !== 200) {
      return null;
    }
    const body = feedRes.json();
    const items = body.items || [];
    const matches = items.filter((item) => item.post_id === postBody.post_id);
    if (matches.length > 0) {
      metrics.feedItemsReturned.add(items.length);
      return body;
    }
    return null;
  });

  if (feedResult.result) {
    const items = feedResult.result.items || [];
    assertNoDuplicates(items, (item) => item.post_id, 'feed items');
    assertOrderedByScoreDesc(items, 'feed ordering');
  }

  const page1Res = get(`${env.feedServiceUrl}/feed?limit=1`, {
    headers: authHeaders(followerId),
    tags: { scenario: 'feed-service', endpoint: 'feed-page-1' },
  });
  assertStatus(page1Res, 200, 'feed page 1');
  const page1 = page1Res.json();
  const cursor = getCursorFromResponse(page1);

  if (cursor) {
    const page2Res = get(`${env.feedServiceUrl}/feed?limit=1&cursor=${encodeURIComponent(cursor)}`, {
      headers: authHeaders(followerId),
      tags: { scenario: 'feed-service', endpoint: 'feed-page-2' },
    });
    assertStatus(page2Res, 200, 'feed page 2');
    const page2 = page2Res.json();
    assertCursorProgression(page1.items || [], page2.items || [], 'feed pagination');
  }

  if (__ENV.EXPECT_REDIS_DOWN === 'true') {
    const redisDownRes = get(`${env.feedServiceUrl}/feed?limit=${env.feedPageLimit}`, {
      headers: authHeaders(followerId),
      tags: { scenario: 'feed-service', endpoint: 'redis-down' },
    });
    if (redisDownRes.status === 503) {
      metrics.partialFeedResponses.add(1);
    }
    assertStatus(redisDownRes, 503, 'redis down');
  }
}
