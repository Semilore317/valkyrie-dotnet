import {
  Component,
  computed,
  inject,
  OnDestroy,
  OnInit,
  signal
} from '@angular/core';
import {DatePipe} from '@angular/common';
import {MarketDataService} from './MarketData.service';
import {TradingApiService} from './TradingApi.service';
import {BookMessage, MarketMessage, WorkingOrder} from './trading.models';

interface Instrument {
  securityId: number;
  symbol: string;
  name: string;
  last: number;
  changePercent: number;
}

interface Level {
  price: number;
  quantity: number;
}

interface LadderRow {
  price: number;
  quantity: number;
  cumulative: number;
  depthPercentage: number;
  isBest: boolean;
}

interface TapeRow {
  id: string,
  price: number;
  quantity: number;
  side: 'buy' | 'sell';
  filledAt: string;
}

interface TracePoint {
  price: number;
  x: number;
  y: number;
}

@Component({
  selector: 'app-root',
  imports: [DatePipe],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit, OnDestroy {
  private readonly marketData = inject(MarketDataService);
  private readonly api = inject(TradingApiService);

  readonly tape = signal<TapeRow[]>([]);
  readonly midTrace = signal<number[]>([]);
  readonly traceHover = signal<TracePoint | null>(null);
  private traceTimer?: number;
  readonly workingOrders = signal<WorkingOrder[]>([]);
  readonly connectionStatus = signal('CONNECTING');
  readonly dark = signal(this.getInitialTheme());
  readonly activeId = signal(1);

  readonly instruments = signal<Instrument[]>([
    {securityId: 1, symbol: 'MSFT', name: 'Microsoft Corp', last: 418.05, changePercent: 1.5},
    {securityId: 2, symbol: 'AAPL', name: 'Apple Inc', last: 227.15, changePercent: -0.18},
    {securityId: 3, symbol: 'NVDA', name: 'Space Exploration Technologies Corp', last: 180.55, changePercent: 2.94},
  ]);

  readonly asks = signal<Level[]>([
    {price: 41810, quantity: 120}, {price: 41815, quantity: 340},
    {price: 41820, quantity: 120}, {price: 41825, quantity: 560},
    {price: 41835, quantity: 150}, {price: 41850, quantity: 700},
  ]);

  readonly bids = signal<Level[]>([
    {price: 41800, quantity: 260}, {price: 41795, quantity: 480},
    {price: 41790, quantity: 190}, {price: 41780, quantity: 620},
    {price: 41770, quantity: 300}, {price: 41755, quantity: 540},
  ]);

  readonly side = signal<'buy' | 'sell'>('buy');
  readonly trader = signal('Jon Snow');
  readonly priceInput = signal('418.50');
  readonly quantityInput = signal('500');
  readonly submitError = signal('');
  readonly isSubmitting = signal(false);

  readonly activeInstrument = computed(() =>
    this.instruments().find(i => i.securityId === this.activeId()) ?? null
  );

  readonly bestAsk = computed(() => this.asks()[0]?.price ?? null);
  readonly bestBid = computed(() => this.bids()[0]?.price ?? null);

  readonly spreadCents = computed(() => {
    const a = this.bestAsk(), b = this.bestBid();
    return a !== null && b !== null ? a - b : null;
  });

  readonly mid = computed(() => {
    const a = this.bestAsk(), b = this.bestBid();
    return a !== null && b !== null ? (a + b) / 2 : null;
  });

  readonly askRows = computed(() => this.toLadder(this.asks()).reverse());
  readonly bidRows = computed(() => this.toLadder(this.bids()));

  readonly priceCents = computed(() => Math.round((parseFloat(this.priceInput()) || 0) * 100));
  readonly quantity = computed(() => parseInt(this.quantityInput(), 10) || 0);
  readonly notional = computed(() => (this.priceCents() * this.quantity()) / 100);

  private readonly maxCumulative = computed(() => {
    const total = (ls: Level[]) => ls.reduce((s, l) => s + l.quantity, 0);
    return Math.max(1, total(this.asks()), total(this.bids()));
  });

  readonly totalBookDepth = computed(() =>
    [...this.asks(), ...this.bids()]
      .reduce((total, level) => total + level.quantity, 0)
  );

  readonly tracePoints = computed<TracePoint[]>(() => {
    const prices = this.midTrace();

    if (prices.length === 0) {
      return [];
    }

    const width = 500;
    const height = 46;
    const horizontalPadding = 10;
    const verticalPadding = 12;
    const observedMin = Math.min(...prices);
    const observedMax = Math.max(...prices);
    const centre = (observedMin + observedMax) / 2;
    const range = Math.max(12, observedMax - observedMin + 4);
    const minimum = centre - range / 2;

    return prices.map((price, index) => {
      const x = prices.length === 1
        ? width - horizontalPadding
        : horizontalPadding + (index / (prices.length - 1)) * (width - horizontalPadding * 2);
      const y = height - verticalPadding -
        ((price - minimum) / range) * (height - verticalPadding * 2);

      return { price, x, y };
    });
  });

  readonly tracePath = computed(() =>
    this.tracePoints().map((point, index) =>
      `${index === 0 ? 'M' : 'L'} ${point.x.toFixed(1)} ${point.y.toFixed(1)}`,
    ).join(' '),
  );

  readonly traceTip = computed(() => {
    const points = this.tracePoints();
    return points.length === 0 ? null : points[points.length - 1];
  });

  ngOnInit(): void {
    this.applyTheme();
    this.subscribeToInstrument(this.activeId());
    this.startTraceSampling();
  }

  ngOnDestroy(): void {
    this.marketData.disconnect();

    if(this.traceTimer !==  undefined)
      window.clearInterval(this.traceTimer);
  }

  toggleTheme(): void {
    this.dark.update(isDark => !isDark);

    const theme = this.dark() ? 'dark' : 'light';

    localStorage.setItem('theme', theme);
    this.applyTheme();
  }

  select(id: number): void {
    if (id === this.activeId()) return;

    this.activeId.set(id);
    this.asks.set([]);
    this.bids.set([]);
    this.midTrace.set([]);
    this.traceHover.set(null);
    this.subscribeToInstrument(id);
  }

  onTraceMove(event: MouseEvent): void {
    const points = this.tracePoints();
    const svg = event.currentTarget as SVGSVGElement;

    if (points.length === 0 || svg.clientWidth === 0) {
      return;
    }

    const bounds = svg.getBoundingClientRect();
    const fraction = Math.max(0, Math.min(1, (event.clientX - bounds.left) / bounds.width));
    const index = Math.round(fraction * (points.length - 1));

    this.traceHover.set(points[index]);
  }

  clearTraceHover(): void {
    this.traceHover.set(null);
  }

  setSide(s: 'buy' | 'sell'): void {
    this.side.set(s);
  }

  updateInput(targetSignal: { set: (val: string) => void }, event: Event): void {
    targetSignal.set((event.target as HTMLInputElement).value);
  }

  submit(event: Event): void {
    event?.preventDefault();
    this.submitError.set('');

    if (!this.trader().trim()) {
      this.submitError.set('Enter a trader name');
      return;
    }

    if (this.priceCents() <= 0 || this.quantity() <= 0) {
      this.submitError.set('Price & Quantity must be greater than 0');
      return;
    }

    this.isSubmitting.set(true);

    this.api.placeOrder({
      securityId: this.activeId(),
      username: this.trader().trim(),
      side: this.side() === 'buy' ? 'Buy' : 'Sell',
      price: this.priceCents(),
      quantity: this.quantity(),
    }).subscribe({
      next: ack => {
        if (!ack.matched) {
          const instrument = this.activeInstrument();

          if (instrument) {
            this.workingOrders.update(orders => [
              {
                orderId: ack.orderId,
                securityId: instrument.securityId,
                symbol: instrument.symbol,
                username: this.trader().trim(),
                side: this.side(),
                price: this.priceCents(),
                quantity: this.quantity(),
                filledQuantity: 0,
              },
              ...orders,
            ]);
          }
        }
        this.isSubmitting.set(false);
      },
      error: () => {
        this.submitError.set('Order not accepted.');
        this.isSubmitting.set(false);
      },
    });
  }

  cancelOrder(order: WorkingOrder): void {
    this.api.cancelOrder(
      order.securityId,
      order.orderId,
      order.username
    ).subscribe({
      next: () => {
        this.workingOrders.update(orders =>
          orders.filter(item => item.orderId !== order.orderId));
      },
      error: () => this.submitError.set('Order could not be cancelled')
    })
  }


  private applyTheme(): void {
    if (typeof document !== 'undefined') {
      document.documentElement.setAttribute('data-theme', this.dark() ? 'dark' : 'light');
    }
  }

  private subscribeToInstrument(securityId: number): void {
    this.connectionStatus.set('CONNECTING');

    this.marketData.connect(
      securityId,
      (message: MarketMessage) => this.handleMarketMessage(message),
      (status: string) => this.connectionStatus.set(status)
    );
  }

  private handleMarketMessage(message: MarketMessage): void {
    if (message.type === 'book') {
      this.applyBook(message);
      return;
    }

    const side: TapeRow['side'] =
      this.bestAsk() !== null && message.price >= this.bestAsk()!
        ? 'buy'
        : 'sell';

    this.tape.update(rows => [
      {
        id: `${message.filledAt}-${message.price}-${message.quantity}`,
        price: message.price,
        quantity: message.quantity,
        side,
        filledAt: message.filledAt
      }, ...rows,
    ].slice(0, 60));
  }

  private applyBook(book: BookMessage): void {
    this.bids.set(book.bids);
    this.asks.set(book.asks);

    if (book.bid !== null && book.ask !== null && this.midTrace().length === 0) {
      const newMid = (book.bid + book.ask) / 2;

      this.midTrace.set(Array.from({length: 90},() => newMid));
    }
  }

  private toLadder(levels: Level[]): LadderRow[] {
    const max = this.maxCumulative();
    let cumulative = 0;
    return levels.map((l, i) => {
      cumulative += l.quantity;
      return {
        price: l.price,
        quantity: l.quantity,
        cumulative,
        depthPercentage: (cumulative / max) * 100,
        isBest: i === 0
      };
    });
  }

  private getInitialTheme(): boolean {
    if (typeof window === 'undefined') {
      return false;
    }

    const savedTheme = localStorage.getItem('theme');

    if (savedTheme === 'dark')
      return true;

    if (savedTheme === 'light')
      return false;

    //first visit: follow the device/broswer prefernce
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
  }

  private startTraceSampling(): void{
      this.traceTimer = window.setInterval(() =>{
        const currentMid = this.mid();

        if(currentMid === null)
          return;

        this.midTrace.update(price =>[
          ...price.slice(-89),
          currentMid
        ]);
      },1000);
  }
}
