import { buildHttpUrl, buildWebSocketUrl } from './backend-url';

describe('backend URL configuration', () => {
  it('uses relative HTTP URLS when no backend is configured', () => {
    expect(buildHttpUrl('/instruments', '')).toBe('/instruments');
  });

  it('prefixes HTTP paths with the configured backend', () => {
    expect(buildHttpUrl('/instruments', 'https://valkyrie-api.onrender.com')).toBe(
      'https://valkyrie-api.onrender.com/instruments',
    );
  });

  it('creates a secure production WebSocket URL', () => {
    expect(
      buildWebSocketUrl(
        '/ws/marketdata',
        'https://valkyrie-api.onrender.com',
        'http://localhost:4200',
      ),
    ).toBe('wss://valkyrie-api.onrender.com/ws/marketdata');
  });

  it('uses the browser origin for local WebSockets', () => {
    expect(buildWebSocketUrl('/ws/marketdata', '', 'http://localhost:4200')).toBe(
      'ws://localhost:4200/ws/marketdata',
    );
  });
});
