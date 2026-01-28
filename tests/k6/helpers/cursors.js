export function getCursorFromResponse(body) {
  return body.next_cursor || body.nextCursor || null;
}

export function normalizeCursor(cursor) {
  return cursor || null;
}
