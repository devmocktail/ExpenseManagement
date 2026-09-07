"""End-to-end smoke test against a running API.

Exercises the paths a build cannot prove: registration, the token lifecycle,
money arithmetic, and above all that one user cannot reach another's data.

    python smoke.py [base-url]        # default http://localhost:5165

Exits non-zero if anything fails, so it is usable as a deployment gate.
This is a smoke test, not a substitute for the unit and integration suites
that still need writing.
"""
import json
import sys
import time
import urllib.error
import urllib.request

API = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5165").rstrip("/") + "/api/v1"

passed: list[str] = []
failed: list[str] = []


def ok(msg: str) -> None:
    print("  PASS  " + msg)
    passed.append(msg)


def bad(msg: str, detail: object = "") -> None:
    print("  FAIL  " + msg)
    if detail:
        print("        " + str(detail)[:400])
    failed.append(msg)


def call(method: str, path: str, body=None, token: str | None = None):
    """Returns (status, parsed-json-or-text)."""
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    if data is not None:
        req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req) as response:
            raw = response.read().decode()
            try:
                return response.status, json.loads(raw)
            except json.JSONDecodeError:
                return response.status, raw
    except urllib.error.HTTPError as error:
        raw = error.read().decode()
        try:
            return error.code, json.loads(raw)
        except json.JSONDecodeError:
            return error.code, raw
    except Exception as error:  # noqa: BLE001
        return 0, str(error)


def section(n: int, title: str) -> None:
    print(f"\n=== {n}. {title} ===")


stamp = str(int(time.time()))
EMAIL_A = f"alice.{stamp}@example.test"
EMAIL_B = f"bob.{stamp}@example.test"
PW = "Str0ng!Passw0rd"

# --------------------------------------------------------------------- auth
section(1, "Register user A")
_, ra = call("POST", "/auth/register", {
    "fullName": "Alice Test", "email": EMAIL_A, "password": PW,
    "confirmPassword": PW, "acceptedTerms": True,
    "currencyCode": "INR", "timeZoneId": "Asia/Kolkata",
})
token_a = ra.get("data", {}).get("accessToken") if isinstance(ra, dict) else None
refresh_a = ra.get("data", {}).get("refreshToken") if isinstance(ra, dict) else None
ok("registered A") if token_a else bad("register A", ra)

section(2, "Register user B")
_, rb = call("POST", "/auth/register", {
    "fullName": "Bob Test", "email": EMAIL_B, "password": PW,
    "confirmPassword": PW, "acceptedTerms": True,
})
token_b = rb.get("data", {}).get("accessToken") if isinstance(rb, dict) else None
ok("registered B") if token_b else bad("register B", rb)

section(3, "Duplicate email rejected")
s, r = call("POST", "/auth/register", {
    "fullName": "Dup", "email": EMAIL_A, "password": PW,
    "confirmPassword": PW, "acceptedTerms": True,
})
ok("duplicate email -> 409") if s == 409 else bad("duplicate email", f"{s} {r}")

section(4, "Wrong password rejected")
s_wrong, rw = call("POST", "/auth/login", {"email": EMAIL_A, "password": "WrongPass1!"})
code_wrong = rw.get("errorCode") if isinstance(rw, dict) else None
ok(f"wrong password -> {code_wrong}") if s_wrong >= 400 else bad("wrong password accepted", rw)

section(5, "Unknown email is indistinguishable (no enumeration oracle)")
s_unknown, ru = call("POST", "/auth/login",
                     {"email": f"nobody.{stamp}@example.test", "password": "WrongPass1!"})
code_unknown = ru.get("errorCode") if isinstance(ru, dict) else None
if code_wrong == code_unknown and s_wrong == s_unknown:
    ok(f"unknown email identical to wrong password ({code_unknown})")
else:
    bad("ACCOUNT ENUMERATION ORACLE",
        f"known={s_wrong}/{code_wrong} unknown={s_unknown}/{code_unknown}")

