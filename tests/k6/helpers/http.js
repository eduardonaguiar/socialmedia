import http from 'k6/http';
import { check } from 'k6';
import env from '../config/env.local.js';

export function request(method, url, payload, params = {}) {
  const mergedParams = {
    timeout: `${env.requestTimeoutMs}ms`,
    tags: params.tags || {},
    headers: params.headers || {},
  };

  const response = http.request(method, url, payload, mergedParams);
  check(response, {
    'http status is not 0': (res) => res.status !== 0,
  });
  return response;
}

export function get(url, params) {
  return request('GET', url, null, params);
}

export function post(url, payload, params) {
  return request('POST', url, JSON.stringify(payload), params);
}

export function del(url, params) {
  return request('DELETE', url, null, params);
}
