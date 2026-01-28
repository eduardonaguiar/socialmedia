import { check } from 'k6';
import { metrics } from './utils.js';

export function assertStatus(response, expectedStatus, label) {
  check(response, {
    [`${label} status is ${expectedStatus}`]: (res) => res.status === expectedStatus,
  });
}

export function assertStatusIn(response, expectedStatuses, label) {
  check(response, {
    [`${label} status in ${expectedStatuses.join(',')}`]: (res) => expectedStatuses.includes(res.status),
  });
}

export function assertJsonFields(response, fields, label) {
  const body = response.json();
  check(body, {
    [`${label} has fields`]: () => fields.every((field) => body[field] !== undefined),
  });
  return body;
}

export function assertArrayNotEmpty(items, label) {
  check(items, {
    [`${label} not empty`]: (arr) => Array.isArray(arr) && arr.length > 0,
  });
}

export function assertNoDuplicates(items, keyFn, label) {
  const seen = new Set();
  let duplicates = 0;
  for (const item of items) {
    const key = keyFn(item);
    if (seen.has(key)) {
      duplicates += 1;
    } else {
      seen.add(key);
    }
  }
  if (duplicates > 0) {
    metrics.duplicateItemsDetected.add(duplicates);
  }
  check(duplicates, {
    [`${label} has no duplicates`]: (count) => count === 0,
  });
}

export function assertOrderedByScoreDesc(items, label) {
  let ordered = true;
  for (let i = 1; i < items.length; i += 1) {
    if (items[i].score > items[i - 1].score) {
      ordered = false;
      break;
    }
  }
  check(ordered, {
    [`${label} ordered by score desc`]: () => ordered,
  });
}

export function assertCursorProgression(firstPage, secondPage, label) {
  const firstIds = new Set(firstPage.map((item) => item.post_id || item.followed_id || item.postId || item.followedId));
  const overlap = secondPage.filter((item) =>
    firstIds.has(item.post_id || item.followed_id || item.postId || item.followedId)
  );
  check(overlap, {
    [`${label} pages do not overlap`]: (items) => items.length === 0,
  });
}

export function assertLatency(response, maxMs, label) {
  check(response, {
    [`${label} latency < ${maxMs}ms`]: (res) => res.timings.duration < maxMs,
  });
}
