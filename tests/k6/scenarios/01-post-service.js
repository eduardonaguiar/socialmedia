import env from '../config/env.local.js';
import { post, get } from '../helpers/http.js';
import { authHeaders, optionalAuthHeaders } from '../helpers/auth.js';
import { assertStatus, assertJsonFields, assertLatency, assertArrayNotEmpty } from '../helpers/assertions.js';
import { buildContent, waitFor, metrics } from '../helpers/utils.js';

export default function postServiceScenario() {
  const userId = `user-${__VU}-${__ITER}`;
  const content = buildContent('post');

  const createRes = post(`${env.postServiceUrl}/posts`, { content }, {
    headers: authHeaders(userId),
    tags: { scenario: 'post-service', endpoint: 'create-post' },
  });
  assertStatus(createRes, 201, 'create post');
  assertLatency(createRes, env.latencyP95Ms, 'create post');
  const created = assertJsonFields(createRes, ['post_id', 'author_id', 'content', 'created_at'], 'create post');

  const missingHeaderRes = post(`${env.postServiceUrl}/posts`, { content }, {
    headers: optionalAuthHeaders(null),
    tags: { scenario: 'post-service', endpoint: 'create-post-missing-user' },
  });
  assertStatus(missingHeaderRes, 400, 'missing user header');

  const readRes = get(`${env.postServiceUrl}/posts/${created.post_id}`, {
    headers: authHeaders(userId),
    tags: { scenario: 'post-service', endpoint: 'read-post' },
  });
  assertStatus(readRes, 200, 'read existing post');
  assertJsonFields(readRes, ['post_id', 'author_id', 'content', 'created_at'], 'read existing post');

  const missingRes = get(`${env.postServiceUrl}/posts/00000000-0000-0000-0000-000000000001`, {
    headers: authHeaders(userId),
    tags: { scenario: 'post-service', endpoint: 'read-missing-post' },
  });
  assertStatus(missingRes, 404, 'read missing post');

  const timelineResult = waitFor(() => {
    const timelineRes = get(`${env.postServiceUrl}/authors/${userId}/posts?limit=${env.authorTimelineLimit}`, {
      headers: authHeaders(userId),
      tags: { scenario: 'post-service', endpoint: 'author-timeline' },
    });
    if (timelineRes.status !== 200) {
      return null;
    }
    const body = timelineRes.json();
    if (!body.items || body.items.length === 0) {
      return null;
    }
    const match = body.items.find((item) => item.post_id === created.post_id);
    if (match) {
      metrics.feedItemsReturned.add(body.items.length);
      return body.items;
    }
    return null;
  });

  assertArrayNotEmpty(timelineResult.result || [], 'author timeline');
  if (timelineResult.result) {
    metrics.eventualConsistencyDelayMs.add(timelineResult.elapsedMs);
  }
}
