"""End-to-end check of accounts and transfers.

Run against a live API:

    python accounts_smoke.py [base-url]      # default http://localhost:5165

The property this suite exists for is the one in section 6: a transfer moves
two balances and changes no total the user reads as spending. Everything else
here is ordinary CRUD that a build would mostly catch; that one is a design
invariant, and the way it usually breaks is silent.
"""
import json
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

API = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5165").rstrip("/") + "/api/v1"

# Inside the current dashboard period, at an hour where the IST calendar date
# still matches the UTC one. See the note in smoke.py.
TODAY = datetime.now(timezone.utc).strftime("%Y-%m-%d")
WHEN = TODAY + "T10:00:00Z"

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


def section(n: int, title: str) -> None:
    print(f"\n=== {n}. {title} ===")


def call(method: str, path: str, body=None, token: str | None = None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as response:
            return response.status, json.loads(response.read() or b"{}")
    except urllib.error.HTTPError as err:
        raw = err.read()
        try:
            return err.code, json.loads(raw or b"{}")
        except json.JSONDecodeError:
            return err.code, {"raw": raw.decode("utf-8", "replace")[:400]}


def register(tag: str):
    email = f"acct.{tag}.{int(time.time() * 1000)}@example.test"
    status, body = call("POST", "/auth/register", {
        "email": email,
        "password": "AcctCheck@Pass123!",
        "confirmPassword": "AcctCheck@Pass123!",
        "fullName": "Account Check",
        "acceptedTerms": True,
        "currencyCode": "INR",
        "timeZoneId": "Asia/Kolkata",
    })
    if status not in (200, 201):
        bad(f"register {tag}", body)
        sys.exit(1)
    return body["data"]["accessToken"], email


def money(value) -> str:
    return f"{float(value):.2f}"


# ---------------------------------------------------------------------------
print("=" * 60)
print("  accounts + transfers against", API)
print("=" * 60)

token, email = register("a")

section(1, "Registration seeds exactly one default account")
status, body = call("GET", "/accounts", token=token)
accounts = body.get("data", []) if status == 200 else []
if status != 200:
    bad("list accounts", body)
    sys.exit(1)

ok(f"one starter account: {[a['name'] for a in accounts]}") if len(accounts) == 1 \
    else bad("expected exactly one starter account", [a["name"] for a in accounts])

cash = accounts[0]
ok("it is the default") if cash["isDefault"] else bad("starter account is not default", cash)
if str(cash["type"]) in ("Cash", "1"):
    ok(f"it is a {cash['type']} account")
else:
    bad("starter is not type Cash", cash["type"])
ok(f"balance starts at {money(cash['balance'])}") if float(cash["balance"]) == 0 \
    else bad("starter balance is not zero", cash["balance"])
ok("currency follows the sign-up choice") if cash["currencyCode"] == "INR" \
    else bad("currency mismatch", cash["currencyCode"])

section(2, "Create a bank account with an opening balance")
status, body = call("POST", "/accounts", {
    "name": "HDFC Savings",
    "type": "Bank",
    "openingBalance": 50000,
    "institution": "HDFC Bank",
    "last4": "4321",
}, token=token)
if status != 201:
    bad("create bank account", body)
    sys.exit(1)
bank = body["data"]
ok(f"created, balance = opening = {money(bank['balance'])}") if float(bank["balance"]) == 50000 \
    else bad("opening balance not reflected", bank["balance"])
ok("the first account stays default") if not bank["isDefault"] else bad("new account stole default", bank)

section(3, "A full card number cannot be stored in last4")
status, body = call("POST", "/accounts", {
    "name": "Rejected Card", "type": "CreditCard", "last4": "4111111111111111",
}, token=token)
ok("16 digits rejected (400)") if status == 400 else bad("full card number accepted", (status, body))

section(4, "Duplicate names are refused, case-insensitively")
status, body = call("POST", "/accounts", {"name": "hdfc savings", "type": "Bank"}, token=token)
ok("case variant rejected (409)") if status == 409 else bad("duplicate name accepted", (status, body))

section(5, "An expense from the bank reduces that balance")
status, body = call("GET", "/categories?type=Expense", token=token)
category_id = body["data"][0]["id"]

status, body = call("POST", "/transactions", {
    "type": "Expense", "amount": 1500.50, "categoryId": category_id,
    "accountId": bank["id"], "transactionDate": WHEN,
    "paymentMethod": "DebitCard", "merchant": "Big Bazaar",
}, token=token)
if status != 201:
    bad("create expense against account", body)
    sys.exit(1)
ok("expense recorded against the account") if body["data"]["accountId"] == bank["id"] \
    else bad("accountId not echoed", body["data"].get("accountId"))

status, body = call("GET", f"/accounts/{bank['id']}", token=token)
bank_balance = float(body["data"]["balance"])
ok(f"50000.00 - 1500.50 = {money(bank_balance)}") if bank_balance == 48499.50 \
    else bad("bank balance wrong after expense", bank_balance)

section(6, "*** A TRANSFER MOVES BALANCES BUT IS NOT SPENDING ***")
status, body = call("GET", "/dashboard", token=token)
before = body["data"]
exp_before, inc_before = float(before["expenses"]), float(before["income"])

status, body = call("POST", "/transfers", {
    "fromAccountId": bank["id"],
    "toAccountId": cash["id"],
    "amount": 10000,
    "transferDate": WHEN,
    "notes": "ATM withdrawal",
}, token=token)
if status != 201:
    bad("create transfer", body)
    sys.exit(1)
transfer = body["data"]
ok("transfer created, both ends named: "
   f"{transfer['fromAccountName']} -> {transfer['toAccountName']}")

status, body = call("GET", f"/accounts/{bank['id']}", token=token)
bank_after = float(body["data"]["balance"])
ok(f"bank fell by 10000: {money(bank_balance)} -> {money(bank_after)}") \
    if bank_after == bank_balance - 10000 else bad("bank balance wrong after transfer", bank_after)

status, body = call("GET", f"/accounts/{cash['id']}", token=token)
cash_after = float(body["data"]["balance"])
ok(f"cash in hand rose by 10000: 0.00 -> {money(cash_after)}") if cash_after == 10000 \
    else bad("cash balance wrong after transfer", cash_after)

status, body = call("GET", "/dashboard", token=token)
after = body["data"]
exp_after, inc_after = float(after["expenses"]), float(after["income"])
ok(f"expenses unchanged at {money(exp_after)}") if exp_after == exp_before \
    else bad("TRANSFER COUNTED AS SPENDING", f"{exp_before} -> {exp_after}")
ok(f"income unchanged at {money(inc_after)}") if inc_after == inc_before \
    else bad("TRANSFER COUNTED AS INCOME", f"{inc_before} -> {inc_after}")

status, body = call("GET", "/analytics/summary?period=month", token=token)
summary = body["data"]["summary"]
ok("analytics expenses exclude the transfer too") \
    if float(summary["totalExpenses"]) == exp_before \
    else bad("transfer leaked into analytics", summary["totalExpenses"])

section(7, "Money is conserved")
status, body = call("GET", "/accounts", token=token)
total = sum(float(a["balance"]) for a in body["data"])
# 50000 opening - 1500.50 spent; the transfer moved money without creating any.
ok(f"total across accounts = {money(total)}") if total == 48499.50 \
    else bad("transfer created or destroyed money", total)

section(8, "A transfer to the same account is refused")
status, body = call("POST", "/transfers", {
    "fromAccountId": bank["id"], "toAccountId": bank["id"],
    "amount": 100, "transferDate": WHEN,
}, token=token)
ok(f"rejected ({status})") if status in (400, 422) else bad("same-account transfer accepted", (status, body))

section(9, "A transfer across currencies is refused")
status, body = call("POST", "/accounts", {
    "name": "US Account", "type": "Bank", "currencyCode": "USD",
}, token=token)
usd = body["data"]
status, body = call("POST", "/transfers", {
    "fromAccountId": bank["id"], "toAccountId": usd["id"],
    "amount": 100, "transferDate": WHEN,
}, token=token)
ok(f"rejected ({status})") if status == 422 else bad("cross-currency transfer accepted", (status, body))

section(10, "An expense cannot contradict its account's currency")
status, body = call("POST", "/transactions", {
    "type": "Expense", "amount": 10, "categoryId": category_id, "accountId": usd["id"],
    "transactionDate": WHEN, "paymentMethod": "Cash", "currencyCode": "INR",
}, token=token)
ok(f"rejected ({status})") if status == 422 else bad("currency mismatch accepted", (status, body))

section(11, "An account with history cannot be deleted")
status, body = call("DELETE", f"/accounts/{bank['id']}", token=token)
ok(f"refused ({status}), archive instead") if status == 422 \
    else bad("account with history was deleted", (status, body))

section(12, "Transactions can be filtered to one account")
status, body = call("GET", f"/transactions?accountId={bank['id']}&page=1&pageSize=20", token=token)
items = body["data"]["items"]
ok(f"{len(items)} transaction(s), all on this account") \
    if items and all(t["accountId"] == bank["id"] for t in items) \
    else bad("account filter wrong", [t.get("accountId") for t in items])

section(13, "*** ISOLATION: another user cannot see or use these accounts ***")
token_b, _ = register("b")

status, body = call("GET", "/accounts", token=token_b)
b_ids = {a["id"] for a in body["data"]}
ok("B sees only their own starter account") if bank["id"] not in b_ids and len(b_ids) == 1 \
    else bad("ISOLATION BREACH: B can list A's accounts", b_ids)

status, body = call("GET", f"/accounts/{bank['id']}", token=token_b)
ok(f"B reading A's account -> {status}") if status == 404 \
    else bad("ISOLATION BREACH on read", (status, body))

status, body = call("GET", f"/transfers/{transfer['id']}", token=token_b)
ok(f"B reading A's transfer -> {status}") if status == 404 \
    else bad("ISOLATION BREACH on transfer read", (status, body))

b_accounts = call("GET", "/accounts", token=token_b)[1]["data"]
status, body = call("POST", "/transfers", {
    "fromAccountId": bank["id"], "toAccountId": b_accounts[0]["id"],
    "amount": 1, "transferDate": WHEN,
}, token=token_b)
ok(f"B transferring OUT of A's account -> {status}") if status in (404, 422) \
    else bad("ISOLATION BREACH: B moved money from A's account", (status, body))

section(14, "Deleting a transfer returns both balances")
status, _ = call("DELETE", f"/transfers/{transfer['id']}", token=token)
status, body = call("GET", f"/accounts/{cash['id']}", token=token)
cash_restored = float(body["data"]["balance"])
ok(f"cash back to {money(cash_restored)}") if cash_restored == 0 \
    else bad("balance did not reverse", cash_restored)

status, body = call("GET", f"/accounts/{bank['id']}", token=token)
bank_restored = float(body["data"]["balance"])
ok(f"bank back to {money(bank_restored)}") if bank_restored == 48499.50 \
    else bad("balance did not reverse", bank_restored)

# ---------------------------------------------------------------------------
print("\n" + "=" * 60)
print(f"  PASSED: {len(passed)}     FAILED: {len(failed)}")
if failed:
    print("\n  Failures:")
    for item in failed:
        print("    - " + item)
print("=" * 60)
sys.exit(1 if failed else 0)
