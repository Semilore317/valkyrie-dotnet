import { Injectable } from '@angular/core';
import { buildWebSocketUrl } from './backend-url';
import { MarketMessage } from './trading.models';

@Injectable({ providedIn: 'root' })
export class MarketDataService {
  private socket?: WebSocket;
  private reconnectTimer?: number;
  private reconnectAttempt = 0;
  private shouldReconnect = false;

  private securityIds: number[] = [];
  private onMessage: ((message: MarketMessage) => void) | undefined;

  private onStatus: ((status: string) => void) | undefined;

  private readonly initialReconnectDelayMs = 1_000;
  private readonly maximumReconnectDelayMs = 30_000;

  connect(
    securityIds: number[],
    onMessage: (message: MarketMessage) => void,
    onStatus: (status: string) => void,
  ): void {
    this.disconnect();

    this.securityIds = [...securityIds];
    this.onMessage = onMessage;
    this.onStatus = onStatus;
    this.shouldReconnect = true;

    this.openSocket();
  }

  disconnect(): void {
    this.shouldReconnect = false;
    this.reconnectAttempt = 0;

    this.clearReconnectTimer();
    this.closeSocket();
  }

  private openSocket(): void {
    if (!this.shouldReconnect) return;

    this.clearReconnectTimer();
    this.onStatus?.('CONNECTING');

    let socket: WebSocket;

    try {
      socket = new WebSocket(buildWebSocketUrl('/ws/marketdata'));
    } catch {
      this.onStatus?.('CONNECTION ERROR');
      this.scheduleReconnect();
      return;
    }

    this.socket = socket;

    socket.onopen = () => {
      if (this.socket !== socket) return;

      this.reconnectAttempt = 0;
      this.onStatus?.('LIVE');

      for (const securityId of this.securityIds) {
        socket.send(
          JSON.stringify({
            action: 'subscribe',
            securityId,
          }),
        );
      }
    };

    socket.onmessage = (event: MessageEvent<string>) => {
      if (this.socket !== socket) return;

      const message = JSON.parse(event.data) as MarketMessage;

      this.onMessage?.(message);
    };

    socket.onerror = () => {
      if (this.socket !== socket) return;

      this.onStatus?.('CONNECTION ERROR');
    };

    socket.onclose = () => {
      if (this.socket !== socket) return;

      this.socket = undefined;

      if (!this.shouldReconnect) return;

      this.onStatus?.('RECONNECTING');
      this.scheduleReconnect();
    };
  }

  private scheduleReconnect(): void {
    if (!this.shouldReconnect || this.reconnectTimer !== undefined) {
      return;
    }

    const delay = Math.min(
      this.initialReconnectDelayMs * 2 ** this.reconnectAttempt,
      this.maximumReconnectDelayMs,
    );

    this.reconnectAttempt += 1;

    this.reconnectTimer = window.setTimeout(() => {
      this.reconnectTimer = undefined;
      this.openSocket();
    }, delay);
  }

  private clearReconnectTimer(): void {
    if (this.reconnectTimer === undefined) return;

    window.clearTimeout(this.reconnectTimer);
    this.reconnectTimer = undefined;
  }

  private closeSocket(): void {
    const socket = this.socket;
    this.socket = undefined;

    if (!socket) return;

    socket.onopen = null;
    socket.onmessage = null;
    socket.onerror = null;
    socket.onclose = null;

    if (socket.readyState !== WebSocket.CONNECTING && socket.readyState !== WebSocket.OPEN) {
      return;
    }

    try {
      socket.close(1000, 'client disconnect');
    } catch {
      // A connecting socket can race with browser shutdown.
    }
  }
}
