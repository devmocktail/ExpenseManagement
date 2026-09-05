using System.Globalization;
using ExpenseManagement.Application.Common.Extensions;
using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Application.Common.Models;
using ExpenseManagement.Application.Features.Categories;
using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExpenseManagement.Infrastructure.Persistence.Seeding;

/// <summary>
/// Brings a database to a usable state at startup. Program.cs calls this on
/// every boot, so every step is written to find its own previous work and leave
/// it alone rather than repeat it.
///
/// The two halves have very different blast radii. Roles are structural — every
/// environment needs them and creating one grants nobody anything — so they are
/// always ensured. The demo account is the opposite: a published email with a
/// configured password is a standing backdoor anywhere it can be reached, so it
/// is created only when the host is Development AND configuration asks for it
/// AND a password was supplied out of band.
/// </summary>
public sealed class DatabaseSeeder(
    AppDbContext db,
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IHostEnvironment environment,
    IConfiguration configuration,
    IDateTimeProvider clock,
    ILogger<DatabaseSeeder> logger)
{
    private const string DemoUserFlagKey = "Seed:DemoUser";
    private const string DemoPasswordKey = "Seed:DemoPassword";

    /// <summary>
    /// <c>.local</c> is reserved for local networks and can never receive mail,
    /// so this address cannot collide with a real person's and nothing the app
    /// sends to it can leave the machine.
    /// </summary>
    public const string DemoEmail = "demo@expense.local";

    private const string DemoFullName = "Demo User (development only)";

    /// <summary>
    /// Prefix of the idempotency key written to <see cref="Transaction.ClientReference"/>
    /// on every generated row. That column already carries a unique filtered
    /// index per user for offline sync, which makes it exactly the right place
    /// to record "the seeder wrote this one": no extra column, and the database
    /// enforces the no-duplicates rule this class is trying to keep.
    /// </summary>
    private const string SeedReferencePrefix = "seed-demo-";

    private const int DemoHistoryDays = 30;
    private const int MaxDiscretionaryPerDay = 3;
    private const int SalaryDayOfMonth = 1;
    private const int RentDayOfMonth = 5;

    /// <summary>
    /// Fixed so two developers, and the same developer twice, get identical
    /// demo data. <c>new Random(seed)</c> deliberately keeps the legacy .NET
    /// algorithm so a seeded stream stays stable across runtime versions; the
    /// parameterless constructor does not, and must not be used here.
    /// </summary>
    private const int TransactionRandomSeed = 20_260_901;

    private const decimal DemoSalaryAmount = 85_000m;
    private const decimal DemoRentAmount = 28_000m;
    private const decimal DemoOverallBudgetAmount = 60_000m;
    private const decimal DemoFoodBudgetAmount = 9_000m;

    /// <summary>
    /// Startup runs outside any HTTP request, so <see cref="AppDbContext.CurrentUserId"/>
    /// is null and the UserOwnership filter compares <c>UserId</c> against NULL,
    /// which matches nothing. Every read below therefore suspends it by name —
    /// without that the seeder would see an empty database on the second run and
    /// cheerfully insert a second copy of everything.
    ///
    /// SoftDelete is suspended alongside it so a demo row someone deleted while
    /// testing stays deleted. This is a seeder, not a restore tool, and every
    /// unique index here is filtered on <c>IsDeleted = 0</c>, so a check that
    /// honoured the filter would happily recreate a row that was removed on
    /// purpose and the database would not object.
    /// </summary>
    private static readonly string[] IgnoreOwnershipAndSoftDelete =
        [FilterNames.UserOwnership, FilterNames.SoftDelete];

    /// <summary>UserSettings is not soft-deletable, so naming that filter on it would be a lie.</summary>
    private static readonly string[] IgnoreOwnership = [FilterNames.UserOwnership];

    /// <summary>Names come from <see cref="RoleNames"/> so authorization attributes and this list cannot drift.</summary>
    private static readonly (string Name, string Description)[] Roles =
    [
        (RoleNames.User, "Standard account. Sees only its own data."),
        (RoleNames.Admin, "Operations access: user administration and system-wide reporting."),
    ];

    /// <summary>
    /// A merchant the generator can draw from. Bounds are in minor units
    /// (paise) and divided by 100 to produce the amount, so a randomly drawn
    /// value is exact at two decimal places by construction and no binary
    /// floating point ever touches money. Common categories appear more than
    /// once, which is how the mix is weighted without a weight column.
    /// </summary>
    private sealed record SpendTemplate(
        string Category,
        string Merchant,
        int MinMinorUnits,
        int MaxMinorUnits,
        PaymentMethod Method);

    private static readonly SpendTemplate[] DiscretionarySpend =
    [
        new("Food", "Chai Point", 8_000, 24_000, PaymentMethod.Upi),
        new("Food", "Sagar Ratna", 25_000, 78_000, PaymentMethod.Upi),
        new("Food", "Swiggy", 32_000, 96_000, PaymentMethod.CreditCard),
        new("Groceries", "BigBasket", 62_000, 245_000, PaymentMethod.CreditCard),
        new("Groceries", "Reliance Fresh", 41_000, 178_000, PaymentMethod.DebitCard),
        new("Transport", "Uber", 14_000, 62_000, PaymentMethod.Upi),
        new("Transport", "Namma Metro", 3_000, 9_000, PaymentMethod.Wallet),
        new("Transport", "Indian Oil", 200_000, 350_000, PaymentMethod.CreditCard),
        new("Shopping", "Myntra", 84_000, 410_000, PaymentMethod.CreditCard),
        new("Entertainment", "PVR Cinemas", 40_000, 128_000, PaymentMethod.Upi),
        new("Utilities", "BESCOM", 92_000, 264_000, PaymentMethod.BankTransfer),
        new("Subscriptions", "Netflix", 19_900, 64_900, PaymentMethod.CreditCard),
        new("Healthcare", "Apollo Pharmacy", 28_000, 152_000, PaymentMethod.Cash),
        new("Personal Care", "Urban Company", 49_000, 210_000, PaymentMethod.Upi),
        new("Bills", "Airtel", 39_900, 99_900, PaymentMethod.Upi),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await SeedRolesAsync(cancellationToken);

        if (!configuration.GetValue<bool>(DemoUserFlagKey)) return;

        if (!environment.IsDevelopment())
        {
            // Loud, and then nothing happens. A demo account with a known
            // password outside development is an unauthenticated door into real
            // people's finances, so the flag is treated as a misconfiguration to
            // shout about rather than as an instruction to obey.
            logger.LogError(
                "{Flag} is enabled in the {Environment} environment. Refusing to create the demo account: " +
                "a seeded credential outside Development is a standing backdoor. Remove the flag from this " +
                "environment's configuration.",
                DemoUserFlagKey,
                environment.EnvironmentName);

            return;
        }

        var password = configuration[DemoPasswordKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            // Deliberately no fallback default: a password baked into source is
            // a credential published to everyone who can read the repository,
            // and it would be the same one on every clone.
            logger.LogWarning(
                "{Flag} is enabled but {PasswordKey} is empty, so the demo account was not created. " +
                "Set it in user-secrets or the environment.",
                DemoUserFlagKey,
                DemoPasswordKey);

            return;
        }

        await SeedDemoAccountAsync(password, cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var (name, description) in Roles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await roleManager.RoleExistsAsync(name)) continue;

            string failure;

            try
            {
                var created = await roleManager.CreateAsync(
                    new ApplicationRole(name) { Description = description });

                if (created.Succeeded)
                {
                    logger.LogInformation("Created the {Role} role.", name);
                    continue;
                }

                failure = Describe(created);
            }
            catch (DbUpdateException ex)
            {
                failure = ex.GetBaseException().Message;
            }

            // A failed insert leaves the role tracked as Added, and every later
            // SaveChanges in this seeder would retry — and re-fail — it. Nothing
            // else is tracked yet, roles being the first thing seeded, so
            // clearing is precise rather than a hammer.
            db.ChangeTracker.Clear();

            // Two instances booting against one database race here, and the
            // loser sees the winner's row either as a validator rejection or as
            // a unique-index violation on the normalised name. Both are benign
            // once the role is confirmed present, which is all this method ever
            // promised; anything else is a broken database and must stop startup.
            if (!await roleManager.RoleExistsAsync(name))
            {
                throw new InvalidOperationException($"The '{name}' role could not be created: {failure}");
            }

            logger.LogDebug("The {Role} role was created concurrently by another instance.", name);
        }
    }

    private async Task SeedDemoAccountAsync(string password, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(DemoEmail);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = DemoEmail,
                Email = DemoEmail,
                FullName = DemoFullName,

                // Nothing can deliver to a .local address, so requiring a
                // confirmation round trip would make the account unusable.
                EmailConfirmed = true,
            };

            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                // Nearly always a configured password that fails the Identity
                // rules. A broken demo account is not a reason to stop the API
                // from starting, so this warns and gives up rather than throws.
                logger.LogWarning("The demo account could not be created: {Errors}", Describe(created));
                return;
            }

            logger.LogInformation("Created the development demo account {Email}.", DemoEmail);
        }
        else if (user.IsDeleted)
        {
            // Closing this account is itself something worth testing, and a
            // restart silently reopening it would undo that test. An existing
            // account's password is never reset here for the same reason: the
            // developer's state wins over the seeder's opinion of it.
            logger.LogWarning(
                "The demo account {Email} is closed, so no demo data was seeded. " +
                "Clear its IsDeleted flag or drop the database to start over.",
                DemoEmail);

            return;
        }

        if (!await userManager.IsInRoleAsync(user, RoleNames.User))
        {
            var assigned = await userManager.AddToRoleAsync(user, RoleNames.User);
            if (!assigned.Succeeded)
            {
                // Without the role the account cannot pass authorization, so
                // there is no point generating a month of data for it.
                logger.LogWarning(
                    "The demo account could not be added to the {Role} role: {Errors}",
                    RoleNames.User,
                    Describe(assigned));

                return;
            }
        }

        var settings = await EnsureDemoSettingsAsync(user.Id, cancellationToken);
        var categories = await EnsureDemoCategoriesAsync(user.Id, cancellationToken);

        // Each step commits on its own rather than sharing one transaction:
        // they are individually idempotent, so a failure part way through is
        // repaired by the next boot instead of rolling back work that was
        // already correct.
        await EnsureDemoTransactionsAsync(user.Id, settings, categories, cancellationToken);
        await EnsureDemoBudgetsAsync(user.Id, settings, categories, cancellationToken);
    }

    private async Task<UserSettings> EnsureDemoSettingsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await db.UserSettings
            .AsNoTracking()
            .IgnoreQueryFilters(IgnoreOwnership)
            .Where(s => s.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null) return existing;

        // The entity initialisers are the single source of the defaults (INR,
        // en-IN, Asia/Kolkata); restating them here would let the two drift.
        var settings = new UserSettings { UserId = userId };

        db.UserSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);

        return settings;
    }

    /// <summary>
    /// Ensures the demo account owns the standard starter set, and returns the
    /// ids the generated rows point at, keyed by name and ledger side.
    /// </summary>
    private async Task<Dictionary<(string Name, TransactionType Type), Guid>> EnsureDemoCategoriesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        // One read answers both questions: which (name, type) pairs are already
        // taken — deleted ones included, so a bucket someone removed does not
        // grow back — and which live rows a transaction may reference.
        var existing = await db.Categories
            .AsNoTracking()
            .IgnoreQueryFilters(IgnoreOwnershipAndSoftDelete)
            .Where(c => c.UserId == userId)
            .Select(c => new { c.Id, c.Name, c.Type, c.IsDeleted })
            .ToListAsync(cancellationToken);

        var taken = existing.Select(c => (c.Name, c.Type)).ToHashSet();

        var missing = DefaultCategories.All
            .Where(template => !taken.Contains((template.Name, template.Type)))
            .Select(template => new Category
            {
                UserId = userId,
                Name = template.Name,
                Type = template.Type,
                Icon = template.Icon,
                Color = template.Color,
                SortOrder = template.SortOrder,
                IsSystem = true,
            })
            .ToList();

        if (missing.Count > 0)
        {
            db.Categories.AddRange(missing);

            // Ids come from the column's NEWSEQUENTIALID() default, so they only
            // exist once the insert has round-tripped — the transactions below
            // cannot be built before this save.
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Seeded {Count} categories for the demo account.", missing.Count);
        }

        return existing
            .Where(c => !c.IsDeleted)
            .Select(c => (c.Name, c.Type, c.Id))
            .Concat(missing.Select(c => (c.Name, c.Type, c.Id)))
            .ToDictionary(c => (c.Name, c.Type), c => c.Id);
    }

    private async Task EnsureDemoTransactionsAsync(
        Guid userId,
        UserSettings settings,
        IReadOnlyDictionary<(string Name, TransactionType Type), Guid> categories,
        CancellationToken cancellationToken)
    {
        var alreadySeeded = await db.Transactions
            .AsNoTracking()
            .IgnoreQueryFilters(IgnoreOwnershipAndSoftDelete)
            .Where(t => t.UserId == userId
                        && t.ClientReference != null
                        && t.ClientReference.StartsWith(SeedReferencePrefix))
            .Select(t => t.ClientReference!)
            .ToListAsync(cancellationToken);

        var rows = BuildDemoTransactions(
            userId,
            settings.CurrencyCode,
            categories,
            alreadySeeded.ToHashSet(StringComparer.Ordinal),
            clock.UtcNow,
            PeriodCalculator.ResolveTimeZone(settings.TimeZoneId));

        if (rows.Count == 0) return;

        db.Transactions.AddRange(rows);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} demo transactions.", rows.Count);
    }

    private static List<Transaction> BuildDemoTransactions(
        Guid userId,
        string currencyCode,
        IReadOnlyDictionary<(string Name, TransactionType Type), Guid> categories,
        HashSet<string> seededReferences,
        DateTimeOffset now,
        TimeZoneInfo timeZone)
    {
        var rows = new List<Transaction>();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).Date);

        void TryAdd(
            DateOnly date,
            string slot,
            string categoryName,
            TransactionType type,
            decimal amount,
            DateTimeOffset occurredAt,
            string? merchant,
            string? description,
            PaymentMethod method)
        {
            // A future-dated row would sit inside the current budget window and
            // inflate every "spent so far" figure on the dashboard. Skipping it
            // loses nothing: the day is regenerated identically on a later boot,
            // by which time the instant has passed.
            if (occurredAt > now) return;

            var reference = BuildReference(date, slot);

            // Add returns false for a reference the database already holds, so
            // this single call covers both "seeded on an earlier run" and
            // "already produced by this run".
            if (!seededReferences.Add(reference)) return;

            // A category the developer deleted is not resurrected, so the rows
            // that would have pointed at it are simply not written.
            if (!categories.TryGetValue((categoryName, type), out var categoryId)) return;

            rows.Add(new Transaction
            {
                UserId = userId,
                CategoryId = categoryId,
                Type = type,
                Amount = amount,
                CurrencyCode = currencyCode,
                Merchant = merchant,
                Description = description,
                PaymentMethod = method,
                TransactionDate = occurredAt,
                ClientReference = reference,
            });
        }

        for (var offset = DemoHistoryDays - 1; offset >= 0; offset--)
        {
            var date = today.AddDays(-offset);

            // The seed is fixed but folded with the day number, so a calendar
            // date always produces the same rows whichever day the seeder runs.
            // One stream shared across the whole window would shift every draw
            // as the window slid forward, and yesterday's generated data would
            // disagree overnight with the rows already stored for it.
            var random = new Random(TransactionRandomSeed + date.DayNumber);

            if (date.Day == SalaryDayOfMonth)
            {
                TryAdd(
                    date, "salary", "Salary", TransactionType.Income, DemoSalaryAmount,
                    AtLocalTime(date, 10, 0, timeZone),
                    "Acme Technologies", "Monthly salary", PaymentMethod.BankTransfer);
            }

            if (date.Day == RentDayOfMonth)
            {
                TryAdd(
                    date, "rent", "Rent", TransactionType.Expense, DemoRentAmount,
                    AtLocalTime(date, 9, 30, timeZone),
                    "Landlord", "Apartment rent", PaymentMethod.BankTransfer);
            }

            var count = random.Next(0, MaxDiscretionaryPerDay + 1);

            for (var slot = 1; slot <= count; slot++)
            {
                var template = DiscretionarySpend[random.Next(DiscretionarySpend.Length)];

                // Drawn in minor units and scaled by a decimal literal, so the
                // result is exact at the column's two decimal places — scaling
                // a double would land on values like 249.99999999997.
                var amount = Money.Round(
                    random.Next(template.MinMinorUnits, template.MaxMinorUnits + 1) / 100m);

                TryAdd(
                    date,
                    slot.ToString(CultureInfo.InvariantCulture),
                    template.Category,
                    TransactionType.Expense,
                    amount,
                    AtLocalTime(date, random.Next(8, 22), random.Next(0, 60), timeZone),
                    template.Merchant,
                    description: null,
                    template.Method);
            }
        }

        return rows;
    }

    private async Task EnsureDemoBudgetsAsync(
        Guid userId,
        UserSettings settings,
        IReadOnlyDictionary<(string Name, TransactionType Type), Guid> categories,
        CancellationToken cancellationToken)
    {
        var timeZone = PeriodCalculator.ResolveTimeZone(settings.TimeZoneId);
        var window = PeriodCalculator.MonthOf(clock.UtcNow, timeZone, settings.MonthStartDay);

        // Mirrors the two unique indexes on Budgets — user, cadence and start
        // date, split by whether the cap is overall or per category — so the
        // check the code makes is the one the database enforces.
        var occupied = await db.Budgets
            .AsNoTracking()
            .IgnoreQueryFilters(IgnoreOwnershipAndSoftDelete)
            .Where(b => b.UserId == userId
                        && b.Period == BudgetPeriod.Monthly
                        && b.StartDate == window.Start)
            .Select(b => b.CategoryId)
            .ToListAsync(cancellationToken);

        var taken = occupied.ToHashSet();
        var wanted = new List<Budget>();

        if (!taken.Contains(null))
        {
            wanted.Add(BuildBudget(
                userId, "Monthly spending", categoryId: null, DemoOverallBudgetAmount, settings, window));
        }

        if (categories.TryGetValue(("Food", TransactionType.Expense), out var foodId) && !taken.Contains(foodId))
        {
            // A second, category-scoped cap so development data exercises both
            // budget shapes rather than only the overall one.
            wanted.Add(BuildBudget(
                userId, "Food this month", foodId, DemoFoodBudgetAmount, settings, window));
        }

        if (wanted.Count == 0) return;

        db.Budgets.AddRange(wanted);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {Count} demo budgets for the period starting {Start:u}.", wanted.Count, window.Start);
    }

    private static Budget BuildBudget(
        Guid userId,
        string name,
        Guid? categoryId,
        decimal amount,
        UserSettings settings,
        DateRange window) => new()
        {
            UserId = userId,
            CategoryId = categoryId,
            Name = name,
            Amount = Money.Round(amount),
            CurrencyCode = settings.CurrencyCode,
            Period = BudgetPeriod.Monthly,
            StartDate = window.Start,
            EndDate = window.End,
            IsRecurring = true,
            IsActive = true,

            // Null thresholds mean "use the account's", which is what a real
            // user gets until they override one budget in particular.
            WarningThreshold = null,
            CriticalThreshold = null,
        };

    /// <summary>
    /// The seeder's idempotency key. The calendar date is part of it so a run in
    /// a new month cannot reuse last month's key — an ordinal alone would
    /// collide with the row already holding it on the unique index and fail the
    /// entire save.
    /// </summary>
    private static string BuildReference(DateOnly date, string slot) =>
        // Invariant culture on purpose: under a non-Gregorian calendar culture
        // "yyyy" renders a different year, which would silently change the key.
        $"{SeedReferencePrefix}{date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{slot}";

    /// <summary>
    /// A wall-clock time in the account's own zone, expressed as a UTC instant —
    /// the rule the rest of the system follows, so a demo evening in
    /// Asia/Kolkata is not filed under the previous day.
    /// </summary>
    private static DateTimeOffset AtLocalTime(DateOnly date, int hour, int minute, TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(hour, minute)), DateTimeKind.Unspecified);

        // ConvertTimeToUtc throws on a local time that does not exist — the hour
        // a DST spring-forward skips. Nudging past it costs one comparison and
        // turns a once-a-year startup crash into an hour's difference nobody
        // will notice in demo data.
        while (timeZone.IsInvalidTime(local)) local = local.AddHours(1);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => error.Description));
}
