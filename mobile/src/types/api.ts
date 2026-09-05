/**
 * The wire contract between this app and ExpenseManagement.Api.
 *
 * These types are the canonical shape of every request and response. The
 * backend DTOs mirror them exactly; when the two disagree, this file and
 * `docs/api.md` are the specification and the server is wrong.
 *
 * Money is `number` here and `decimal` on the server. JSON has no decimal type,
 * so amounts cross the wire as JSON numbers — safe because every amount in this
 * product is far inside IEEE-754's exact-integer range once scaled by 100, and
 * the client never does arithmetic that the server has not already done.
 */

// ---------------------------------------------------------------------------
// Envelope
// ---------------------------------------------------------------------------

/** Every successful response body. */
export type ApiSuccess<T> = {
  success: true;
  data: T;
  message: string | null;
};

/** One field-level validation failure. */
export type ApiFieldError = {
  /** camelCase field path, e.g. `amount` or `items[0].categoryId`. */
  field: string;
  message: string;
};

/** Every failure response body, for all 4xx and 5xx. */
export type ApiFailure = {
  success: false;
  message: string;
  /** Stable machine code, e.g. `not_found`, `validation_failed`. */
  errorCode: string;
  errors: ApiFieldError[];
  /** Correlates a client report with a server log line. Safe to show to users. */
  traceId?: string;
};

export type ApiEnvelope<T> = ApiSuccess<T> | ApiFailure;

export type Paged<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
};

// ---------------------------------------------------------------------------
// Enums — string-valued on the wire so a payload is readable in a log and a
// reordered C# enum can never silently remap meaning.
// ---------------------------------------------------------------------------

export type TransactionType = 'Expense' | 'Income';

export const PAYMENT_METHODS = [
  'Cash',
  'CreditCard',
  'DebitCard',
  'Upi',
  'BankTransfer',
  'Wallet',
  'Other',
] as const;
export type PaymentMethod = (typeof PAYMENT_METHODS)[number];

export const BUDGET_PERIODS = ['Weekly', 'Monthly', 'Quarterly', 'Yearly', 'Custom'] as const;
export type BudgetPeriod = (typeof BUDGET_PERIODS)[number];

export const RECURRENCE_FREQUENCIES = ['Daily', 'Weekly', 'Monthly', 'Yearly'] as const;
export type RecurrenceFrequency = (typeof RECURRENCE_FREQUENCIES)[number];

export type ThemePreference = 'System' | 'Light' | 'Dark';

export type NotificationType =
  | 'BudgetWarning'
  | 'BudgetExceeded'
  | 'RecurringDue'
  | 'MonthlySummary'
  | 'System';

export type DevicePlatform = 'Android' | 'Ios' | 'Web';

/** ISO 8601 with offset, always UTC from the server (e.g. `2026-09-01T18:30:00Z`). */
export type IsoDateTime = string;

// ---------------------------------------------------------------------------
// Auth
// ---------------------------------------------------------------------------

export type RegisterRequest = {
  fullName: string;
  email: string;
  password: string;
  confirmPassword: string;
  acceptedTerms: boolean;
  /** Optional client hints so the first period and currency are right immediately. */
  currencyCode?: string;
  timeZoneId?: string;
};

export type LoginRequest = {
  email: string;
  password: string;
};

export type RefreshRequest = {
  refreshToken: string;
};

export type AuthTokens = {
  accessToken: string;
  /** Returned exactly once per rotation; the previous value stops working. */
  refreshToken: string;
  accessTokenExpiresAt: IsoDateTime;
  refreshTokenExpiresAt: IsoDateTime;
};

export type AuthResponse = AuthTokens & {
  user: UserProfile;
};

export type ForgotPasswordRequest = { email: string };

export type ResetPasswordRequest = {
  email: string;
  token: string;
  newPassword: string;
  confirmPassword: string;
};

export type ChangePasswordRequest = {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
};

// ---------------------------------------------------------------------------
// Profile & settings
// ---------------------------------------------------------------------------

export type UserProfile = {
  id: string;
  fullName: string;
  email: string;
  emailConfirmed: boolean;
  avatarUrl: string | null;
  createdAt: IsoDateTime;
  lastLoginAt: IsoDateTime | null;
  roles: string[];
};

export type UpdateProfileRequest = {
  fullName: string;
};

