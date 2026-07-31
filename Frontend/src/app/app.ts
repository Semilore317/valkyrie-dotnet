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
import {
  BookMessage,
  Execution,
  MarketMessage,
  WorkingOrder,
  TradeMessage
} from './trading.models';

interface Instrument {
  securityId: number;
  symbol: string;
  name: string;
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
  readonly traceHover = signal<TracePoint | null>(null);
  private traceTimer?: number;
  readonly connectionStatus = signal('CONNECTING');
  readonly dark = signal(this.getInitialTheme());
  readonly activeId = signal(1);

  readonly workingOrders = signal<WorkingOrder[]>([]);
  readonly sessionId = signal<string | null>(null);
  readonly executions = signal<Execution[]>([]);
  readonly executionsLoading = signal(false);
  readonly executionError = signal('');

  readonly instruments = signal<Instrument[]>([
    {securityId: 1, symbol: 'MSFT', name: 'Microsoft Corp'},
    {securityId: 2, symbol: 'AAPL', name: 'Apple Inc'},
    {securityId: 3, symbol: 'AMZN', name: 'Amazon.com Inc'},
    {securityId: 4, symbol: 'GOOG', name: "Google Inc"},
    {securityId: 5, symbol: 'INTC', name: "Intel Corp"}
  ]);

  readonly asks = signal<Level[]>([]);
  readonly bids = signal<Level[]>([]);

  readonly side = signal<'buy' | 'sell'>('buy');
  readonly trader = signal('Jon Snow');
  readonly quantityInput = signal('');
  readonly priceInput = signal('');
  readonly submitError = signal('');
  readonly isSubmitting = signal(false);
  readonly tracesByInstrument = signal<Record<number, number[]>>({});
  readonly booksByInstrument = signal<Record<number, BookMessage>>({});
  readonly latestMidsByInstrument = signal<Record<number, number>>({});

  readonly midTrace = computed(() => this.tracesByInstrument()[this.activeId()] ?? []);

  readonly activeInstrument = computed(() =>
    this.instruments().find(execution => execution.securityId === this.activeId())
  );

  readonly activeExecutions = computed(() =>
    this.executions().filter(
      execution => execution.securityId === this.activeId()
    )
  );

  readonly sessionBoughtQuantity = computed(() =>
    this.executions()
      .filter(execution => execution.side === 'Buy')
      .reduce((total, execution) => total + execution.quantity, 0)
  );

  readonly sessionSoldQuantity = computed(() =>
    this.executions()
      .filter(execution => execution.side === 'Sell')
      .reduce((total, execution) => total + execution.quantity, 0)
  );

  readonly sessionNetQuantity = computed(() =>
    this.sessionBoughtQuantity() - this.sessionSoldQuantity()
  );

  readonly buyVWAP = computed(() =>
    this.calculateVWAP(this.executions().filter(
      execution => execution.side === 'Buy'
    )));

  readonly sellVWAP = computed(() =>
    this.calculateVWAP(this.executions().filter(
      execution => execution.side === 'Sell'
    )));

  readonly makerPercentage = computed(() => {
    const executions = this.executions();

    if (executions.length === 0)
      return 0;

    const makerExecutions = executions.filter(
      execution => execution.liquidityRole === 'Maker'
    ).length;

    return makerExecutions / executions.length * 100;
  });

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