section(6, "Weak password rejected")
s, r = call("POST", "/auth/register", {
    "fullName": "Weak", "email": f"weak.{stamp}@example.test",
    "password": "abc", "confirmPassword": "abc", "acceptedTerms": True,
})
ok(f"weak password -> {s}") if s >= 400 else bad("weak password accepted", r)

section(7, "Unauthenticated request rejected")
s, _ = call("GET", "/categories")
ok("no token -> 401") if s == 401 else bad("unauthenticated request", s)

# --------------------------------------------------------------- categories
section(8, "Default categories seeded at registration")
_, cats = call("GET", "/categories", token=token_a)
cat_list = cats.get("data", []) if isinstance(cats, dict) else []
ok(f"A has {len(cat_list)} default categories") if len(cat_list) >= 15 else bad("defaults", cats)

expense_cat = next((c for c in cat_list if c["type"] == "Expense"), None)
income_cat = next((c for c in cat_list if c["type"] == "Income"), None)
cat_id = expense_cat["id"] if expense_cat else None
inc_id = income_cat["id"] if income_cat else None

section(9, "B's categories are a separate copy")
_, cats_b = call("GET", "/categories", token=token_b)
b_ids = {c["id"] for c in cats_b.get("data", [])} if isinstance(cats_b, dict) else set()
a_ids = {c["id"] for c in cat_list}
if b_ids and not (a_ids & b_ids):
    ok(f"A and B share no category ids ({len(b_ids)} each)")
else:
    bad("categories shared between users", a_ids & b_ids)

section(10, "Category names are case-insensitively unique")
existing = expense_cat["name"] if expense_cat else "Food"
s, r = call("POST", "/categories", {
    "name": existing.lower(), "type": "Expense", "icon": "food", "color": "#6366F1",
}, token=token_a)
if s == 409:
    ok(f"'{existing.lower()}' rejected as duplicate of '{existing}'")
else:
    bad("CASE-SENSITIVITY REGRESSION: duplicate accepted", f"{s} {r}")

# ------------------------------------------------------------- transactions
section(11, "Create transaction")
s, txn = call("POST", "/transactions", {
    "type": "Expense", "amount": 450.75, "categoryId": cat_id,
    "transactionDate": "2026-09-01T10:00:00Z", "paymentMethod": "Upi",
    "merchant": "Test Restaurant",
}, token=token_a)
tx = txn.get("data") if isinstance(txn, dict) else None
tx_id = tx["id"] if tx else None
ok(f"created transaction ({s})") if tx_id else bad("create transaction", txn)
if tx:
    ok("decimal preserved exactly: 450.75") if tx["amount"] == 450.75 \
        else bad("decimal precision lost", tx["amount"])

section(12, "A reads own transaction")
s, r = call("GET", f"/transactions/{tx_id}", token=token_a)
ok("A -> 200") if s == 200 else bad("A cannot read own", f"{s} {r}")

section(13, "*** ISOLATION: B cannot read A's transaction ***")
s, r = call("GET", f"/transactions/{tx_id}", token=token_b)
if s == 404:
    ok("B -> 404 (not 403: no existence leak)")
elif s == 403:
    bad("EXISTENCE LEAK: 403 confirms the row exists", r)
else:
    bad("ISOLATION BREACH", f"{s} {r}")

section(14, "*** ISOLATION: B's list is empty ***")
_, lb = call("GET", "/transactions", token=token_b)
total_b = lb.get("data", {}).get("totalCount") if isinstance(lb, dict) else None
ok("B sees 0 transactions") if total_b == 0 else bad(f"ISOLATION BREACH: B sees {total_b}", lb)

section(15, "*** ISOLATION: B cannot delete A's transaction ***")
s, _ = call("DELETE", f"/transactions/{tx_id}", token=token_b)
ok(f"B delete -> {s}") if s == 404 else bad("ISOLATION BREACH on delete", s)

