export function authHeaders(userId) {
  return {
    'Content-Type': 'application/json',
    'X-User-Id': userId,
  };
}

export function optionalAuthHeaders(userId) {
  if (!userId) {
    return { 'Content-Type': 'application/json' };
  }
  return authHeaders(userId);
}
