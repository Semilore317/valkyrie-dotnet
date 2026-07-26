
import {HttpClient} from '@angular/common/http';
import {inject, Injectable} from '@angular/core';
import {Observable} from 'rxjs';
import {
  Execution,
  OrderAck,
  PlaceOrderRequest,
  TradingSession,
} from './trading.models';

@Injectable({providedIn: 'root'})
export class TradingApiService {
  private readonly http = inject(HttpClient);

  createSession(): Observable<TradingSession> {
    return this.http.post<TradingSession>(
      '/sessions',
      {}
    );
  }

  getExecutions(
    sessionId: string,
    securityId?: number
  ): Observable<Execution[]> {
    const params = securityId === undefined
      ? {}
      : {securityId};

    return this.http.get<Execution[]>(
      `/sessions/${sessionId}/executions`,
      {params}
    );
  }

  placeOrder(
    order: PlaceOrderRequest
  ): Observable<OrderAck> {
    return this.http.post<OrderAck>(
      '/orders',
      order
    );
  }

  cancelOrder(
    securityId: number,
    orderId: number,
    username: string,
  ): Observable<void> {
    return this.http.delete<void>(
      `/instruments/${securityId}/orders/${orderId}`,
      {params: {username}}
    );
  }
}