export type UserSettings = {
  currencyCode: string;
  locale: string;
  timeZoneId: string;
  theme: ThemePreference;
  budgetAlertsEnabled: boolean;
  recurringRemindersEnabled: boolean;
  monthlySummaryEnabled: boolean;
  budgetWarningThreshold: number;
  budgetCriticalThreshold: number;
  monthStartDay: number;
  biometricEnabled: boolean;
};

export type UpdateSettingsRequest = Partial<UserSettings>;

// ---------------------------------------------------------------------------
// Categories
// ---------------------------------------------------------------------------

export type Category = {
  id: string;
  name: string;
  type: TransactionType;
  icon: string;
  color: string;
  /** System categories can be renamed and restyled but never deleted. */
  isSystem: boolean;
  sortOrder: number;
  /** Live transactions referencing this category; drives the delete warning. */
  transactionCount: number;
};

export type CreateCategoryRequest = {
  name: string;
  type: TransactionType;
  icon: string;
  color: string;
  sortOrder?: number;
};

export type UpdateCategoryRequest = {
  name: string;
  icon: string;
  color: string;
  sortOrder?: number;
};

export type DeleteCategoryRequest = {
  /**
   * Where to move existing transactions. Required when the category is in use;
   * the server refuses the delete otherwise rather than orphaning history.
   */
  reassignToCategoryId?: string;
};

// ---------------------------------------------------------------------------
// Transactions
// ---------------------------------------------------------------------------

export type ReceiptSummary = {
  id: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  /** Authorized endpoint path, not a public URL. Requires the bearer token. */
  url: string;
  createdAt: IsoDateTime;
};

export type Transaction = {
  id: string;
  type: TransactionType;
  amount: number;
  currencyCode: string;
  categoryId: string;
  categoryName: string;
  categoryIcon: string;
  categoryColor: string;
  description: string | null;
  merchant: string | null;
  paymentMethod: PaymentMethod;
  transactionDate: IsoDateTime;
  notes: string | null;
  recurringTransactionId: string | null;
  receipts: ReceiptSummary[];
  createdAt: IsoDateTime;
  updatedAt: IsoDateTime | null;
};

export type CreateTransactionRequest = {
  type: TransactionType;
  amount: number;
  categoryId: string;
  transactionDate: IsoDateTime;
  paymentMethod: PaymentMethod;
  description?: string | null;
  merchant?: string | null;
  notes?: string | null;
  currencyCode?: string;
  /** Idempotency key for offline replay; a repeat of the same key updates in place. */
  clientReference?: string | null;
};

export type UpdateTransactionRequest = Omit<CreateTransactionRequest, 'clientReference'>;

export type TransactionQuery = {
  page?: number;
  pageSize?: number;
  search?: string;
  categoryId?: string;
  type?: TransactionType;
  /** Inclusive UTC lower bound. */
  from?: IsoDateTime;
  /** Exclusive UTC upper bound. */
  to?: IsoDateTime;
  minAmount?: number;
  maxAmount?: number;
  paymentMethod?: PaymentMethod;
  sortBy?: 'date' | 'amount';
  sortDirection?: 'asc' | 'desc';
};

// ---------------------------------------------------------------------------
// Budgets
// ---------------------------------------------------------------------------

export type Budget = {
  id: string;
  name: string;
  /** Null for the overall budget covering every category. */
  categoryId: string | null;
  categoryName: string | null;
  categoryIcon: string | null;
  categoryColor: string | null;
  amount: number;
  currencyCode: string;
  period: BudgetPeriod;
  startDate: IsoDateTime;
  /** Exclusive. */
  endDate: IsoDateTime;
  isRecurring: boolean;
  isActive: boolean;

  /** Server-computed; the client never sums transactions itself. */
  spent: number;
  remaining: number;
  percentageUsed: number;
  /** `ok` | `warning` | `critical` | `exceeded`, derived from the user's thresholds. */
  status: BudgetStatus;
  daysRemaining: number;
};

export type BudgetStatus = 'ok' | 'warning' | 'critical' | 'exceeded';

export type CreateBudgetRequest = {
  name: string;
  categoryId?: string | null;
  amount: number;
  period: BudgetPeriod;
  startDate: IsoDateTime;
  endDate?: IsoDateTime;
  isRecurring?: boolean;
  warningThreshold?: number | null;
  criticalThreshold?: number | null;
};

export type UpdateBudgetRequest = CreateBudgetRequest & { isActive?: boolean };