section(16, "*** ISOLATION: B cannot update A's transaction ***")
s, r = call("PUT", f"/transactions/{tx_id}", {
    "type": "Expense", "amount": 1, "categoryId": cat_id,
    "transactionDate": "2026-09-01T10:00:00Z", "paymentMethod": "Cash",
}, token=token_b)
ok(f"B update -> {s}") if s in (404, 422) else bad("ISOLATION BREACH on update", f"{s} {r}")

section(17, "*** ISOLATION: B cannot use A's category ***")
s, r = call("POST", "/transactions", {
    "type": "Expense", "amount": 10, "categoryId": cat_id,
    "transactionDate": "2026-09-01T10:00:00Z", "paymentMethod": "Cash",
}, token=token_b)
ok(f"B using A's categoryId -> {s}") if s in (404, 422) else bad("ISOLATION BREACH via category", f"{s} {r}")

# --------------------------------------------------------------------- money
section(18, "Dashboard reflects the transaction")
_, dash = call("GET", "/dashboard", token=token_a)
d = dash.get("data") if isinstance(dash, dict) else {}
ok("expenses = 450.75") if d.get("expenses") == 450.75 else bad("expenses", d.get("expenses"))
ok("balance = -450.75") if d.get("balance") == -450.75 else bad("balance", d.get("balance"))
ok("currency INR from settings") if d.get("currencyCode") == "INR" else bad("currency", d.get("currencyCode"))

section(19, "Add income; balance recomputes exactly")
call("POST", "/transactions", {
    "type": "Income", "amount": 65000.00, "categoryId": inc_id,
    "transactionDate": "2026-09-01T09:00:00Z", "paymentMethod": "BankTransfer",
    "merchant": "Employer",
}, token=token_a)
_, dash2 = call("GET", "/dashboard", token=token_a)
d2 = dash2.get("data") if isinstance(dash2, dict) else {}
ok("income = 65000.0") if d2.get("income") == 65000.0 else bad("income", d2.get("income"))
if d2.get("balance") == 64549.25:
    ok("65000.00 - 450.75 = 64549.25 exactly")
else:
    bad("balance after income", d2.get("balance"))

section(20, "Income does not leak into expenses")
ok("expenses still 450.75") if d2.get("expenses") == 450.75 else bad("income counted as expense", d2.get("expenses"))

# -------------------------------------------------------------------- tokens
section(21, "Refresh rotates the token")
_, rr = call("POST", "/auth/refresh", {"refreshToken": refresh_a})
new_refresh = rr.get("data", {}).get("refreshToken") if isinstance(rr, dict) else None
if new_refresh and new_refresh != refresh_a:
    ok("refresh returned a NEW token")
else:
    bad("refresh rotation", rr)

section(22, "*** REUSE DETECTION: the old token is rejected ***")
s, rr2 = call("POST", "/auth/refresh", {"refreshToken": refresh_a})
ok(f"reused token rejected ({s})") if s >= 400 else bad("TOKEN REUSE NOT DETECTED", rr2)

section(23, "Reuse revoked the whole family")
s, rr3 = call("POST", "/auth/refresh", {"refreshToken": new_refresh})
ok(f"successor revoked too ({s})") if s >= 400 else bad("family not revoked", rr3)

# ------------------------------------------------------------------ contract
section(24, "Nullable fields are written, not omitted")
_, td = call("GET", f"/transactions/{tx_id}", token=token_a)
body = td.get("data", {}) if isinstance(td, dict) else {}
missing = [k for k in ("notes", "description", "recurringTransactionId", "updatedAt") if k not in body]
ok("all nullable keys present") if not missing else bad("nullable keys omitted", missing)

section(25, "Validation returns field-level errors")
s, ve = call("POST", "/transactions", {
    "type": "Expense", "amount": -5, "categoryId": cat_id,
    "transactionDate": "2026-09-01T10:00:00Z", "paymentMethod": "Cash",
}, token=token_a)
errs = ve.get("errors", []) if isinstance(ve, dict) else []
if s in (400, 422) and errs:
    ok(f"negative amount -> {ve.get('errorCode')} on '{errs[0].get('field')}'")
else:
    bad("validation", f"{s} {ve}")

