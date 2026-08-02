import { vi } from 'vitest';
import { MarketDataService } from './MarketData.service';
import { MarketMessage } from './trading.models';

class MockWebSocket {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSING = 2;
  static readonly CLOSED = 3;

  static instances: MockWebSocket[] = [];

  readonly url: string;
  readyState = MockWebSocket.CONNECTING;

  onopen: ((event: Event) => void) | null = null;

  onmessage: ((event: MessageEvent<string>) => void) | null = null;

  onerror: ((event: Event) => void) | null = null;

  onclose: ((event: CloseEvent) => void) | null = null;

  readonly send = vi.fn<(data: string) => void>();

  constructor(url: string | URL) {
    this.url = url.toString();
    MockWebSocket.instances.push(this);
  }

  open(): void {
    this.readyState = MockWebSocket.OPEN;
    this.onopen?.(new Event('open'));
  }

  serverClose(): void {
    this.readyState = MockWebSocket.CLOSED;

    this.onclose?.(
      new CloseEvent('close', {
        code: 1006,
        reason: 'connection interrupted',
      }),
    );
  }

  close(code = 1000, reason = ''): void {
    if (this.readyState === MockWebSocket.CLOSED) return;

    this.readyState = MockWebSocket.CLOSED;

    this.onclose?.(
      new CloseEvent('close', {
        code,
        reason,
      }),
    );
  }
}

describe('MarketDataService', () => {
  let service: MarketDataService;

  beforeEach(() => {
    MockWebSocket.instances = [];

    vi.useFakeTimers();
    vi.stubGlobal('WebSocket', MockWebSocket);

    service = new MarketDataService();
  });

  afterEach(() => {
    service.disconnect();

    vi.useRealTimers();
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  it('subscribes to every security after connecting', () => {
    const onMessage = vi.fn<(message: MarketMessage) => void>();

    const onStatus = vi.fn<(status: string) => void>();

    service.connect([1, 2], onMessage, onStatus);

    const socket = MockWebSocket.instances[0];

    socket.open();

    expect(onStatus).toHaveBeenLastCalledWith('LIVE');

    expect(socket.send).toHaveBeenCalledTimes(2);

    expect(socket.send.mock.calls.map(([message]) => JSON.parse(message))).toEqual([
      {
        action: 'subscribe',
        securityId: 1,
      },
      {
        action: 'subscribe',
        securityId: 2,
      },
    ]);
  });

  it('reconnects and resubscribes after an interruption', () => {
    const onMessage = vi.fn<(message: MarketMessage) => void>();

    const onStatus = vi.fn<(status: string) => void>();

    service.connect([1], onMessage, onStatus);

    const firstSocket = MockWebSocket.instances[0];

    firstSocket.open();
    firstSocket.serverClose();

    expect(onStatus).toHaveBeenLastCalledWith('RECONNECTING');

    vi.advanceTimersByTime(1_000);

    expect(MockWebSocket.instances).toHaveLength(2);

    const secondSocket = MockWebSocket.instances[1];

    secondSocket.open();

    expect(secondSocket.send).toHaveBeenCalledWith(
      JSON.stringify({
        action: 'subscribe',
        securityId: 1,
      }),
    );

    expect(onStatus).toHaveBeenLastCalledWith('LIVE');
  });

  it('does not reconnect after a deliberate disconnect', () => {
    service.connect([1], vi.fn(), vi.fn());

    const socket = MockWebSocket.instances[0];

    socket.open();
    socket.serverClose();

    service.disconnect();

    vi.advanceTimersByTime(30_000);

    expect(MockWebSocket.instances).toHaveLength(1);
  });
});
