export type OrderSide = 'buy' | 'sell';

export interface Level{
  price: number;
  quantity: number;
}

export interface BookMessage{
  type: 'book';
  securityId: number;
  bid: number | null;
  ask: number | null;
  spread: number | null;
  bids: Level[];
  asks: Level[];
}

export interface TradeMessage{
  type: 'trade';
  securityId: number;
  bidOrderId: number;
  askOrderId: number;
  price: number;
  quantity: number;
  filledAt: string; // ISO timestamp from the API
  aggressorSide: OrderSide;
}

export interface MarketTradeMessage{
  type: 'marketTrade';
  securityId: number;
  price: number;
  quantity: number;
  occurredAt: string;
  aggressorSide: OrderSide;
}

export interface TradingSession{
  sessionId: string; // since they're Guids not just digits
  createdAt: string;
}

export type MarketMessage = BookMessage | TradeMessage | MarketTradeMessage;
export type LiquidityRole = 'Maker' | 'Taker';

export interface PlaceOrderRequest{
  sessionId: string;
  securityId: number;
  username: string;
  side: 'Buy' | 'Sell';
  price: number;
  quantity: number;
}

export interface OrderAck{
  orderId: number;
  matched: boolean;
  fills: Array<{
    bidOrderId: number;
    askOrderId: number;
    price: number;
    quantity: number;
  }>;
}

export interface WorkingOrder{
  orderId: number;
  securityId: number;
  symbol: string;
  username: string;
  side: OrderSide;
  price: number;
  quantity: number;
  filledQuantity: number;
}

export interface Execution{
  executionId: string;
  matchId: string,
  sessionId: string;
  securityId: number;
  orderId: number;
  side: 'Buy' | 'Sell';
  price: number;
  quantity: number;
  executedAt: string;
  liquidityRole: LiquidityRole;
}