section(26, "Search matches merchant")
_, sr = call("GET", "/transactions?search=Restaurant", token=token_a)
n = sr.get("data", {}).get("totalCount") if isinstance(sr, dict) else None
ok("search 'Restaurant' -> 1") if n == 1 else bad("search", n)

section(27, "Search is case-insensitive")
_, sr_lower = call("GET", "/transactions?search=restaurant", token=token_a)
n_lower = sr_lower.get("data", {}).get("totalCount") if isinstance(sr_lower, dict) else None
if n_lower == 1:
    ok("lowercase 'restaurant' also matches 'Test Restaurant'")
else:
    bad("CASE-SENSITIVITY REGRESSION: lowercase search found nothing", n_lower)

section(28, "LIKE wildcards are escaped, not interpreted")
_, sr2 = call("GET", "/transactions?search=%25", token=token_a)
n2 = sr2.get("data", {}).get("totalCount") if isinstance(sr2, dict) else None
ok("'%' treated as a literal") if n2 == 0 else bad(f"LIKE INJECTION: '%' matched {n2} rows", sr2)

section(29, "Pagination metadata is coherent")
_, pg = call("GET", "/transactions?page=1&pageSize=1", token=token_a)
p = pg.get("data", {}) if isinstance(pg, dict) else {}
if (p.get("pageSize") == 1 and p.get("totalCount") == 2
        and p.get("totalPages") == 2 and p.get("hasNextPage") is True):
    ok("page=1 size=1 of 2 -> totalPages=2, hasNextPage=true")
else:
    bad("pagination metadata", p)

section(30, "Oversized pageSize is clamped")
_, pg2 = call("GET", "/transactions?pageSize=100000", token=token_a)
p2 = pg2.get("data", {}) if isinstance(pg2, dict) else {}
ok(f"clamped to {p2.get('pageSize')}") if p2.get("pageSize", 10**9) <= 100 \
    else bad("UNBOUNDED PAGE SIZE", p2.get("pageSize"))

section(31, "Analytics aggregates server-side")
_, an = call("GET", "/analytics/summary?period=month", token=token_a)
summ = an.get("data", {}).get("summary") if isinstance(an, dict) else None
if summ and summ.get("totalExpenses") == 450.75 and summ.get("totalIncome") == 65000.0:
    ok(f"income={summ.get('totalIncome')} expenses={summ.get('totalExpenses')} savings={summ.get('savings')}")
else:
    bad("analytics summary", an)

section(32, "Settings round-trip")
_, st = call("GET", "/profile/settings", token=token_a)
settings = st.get("data") if isinstance(st, dict) else {}
if settings.get("currencyCode") == "INR" and settings.get("timeZoneId") == "Asia/Kolkata":
    ok("registration hints honoured (INR / Asia/Kolkata)")
else:
    bad("settings", settings)

_, up = call("PUT", "/profile/settings", {"currencyCode": "USD"}, token=token_a)
ok("currency changed to USD") if isinstance(up, dict) and up.get("data", {}).get("currencyCode") == "USD" \
    else bad("update settings", up)

s, up2 = call("PUT", "/profile/settings", {"timeZoneId": "Not/AZone"}, token=token_a)
ok(f"unresolvable timezone rejected ({s})") if s >= 400 else bad("invalid timezone accepted", up2)

section(33, "Soft delete removes it from the list")
s, r = call("DELETE", f"/transactions/{tx_id}", token=token_a)
if s in (200, 204):
    _, after = call("GET", "/transactions", token=token_a)
    n = after.get("data", {}).get("totalCount") if isinstance(after, dict) else None
    ok(f"after delete, list has {n} (was 2)") if n == 1 else bad("soft delete", n)
else:
    bad("delete own transaction", f"{s} {r}")

print("\n" + "=" * 60)
print(f"  PASSED: {len(passed)}     FAILED: {len(failed)}")
if failed:
    print("\n  Failures:")
    for item in failed:
        print("    - " + item)
print("=" * 60)
raise SystemExit(1 if failed else 0)
