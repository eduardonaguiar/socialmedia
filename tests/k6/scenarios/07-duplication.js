import env from '../config/env.local.js';
import { post, get } from '../helpers/http.js';
import { authHeaders } from '../helpers/auth.js';
import { assertStatus, assertNoDuplicates } from '../helpers/assertions.js';
import { waitFor, metrics } from '../helpers/utils.js';

export default function duplicationScenario() {
  const followerId = `dup-follower-${__VU}-${__ITER}`;
  const authorId = `dup-author-${__VU}-${__ITER}`;

  const followRes = post(`${env.graphServiceUrl}/follow/${authorId}`, {}, {
    headers: authHeaders(followerId),
    tags: { scenario: 'duplication', endpoint: 'follow' },
  });
  assertStatus(followRes, 200, 'follow');

  const postRes = post(`${env.postServiceUrl}/posts`, { content: `dup-post-${__VU}-${__ITER}` }, {
    headers: authHeaders(authorId),
    tags: { scenario: 'duplication', endpoint: 'create-post' },
  });
  assertStatus(postRes, 201, 'create post');
  const postId = postRes.json().post_id;

  const feedResult = waitFor(() => {
    const feedRes = get(`${env.feedServiceUrl}/feed?limit=${env.feedPageLimit}`, {
      headers: authHeaders(followerId),
      tags: { scenario: 'duplication', endpoint: 'feed' },
    });
    if (feedRes.status !== 200) {
      return null;
    }
    const items = feedRes.json().items || [];
    const matches = items.filter((item) => item.post_id === postId);
    if (matches.length > 0) {
      return items;
    }
    return null;
  });

  if (feedResult.result) {
    metrics.feedItemsReturned.add(feedResult.result.length);
    const matches = feedResult.result.filter((item) => item.post_id === postId);
    assertNoDuplicates(matches, (item) => item.post_id, 'dedupe after initial fanout');
  }

  const repeatFollowRes = post(`${env.graphServiceUrl}/follow/${authorId}`, {}, {
    headers: authHeaders(followerId),
    tags: { scenario: 'duplication', endpoint: 'follow-repeat' },
  });
  assertStatus(repeatFollowRes, 200, 'repeat follow');

  const repeatFeedRes = get(`${env.feedServiceUrl}/feed?limit=${env.feedPageLimit}`, {
    headers: authHeaders(followerId),
    tags: { scenario: 'duplication', endpoint: 'feed-repeat' },
  });
  assertStatus(repeatFeedRes, 200, 'feed repeat');
  const repeatItems = repeatFeedRes.json().items || [];
  const repeatMatches = repeatItems.filter((item) => item.post_id === postId);
  assertNoDuplicates(repeatMatches, (item) => item.post_id, 'dedupe after repeat follow');
}
