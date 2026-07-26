import {TestBed} from '@angular/core/testing';
import {of} from 'rxjs';
import {vi} from 'vitest';
import {App} from './app';
import {MarketDataService} from './MarketData.service';
import {TradingApiService} from './TradingApi.service';

describe('App', () => {
  const marketData = {
    connect: vi.fn(),
    disconnect: vi.fn(),
  };

  const tradingApi = {
    createSession: vi.fn(() => of({
      sessionId: 'test-session-id',
      createdAt: '2026-07-25T00:00:00Z',
    })),

    getExecutions: vi.fn(() => of([])),
    placeOrder: vi.fn(),
    cancelOrder: vi.fn(),
  };

  beforeEach(async () => {
    sessionStorage.clear();

    Object.defineProperty(window, 'matchMedia', {
      configurable: true,
      writable: true,
      value: vi.fn().mockImplementation(
        (query: string) => ({
          matches: false,
          media: query,
          onchange: null,
          addListener: vi.fn(),
          removeListener: vi.fn(),
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
          dispatchEvent: vi.fn(),
        })
      ),
    });

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        {
          provide: MarketDataService,
          useValue: marketData,
        },
        {
          provide: TradingApiService,
          useValue: tradingApi,
        },
      ],
    }).compileComponents();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('creates the application', () => {
    const fixture = TestBed.createComponent(App);

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the Valkyrie brand', () => {
    const fixture = TestBed.createComponent(App);

    fixture.detectChanges();

    const element =
      fixture.nativeElement as HTMLElement;

    expect(
      element.querySelector('.word b')?.textContent
    ).toContain('VALKYRIE');
  });

  it('creates a trading session on first launch', () => {
    const fixture = TestBed.createComponent(App);

    fixture.detectChanges();

    expect(
      tradingApi.createSession
    ).toHaveBeenCalledOnce();

    expect(
      fixture.componentInstance.sessionId()
    ).toBe('test-session-id');
  });

  it('loads session executions after initialization', () => {
    const fixture = TestBed.createComponent(App);

    fixture.detectChanges();

    expect(
      tradingApi.getExecutions
    ).toHaveBeenCalledWith('test-session-id');
  });

  it('reuses a session after a browser refresh', () => {
    sessionStorage.setItem(
      'sessionId',
      'existing-session-id'
    );

    const fixture = TestBed.createComponent(App);

    fixture.detectChanges();

    expect(
      tradingApi.createSession
    ).not.toHaveBeenCalled();

    expect(
      tradingApi.getExecutions
    ).toHaveBeenCalledWith(
      'existing-session-id'
    );
  });
});