// ---------------------------------------------------------------------------
// Dashboard — one call, so the home screen is a single round trip
// ---------------------------------------------------------------------------

export type CategoryBreakdownItem = {
  categoryId: string;
  categoryName: string;
  categoryIcon: string;
  categoryColor: string;
  amount: number;
  percentage: number;
  transactionCount: number;
};

export type TrendPoint = {
  /** Bucket start, UTC. */
  date: IsoDateTime;
  /** Pre-formatted short label from the server, already in the user's locale. */
  label: string;
  income: number;
  expense: number;
};

export type DashboardResponse = {
  currencyCode: string;
  periodStart: IsoDateTime;
  periodEnd: IsoDateTime;
  periodLabel: string;

  balance: number;
  income: number;
  expenses: number;

  /** Null when the user has no overall budget for the period. */
  budget: BudgetSummary | null;

  recentTransactions: Transaction[];
  categoryBreakdown: CategoryBreakdownItem[];
  weeklyTrend: TrendPoint[];
};

export type BudgetSummary = {
  budgetId: string;
  amount: number;
  spent: number;
  remaining: number;
  percentage: number;
  status: BudgetStatus;
};

// ---------------------------------------------------------------------------
// Analytics
// ---------------------------------------------------------------------------

export type AnalyticsPeriod = 'week' | 'month' | 'year';

export type AnalyticsQuery = {
  period: AnalyticsPeriod;
  /** Anchor instant; the server resolves the containing period in the user's zone. */
  at?: IsoDateTime;
};

export type AnalyticsSummary = {
  currencyCode: string;
  periodStart: IsoDateTime;
  periodEnd: IsoDateTime;
  periodLabel: string;
  totalIncome: number;
  totalExpenses: number;
  savings: number;
  savingsRate: number;
  transactionCount: number;
  averageDailySpend: number;
  /** Same figures for the immediately preceding period, for comparison. */
  previousTotalIncome: number;
  previousTotalExpenses: number;
};

export type SpendingInsight = {
  /** Stable id so an insight can be dismissed without re-appearing. */
  id: string;
  severity: 'positive' | 'neutral' | 'warning';
  title: string;
  detail: string;
  /** Percent change vs the previous period, when the insight is a comparison. */
  changePercentage: number | null;
};

export type AnalyticsResponse = {
  summary: AnalyticsSummary;
  trend: TrendPoint[];
  categoryBreakdown: CategoryBreakdownItem[];
  topCategories: CategoryBreakdownItem[];
  insights: SpendingInsight[];
};

// ---------------------------------------------------------------------------
// Recurring
// ---------------------------------------------------------------------------

export type RecurringTransaction = {
  id: string;
  name: string;
  type: TransactionType;
  amount: number;
  currencyCode: string;
  categoryId: string;
  categoryName: string;
  categoryIcon: string;
  categoryColor: string;
  paymentMethod: PaymentMethod;
  merchant: string | null;
  description: string | null;
  frequency: RecurrenceFrequency;
  interval: number;
  startDate: IsoDateTime;
  endDate: IsoDateTime | null;
  nextRunDate: IsoDateTime;
  lastRunDate: IsoDateTime | null;
  occurrencesGenerated: number;
  isPaused: boolean;
  reminderDaysBefore: number;
};

export type CreateRecurringRequest = {
  name: string;
  type: TransactionType;
  amount: number;
  categoryId: string;
  paymentMethod: PaymentMethod;
  frequency: RecurrenceFrequency;
  interval?: number;
  startDate: IsoDateTime;
  endDate?: IsoDateTime | null;
  merchant?: string | null;
  description?: string | null;
  reminderDaysBefore?: number;
};

export type UpdateRecurringRequest = CreateRecurringRequest;

// ---------------------------------------------------------------------------
// Notifications & devices
// ---------------------------------------------------------------------------

export type AppNotification = {
  id: string;
  type: NotificationType;
  title: string;
  body: string;
  data: Record<string, unknown> | null;
  isRead: boolean;
  createdAt: IsoDateTime;
};

export type RegisterDeviceRequest = {
  token: string;
  platform: DevicePlatform;
  deviceName?: string;
  appVersion?: string;
};

// ---------------------------------------------------------------------------
// Export
// ---------------------------------------------------------------------------

export type ExportFormat = 'csv' | 'json';

export type ExportRequest = {
  format: ExportFormat;
  from?: IsoDateTime;
  to?: IsoDateTime;
};
