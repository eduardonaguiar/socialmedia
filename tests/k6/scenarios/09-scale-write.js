import env from '../config/env.local.js';
import { post } from '../helpers/http.js';
import { authHeaders } from '../helpers/auth.js';
import { assertStatus } from '../helpers/assertions.js';

export default function scaleWriteScenario() {
  const hotAuthorId = env.defaultAuthorId;
  const authorId = __ITER % 2 === 0 ? hotAuthorId : `scale-writer-${__VU}-${__ITER}`;

  const postRes = post(`${env.postServiceUrl}/posts`, { content: `scale-post-${__VU}-${__ITER}` }, {
    headers: authHeaders(authorId),
    tags: { scenario: 'scale-write', endpoint: 'create-post' },
  });
  assertStatus(postRes, 201, 'scale write post');
}
