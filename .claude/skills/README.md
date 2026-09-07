# Agent skills

Fetched from [supabase/agent-skills](https://github.com/supabase/agent-skills)
(MIT). Two skills: `supabase` and `supabase-postgres-best-practices`.

Installed by copying rather than with `npx skills add`, because that CLI
requires Node >= 22.20 and this machine runs 21.4 — it fails on
`util.styleText`. The skills are plain markdown, so a copy is equivalent.
`scripts/fetch-agent-skills.py` reproduces it.

## Two places where this project deliberately differs

The guidance is general Postgres advice and is sound. Two pieces conflict with
decisions already made here, so read them with this context:

**`schema-lowercase-identifiers.md`** recommends unquoted lowercase identifiers.
This schema uses quoted PascalCase (`"Transactions"`, `"UserId"`) because EF
Core generates them from the C# property names, and overriding that means a
naming convention on every entity plus hand-maintaining the mapping. The cost
the skill warns about is real and we pay it: every hand-written query has to
quote its identifiers, which is why `database/*.sql` does so throughout and
`database/README.md` calls it out. Worth revisiting only if this schema is ever
queried mainly by hand rather than through EF.

**`security-rls-*.md`** covers Row Level Security. This project does not use it.
Isolation is enforced by the DbContext's global query filters, and the mobile
app never talks to PostgREST — every request goes through the API, which is what
applies validation, ownership checks and audit logging. Adding RLS would put a
second mechanism behind the same rule, and two enforcement points that must
agree eventually will not. See `docs/supabase.md`.

`conn-prepared-statements.md` agrees with what we already do: it describes
exactly the transaction-pooler failure that is why `docs/supabase.md` insists on
the session pooler on port 5432.
