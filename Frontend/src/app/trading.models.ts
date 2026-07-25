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
}

export type MarketMessage = BookMessage | TradeMessage;

export interface PlaceOrderRequest{
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