      return {price, x, y};
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
    this.connectToMarketData();
    this.startTraceSampling();
    this.initializeTradingSession();
  }

  ngOnDestroy(): void {
    this.marketData.disconnect();

    if (this.traceTimer !== undefined)
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
    this.traceHover.set(null);

    const cachedBook = this.booksByInstrument()[id];

    this.bids.set(cachedBook?.bids ?? []);
    this.asks.set(cachedBook?.asks ?? []);
  }

  instrumentMidDollars(securityId: number): number | null {
    const midInCents = this.latestMidsByInstrument()[securityId];

    if(midInCents === undefined)
      return null;

    return midInCents / 100;
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

    const sessionId = this.sessionId();
    if (!sessionId) {
      this.submitError.set("Trading session is still initializing");
      this.isSubmitting.set(false);
      return;
    }

    this.api.placeOrder({
      sessionId,
      securityId: this.activeId(),
      username: this.trader().trim(),
      side: this.side() === 'buy' ? 'Buy' : 'Sell',
      price: this.priceCents(),
      quantity: this.quantity(),
    }).subscribe({
      next: ack => {
        const instrument = this.activeInstrument();
        const submittedSide = this.side();
        const submittedQuantity = this.quantity();

        const immediatelyFilled = ack.fills
          .filter(fill =>
            submittedSide === 'buy'
              ? fill.bidOrderId === ack.orderId
              : fill.askOrderId === ack.orderId
          ).reduce((total, fill) => total + fill.quantity, 0);

        const remainingQuantity = Math.max(
          0,
          submittedQuantity - immediatelyFilled
        );

        // add the order when any quantity remains
        // whether it's a full or partial fill
        if (instrument && remainingQuantity > 0) {
          this.workingOrders.update(orders => [
            {
              orderId: ack.orderId,
              securityId: instrument.securityId,
              symbol: instrument.symbol,
              username: this.trader().trim(),
              side: submittedSide,
              price: this.priceCents(),
              quantity: submittedQuantity,
              filledQuantity: immediatelyFilled
            }, ...orders
          ]);
        }
        this.isSubmitting.set(false);
        this.loadExecutions();
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

  private connectToMarketData(): void {
    this.connectionStatus.set('CONNECTING');

    const securityIds = this.instruments().map(
      instrument => instrument.securityId,
    );

    this.marketData.connect(
      securityIds,
      message => this.handleMarketMessage(message),
      status => this.connectionStatus.set(status)
    );
  }

  private applyTradeToWorkingOrders(trade: TradeMessage): void {
    const concernsWorkingOrder = this.workingOrders().some(
      order => order.securityId === trade.securityId
        && (
          (order.side === 'buy' && order.orderId === trade.bidOrderId) ||
          (order.side === 'sell' && order.orderId === trade.askOrderId)
        ));

    this.workingOrders.update(orders =>
      orders.flatMap(order => {
        // ids are global, but checking securityId prevents
        // accidental updates after a server restart or ID reuse
        if (order.securityId !== trade.securityId)
          return [order];

        const isFilledOrder =
          (order.side == 'buy' && order.orderId === trade.bidOrderId) ||
          (order.side == 'sell' && order.orderId === trade.askOrderId)

        if (!isFilledOrder)
          return [order];

        const filledQuantity = Math.min(
          order.quantity,
          order.filledQuantity + trade.quantity
        );

        // a filled order is no longer resting
        if (filledQuantity >= order.quantity)
          return [];

        return [{
          ...order,
          filledQuantity
        }];
      }))
    if (concernsWorkingOrder)
      this.loadExecutions();
  }

  private handleMarketMessage(message: MarketMessage): void {
    if (message.type === 'book') {
      this.applyBook(message);
      return;
    }

    // a trade message has arrived, update and working order involved in this execution
    this.applyTradeToWorkingOrders(message);

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
    this.booksByInstrument.update(books => ({
      ...books,
      [book.securityId]: book
    }));

    if (book.bid !== null && book.ask !== null) {
      const mid = (book.bid + book.ask) / 2;

      this.latestMidsByInstrument.update(mids => ({
        ...mids,
        [book.securityId]: mid
      }));

      const trace = this.tracesByInstrument()[book.securityId];

      if (!trace || trace.length === 0) {
        this.tracesByInstrument.update(traces => ({
          ...traces,
          [book.securityId]: Array.from({length: 90}, () => mid),
        }));
      }
    }

    if (book.securityId === this.activeId()) {
      this.bids.set(book.bids);
      this.asks.set(book.asks);
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
    if (typeof localStorage === 'undefined')
      return false;

    const savedTheme = localStorage.getItem('theme');

    if (savedTheme === 'dark')
      return true;

    if (savedTheme === 'light')
      return false;

    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function')
      return false;

    //first visit: follow the device/broswer prefernce
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
  }

  private appendTraceSample(
    securityId: number,
    price: number,
  ) {
    this.tracesByInstrument.update(traces => ({
      ...traces,
      [securityId]: [
        ...(traces[securityId] ?? []).slice(-89),
        price
      ]
    }));
  }

  private startTraceSampling(): void {
    this.traceTimer = window.setInterval(() => {
      const mids = this.latestMidsByInstrument();

      for (const [securityId, mid] of Object.entries(mids))
        this.appendTraceSample(Number(securityId), mid);
    }, 1000);
  }

  loadExecutions(): void {
    const sessionId = this.sessionId();

    if (!sessionId)
      return;

    this.executionsLoading.set(true);
    this.executionError.set('');

    this.api.getExecutions(sessionId).subscribe({
      next: executions => {
        // sort newest first, even if the server ordering changes later
        this.executions.set(
          [...executions].sort(
            (a, b) => new Date(b.executedAt).getTime() - new Date(a.executedAt).getTime()
          )
        );

        this.executionsLoading.set(false);
      },
      error: () => {
        this.executionError.set(
          'Execution history could not be loaded.'
        );

        this.executionsLoading.set(false);
      }
    });
  }

  private calculateVWAP(
    executions: Execution[]
  ): number | null {
    const quantity = executions.reduce(
      (total, execution) => total + execution.quantity, 0
    );

    if (quantity === 0)
      return null;

    const priceQuantity = executions.reduce(
      (total, execution) => total + execution.price * execution.quantity, 0
    );

    return priceQuantity / quantity;
  }

  private initializeTradingSession(): void {
    // sessionStorage preserves the session through a browser refresh
    // but opening a separate tab creates a logically  separate session

    const savedSessionId = sessionStorage.getItem('sessionId');

    if (savedSessionId) {
      this.sessionId.set(savedSessionId);
      this.loadExecutions();
      return;
    }

    this.api.createSession().subscribe({
      next: session => {
        sessionStorage.setItem('sessionId', session.sessionId);

        this.sessionId.set(session.sessionId);
        this.loadExecutions();
      },

      error: () => {
        this.executionError.set('Trading session could not be created.');
      }
    });
  }
}
