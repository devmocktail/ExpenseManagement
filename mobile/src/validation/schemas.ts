import { z } from 'zod';
import {
  BUDGET_PERIODS,
  PAYMENT_METHODS,
  RECURRENCE_FREQUENCIES,
} from '@/types/api';

/**
 * Client-side validation.
 *
 * These schemas mirror the server's FluentValidation rules so the user gets an
 * instant, field-level message instead of a round trip. They are a UX
 * affordance, never a security control: the server re-validates everything,
 * because anything enforced only here can be bypassed by talking to the API
 * directly.
 */

// --- Shared primitives -----------------------------------------------------

const email = z
  .string()
  .trim()
  .min(1, 'Email is required')
  .email('Enter a valid email address')
  .max(256, 'Email is too long');

/**
 * Password policy, matched to ASP.NET Core Identity's configuration.
 *
 * Each rule is a separate refinement so the form can show exactly which
 * requirement is unmet rather than one opaque "password is not strong enough".
 */
export const passwordSchema = z
  .string()
  .min(8, 'Use at least 8 characters')
  .max(128, 'Password is too long')
  .regex(/[A-Z]/, 'Add an uppercase letter')
  .regex(/[a-z]/, 'Add a lowercase letter')
  .regex(/[0-9]/, 'Add a number')
  .regex(/[^A-Za-z0-9]/, 'Add a special character');

/** Individual policy checks, for the live strength meter on the register screen. */
export const passwordRules = [
  { id: 'length', label: 'At least 8 characters', test: (v: string) => v.length >= 8 },
  { id: 'upper', label: 'One uppercase letter', test: (v: string) => /[A-Z]/.test(v) },
  { id: 'lower', label: 'One lowercase letter', test: (v: string) => /[a-z]/.test(v) },
  { id: 'digit', label: 'One number', test: (v: string) => /[0-9]/.test(v) },
  { id: 'symbol', label: 'One special character', test: (v: string) => /[^A-Za-z0-9]/.test(v) },
] as const;

/**
 * Amount as a string, because the input is a keypad and an empty field must be
 * distinguishable from zero. Coerced to a number only after it validates.
 */
const amountString = z
  .string()
  .trim()
  .min(1, 'Enter an amount')
  .refine((v) => !Number.isNaN(Number(v.replace(/,/g, ''))), 'Enter a valid amount')
  .refine((v) => Number(v.replace(/,/g, '')) > 0, 'Amount must be greater than zero')
  .refine(
    (v) => Number(v.replace(/,/g, '')) <= 999_999_999,
    'That amount is larger than this app supports',
  )
  .refine(
    (v) => !/\.\d{3,}$/.test(v.trim()),
    'Amounts can have at most 2 decimal places',
  );

const uuid = z.string().uuid('Select a valid option');

const hexColor = z
  .string()
  .regex(/^#[0-9A-Fa-f]{6}$/, 'Pick a colour');

// --- Auth ------------------------------------------------------------------

export const loginSchema = z.object({
  email,
  password: z.string().min(1, 'Password is required'),
});
export type LoginFormValues = z.infer<typeof loginSchema>;

export const registerSchema = z
  .object({
    fullName: z
      .string()
      .trim()
      .min(2, 'Enter your name')
      .max(200, 'That name is too long'),
    email,
    password: passwordSchema,
    confirmPassword: z.string().min(1, 'Confirm your password'),
    acceptedTerms: z.literal(true, {
      errorMap: () => ({ message: 'Please accept the terms to continue' }),
    }),
  })
  // Attached to confirmPassword so the message renders under the field the user
  // needs to fix, not at the top of the form.
  .refine((data) => data.password === data.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  });
export type RegisterFormValues = z.infer<typeof registerSchema>;

export const forgotPasswordSchema = z.object({ email });
export type ForgotPasswordFormValues = z.infer<typeof forgotPasswordSchema>;

export const resetPasswordSchema = z
  .object({
    email,
    token: z.string().min(1, 'Enter the code from your email'),
    newPassword: passwordSchema,
    confirmPassword: z.string().min(1, 'Confirm your password'),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  });
