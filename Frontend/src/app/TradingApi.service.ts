import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { buildHttpUrl } from './backend-url';
import {
  Execution,
  Instrument,
  MarketDataStatus,
  OrderAck,
  PlaceOrderRequest,
  TradingSession,
} from './trading.models';

@Injectable({ providedIn: 'root' })
export class TradingApiService {
  private readonly http = inject(HttpClient);

  getInstruments(): Observable<Instrument[]> {
    return this.http.get<Instrument[]>(buildHttpUrl('/instruments'));
  }

  getMarketDataStatus(): Observable<MarketDataStatus> {
    return this.http.get<MarketDataStatus>(buildHttpUrl('/market-data/status'));
  }

  createSession(): Observable<TradingSession> {
    return this.http.post<TradingSession>(buildHttpUrl('/sessions'), {});
  }

  getExecutions(sessionId: string, securityId?: number): Observable<Execution[]> {
    const url = buildHttpUrl(`/sessions/${sessionId}/executions`);

    if (securityId === undefined) return this.http.get<Execution[]>(url);

    return this.http.get<Execution[]>(url, {
      params: { securityId },
    });
  }

  placeOrder(order: PlaceOrderRequest): Observable<OrderAck> {
    return this.http.post<OrderAck>(buildHttpUrl('/orders'), order);
  }

  cancelOrder(securityId: number, orderId: number, username: string): Observable<void> {
    return this.http.delete<void>(buildHttpUrl(`/instruments/${securityId}/orders/${orderId}`), {
      params: { username },
    });
  }
}
