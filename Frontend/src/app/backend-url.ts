const configuredBackendOrigin = typeof BACKEND_ORIGIN === 'undefined' ? '' : BACKEND_ORIGIN;

function normalizeOrigin(origin: string): string {
  return origin.trim().replace(/\/+$/, '');
}

function normalizePath(path: string): string {
  return path.startsWith('/') ? path : `/${path}`;
}

export function buildHttpUrl(path: string, backendOrigin = configuredBackendOrigin): string {
  const normalizedPath = normalizePath(path);
  const normalizedOrigin = normalizeOrigin(backendOrigin);

  if (!normalizedOrigin) return normalizedPath;

  return `${normalizedOrigin}${normalizedPath}`;
}

export function buildWebSocketUrl(
  path: string,
  backendOrigin = configuredBackendOrigin,
  browserOrigin = typeof location === 'undefined' ? 'http://localhost' : location.origin,
): string {
  const normalizedPath = normalizePath(path);
  const normalizedBackendOrigin = normalizeOrigin(backendOrigin);
  const baseOrigin = normalizedBackendOrigin || browserOrigin;

  const url = new URL(normalizedPath, `${baseOrigin}/`);

  switch (url.protocol) {
    case 'https:':
      url.protocol = 'wss:';
      break;

    case 'http:':
      url.protocol = 'ws:';
      break;

    default:
      throw new Error(`Unsupported backend protocol '${url.protocol}'.`);
  }

  return url.toString();
}
