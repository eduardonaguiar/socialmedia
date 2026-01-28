import env from '../config/env.local.js';
import { post, del, get } from '../helpers/http.js';
import { authHeaders } from '../helpers/auth.js';
import { assertStatus, assertJsonFields, assertNoDuplicates, assertCursorProgression } from '../helpers/assertions.js';
import { getCursorFromResponse } from '../helpers/cursors.js';

export default function graphServiceScenario() {
  const followerId = `follower-${__VU}-${__ITER}`;
  const targets = [`target-a-${__VU}-${__ITER}`, `target-b-${__VU}-${__ITER}`, `target-c-${__VU}-${__ITER}`];

  const followRes = post(`${env.graphServiceUrl}/follow/${targets[0]}`, {}, {
    headers: authHeaders(followerId),
    tags: { scenario: 'graph-service', endpoint: 'follow' },
  });
  assertStatus(followRes, 200, 'follow');
  assertJsonFields(followRes, ['follower_id', 'target_user_id', 'followed_at_utc'], 'follow');

  const idempotentRes = post(`${env.graphServiceUrl}/follow/${targets[0]}`, {}, {
    headers: authHeaders(followerId),
    tags: { scenario: 'graph-service', endpoint: 'follow-idempotent' },
  });
  assertStatus(idempotentRes, 200, 'idempotent follow');

  const selfFollowRes = post(`${env.graphServiceUrl}/follow/${followerId}`, {}, {
    headers: authHeaders(followerId),
    tags: { scenario: 'graph-service', endpoint: 'self-follow' },
  });
  assertStatus(selfFollowRes, 400, 'self follow rejected');

  for (const target of targets.slice(1)) {
    const res = post(`${env.graphServiceUrl}/follow/${target}`, {}, {
      headers: authHeaders(followerId),
      tags: { scenario: 'graph-service', endpoint: 'follow' },
    });
    assertStatus(res, 200, 'follow');
  }

  const page1Res = get(`${env.graphServiceUrl}/users/${followerId}/following?limit=2`, {
    headers: authHeaders(followerId),
    tags: { scenario: 'graph-service', endpoint: 'following-page-1' },
  });
  assertStatus(page1Res, 200, 'following page 1');
  const page1 = page1Res.json();

  const cursor = getCursorFromResponse(page1);
  const page2Res = get(`${env.graphServiceUrl}/users/${followerId}/following?limit=2&cursor=${encodeURIComponent(cursor || '')}`, {
    headers: authHeaders(followerId),
    tags: { scenario: 'graph-service', endpoint: 'following-page-2' },
  });
  assertStatus(page2Res, 200, 'following page 2');
  const page2 = page2Res.json();

  const combined = [...(page1.items || []), ...(page2.items || [])];
  assertNoDuplicates(combined, (item) => item.followed_id, 'following list');
  assertCursorProgression(page1.items || [], page2.items || [], 'following pagination');

  const repeatPage1Res = get(`${env.graphServiceUrl}/users/${followerId}/following?limit=2`, {
    headers: authHeaders(followerId),
    tags: { scenario: 'graph-service', endpoint: 'following-page-1-repeat' },
  });
  assertStatus(repeatPage1Res, 200, 'following page 1 repeat');

  const repeatItems = repeatPage1Res.json().items || [];
  assertNoDuplicates(repeatItems, (item) => item.followed_id, 'following page deterministic');

  const unfollowRes = del(`${env.graphServiceUrl}/follow/${targets[2]}`, {
    headers: authHeaders(followerId),
    tags: { scenario: 'graph-service', endpoint: 'unfollow' },
  });
  assertStatus(unfollowRes, 204, 'unfollow');
}
