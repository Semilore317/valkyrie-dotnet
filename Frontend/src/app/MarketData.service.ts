import {Injectable} from '@angular/core';
import {MarketMessage} from './trading.models';

@Injectable({providedIn: 'root'})
export class MarketDataService {
  private socket?: WebSocket;

  connect(
    securityIds: number[],
    onMessage: (message: MarketMessage) => void,
    onStatus: (status: string) => void,
  ): void {
    this.disconnect();

    const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    const url = `${protocol}//${location.host}/ws/marketdata`;

    this.socket = new WebSocket(url);

    this.socket.onopen = () => {
      onStatus('LIVE');

      for (const securityId of securityIds) {
        this.socket?.send(JSON.stringify({
          action: 'subscribe',
          securityId,
        }));
      }
    };

    this.socket.onmessage = (event: MessageEvent<string>) => {
      onMessage(JSON.parse(event.data) as MarketMessage);
    };

    this.socket.onerror = () => onStatus('CONNECTION ERROR');
    this.socket.onclose = () => onStatus('OFFLINE');
  }

  disconnect(): void {
    if (!this.socket) return;

    if (this.socket.readyState === WebSocket.OPEN) {
      this.socket.close();
    }

    this.socket = undefined;
  }
}
