import env from '../config/env.local.js';
import { post, get } from '../helpers/http.js';
import { authHeaders } from '../helpers/auth.js';
import { assertStatus, assertNoDuplicates, assertOrderedByScoreDesc } from '../helpers/assertions.js';
import { waitFor, metrics } from '../helpers/utils.js';

export default function hybridCelebrityScenario() {
  const followerId = `hybrid-follower-${__VU}-${__ITER}`;
  const normalAuthorId = `hybrid-author-${__VU}-${__ITER}`;
  const celebrityAuthorId = env.celebrityAuthorId;

  const followNormal = post(`${env.graphServiceUrl}/follow/${normalAuthorId}`, {}, {
    headers: authHeaders(followerId),
    tags: { scenario: 'hybrid-celebrity', endpoint: 'follow-normal' },
  });
  assertStatus(followNormal, 200, 'follow normal');

  const followCeleb = post(`${env.graphServiceUrl}/follow/${celebrityAuthorId}`, {}, {
    headers: authHeaders(followerId),
    tags: { scenario: 'hybrid-celebrity', endpoint: 'follow-celebrity' },
  });
  assertStatus(followCeleb, 200, 'follow celebrity');

  const normalPost = post(`${env.postServiceUrl}/posts`, { content: `normal-post-${__VU}-${__ITER}` }, {
    headers: authHeaders(normalAuthorId),
    tags: { scenario: 'hybrid-celebrity', endpoint: 'normal-post' },
  });
  assertStatus(normalPost, 201, 'create normal post');

  const celebrityPost = post(`${env.postServiceUrl}/posts`, { content: `celebrity-post-${__VU}-${__ITER}` }, {
    headers: authHeaders(celebrityAuthorId),
    tags: { scenario: 'hybrid-celebrity', endpoint: 'celebrity-post' },
  });
  assertStatus(celebrityPost, 201, 'create celebrity post');

  const normalPostId = normalPost.json().post_id;
  const celebrityPostId = celebrityPost.json().post_id;

  const feedResult = waitFor(() => {
    const feedRes = get(`${env.feedServiceUrl}/feed?limit=${env.feedPageLimit}`, {
      headers: authHeaders(followerId),
      tags: { scenario: 'hybrid-celebrity', endpoint: 'feed' },
    });
    if (feedRes.status !== 200) {
      return null;
    }
    const body = feedRes.json();
    const items = body.items || [];
    const hasNormal = items.some((item) => item.post_id === normalPostId);
    const hasCelebrity = items.some((item) => item.post_id === celebrityPostId);
    if (hasNormal && hasCelebrity) {
      metrics.feedItemsReturned.add(items.length);
      return items;
    }
    return null;
  });

  if (feedResult.result) {
    assertNoDuplicates(feedResult.result, (item) => item.post_id, 'hybrid feed');
    assertOrderedByScoreDesc(feedResult.result, 'hybrid ordering');
  }

  if (__ENV.REQUIRE_CELEBRITY === 'true') {
    const celebListRes = get(`${env.graphServiceUrl}/users/${followerId}/following/celebrity?limit=${env.graphPageLimit}`, {
      headers: authHeaders(followerId),
      tags: { scenario: 'hybrid-celebrity', endpoint: 'celebrity-list' },
    });
    assertStatus(celebListRes, 200, 'celebrity following list');
    const celebItems = celebListRes.json().items || [];
    assertNoDuplicates(celebItems, (item) => item.followed_id, 'celebrity list');
  }
}