export type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>;

export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Enter your current password'),
    newPassword: passwordSchema,
    confirmPassword: z.string().min(1, 'Confirm your new password'),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  })
  .refine((data) => data.newPassword !== data.currentPassword, {
    message: 'Choose a password you have not used here before',
    path: ['newPassword'],
  });
export type ChangePasswordFormValues = z.infer<typeof changePasswordSchema>;

// --- Transactions ----------------------------------------------------------

export const transactionSchema = z.object({
  type: z.enum(['Expense', 'Income']),
  amount: amountString,
  categoryId: uuid,
  transactionDate: z.date({ required_error: 'Pick a date' }).refine(
    // A future-dated expense is almost always a mis-set date picker rather than
    // an intention, and it would corrupt "spent this month" totals.
    (d) => d.getTime() <= Date.now() + 24 * 60 * 60 * 1000,
    'That date is in the future',
  ),
  paymentMethod: z.enum(PAYMENT_METHODS),
  merchant: z.string().trim().max(200, 'Keep this under 200 characters').optional().or(z.literal('')),
  description: z.string().trim().max(500, 'Keep this under 500 characters').optional().or(z.literal('')),
  notes: z.string().trim().max(2000, 'Keep this under 2000 characters').optional().or(z.literal('')),
});
export type TransactionFormValues = z.infer<typeof transactionSchema>;

// --- Categories ------------------------------------------------------------

export const categorySchema = z.object({
  name: z.string().trim().min(1, 'Enter a name').max(100, 'That name is too long'),
  type: z.enum(['Expense', 'Income']),
  icon: z.string().min(1, 'Pick an icon'),
  color: hexColor,
});
export type CategoryFormValues = z.infer<typeof categorySchema>;

// --- Budgets ---------------------------------------------------------------

export const budgetSchema = z
  .object({
    name: z.string().trim().min(1, 'Enter a name').max(100, 'That name is too long'),
    // Empty string is the "all categories" option in the picker.
    categoryId: z.union([uuid, z.literal('')]).optional(),
    amount: amountString,
    period: z.enum(BUDGET_PERIODS),
    startDate: z.date({ required_error: 'Pick a start date' }),
    endDate: z.date().optional(),
  })
  .refine(
    (data) => data.period !== 'Custom' || data.endDate !== undefined,
    { message: 'A custom budget needs an end date', path: ['endDate'] },
  )
  .refine(
    (data) => !data.endDate || data.endDate > data.startDate,
    { message: 'The end date must be after the start date', path: ['endDate'] },
  );
export type BudgetFormValues = z.infer<typeof budgetSchema>;

// --- Recurring -------------------------------------------------------------

export const recurringSchema = z
  .object({
    name: z.string().trim().min(1, 'Enter a name').max(100, 'That name is too long'),
    type: z.enum(['Expense', 'Income']),
    amount: amountString,
    categoryId: uuid,
    paymentMethod: z.enum(PAYMENT_METHODS),
    frequency: z.enum(RECURRENCE_FREQUENCIES),
    interval: z
      .number({ invalid_type_error: 'Enter a number' })
      .int('Use a whole number')
      .min(1, 'Must be at least 1')
      .max(365, 'That interval is too large'),
    startDate: z.date({ required_error: 'Pick a start date' }),
    endDate: z.date().optional().nullable(),
    merchant: z.string().trim().max(200).optional().or(z.literal('')),
    description: z.string().trim().max(500).optional().or(z.literal('')),
    reminderDaysBefore: z.number().int().min(0).max(30),
  })
  .refine(
    (data) => !data.endDate || data.endDate > data.startDate,
    { message: 'The end date must be after the start date', path: ['endDate'] },
  );
export type RecurringFormValues = z.infer<typeof recurringSchema>;

// --- Profile ---------------------------------------------------------------

export const profileSchema = z.object({
  fullName: z.string().trim().min(2, 'Enter your name').max(200, 'That name is too long'),
});
export type ProfileFormValues = z.infer<typeof profileSchema>;
