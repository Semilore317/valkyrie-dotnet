import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Execution,
  OrderAck,
  PlaceOrderRequest,
  TradingSession,
  Instrument,
  MarketDataStatus,
} from './trading.models';

@Injectable({ providedIn: 'root' })
export class TradingApiService {
  private readonly http = inject(HttpClient);

  getInstruments(): Observable<Instrument[]> {
    return this.http.get<Instrument[]>('/instruments');
  }

  getMarketDataStatus(): Observable<MarketDataStatus> {
    return this.http.get<MarketDataStatus>('/market-data/status');
  }

  createSession(): Observable<TradingSession> {
    return this.http.post<TradingSession>('/sessions', {});
  }

  getExecutions(sessionId: string, securityId?: number): Observable<Execution[]> {
    const url = `/sessions/${sessionId}/executions`;

    if (securityId === undefined) return this.http.get<Execution[]>(url);

    return this.http.get<Execution[]>(url, {
      params: { securityId },
    });
  }

  placeOrder(order: PlaceOrderRequest): Observable<OrderAck> {
    return this.http.post<OrderAck>('/orders', order);
  }

  cancelOrder(securityId: number, orderId: number, username: string): Observable<void> {
    return this.http.delete<void>(`/instruments/${securityId}/orders/${orderId}`, {
      params: { username },
    });
  }
}
