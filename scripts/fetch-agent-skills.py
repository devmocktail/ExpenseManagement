"""Fetches supabase/agent-skills into .claude/skills.

The `skills` CLI would normally do this, but it requires Node >= 22.20 and this
machine has 21.4. The skills themselves are just markdown and reference files,
so there is nothing the CLI does here that a directory copy does not.
"""
import base64
import json
import os
import sys
import urllib.request

REPO = "supabase/agent-skills"
API = f"https://api.github.com/repos/{REPO}/contents"
DEST = sys.argv[1] if len(sys.argv) > 1 else "D:/native_app/.claude/skills"

files_written = 0
bytes_written = 0


def get(path: str):
    req = urllib.request.Request(
        f"{API}/{path}",
        headers={"Accept": "application/vnd.github+json", "User-Agent": "fetch-skills"},
    )
    with urllib.request.urlopen(req) as response:
        return json.loads(response.read().decode())


def walk(remote: str, local: str) -> None:
    global files_written, bytes_written

    entries = get(remote)
    os.makedirs(local, exist_ok=True)

    for entry in entries:
        target = os.path.join(local, entry["name"])

        if entry["type"] == "dir":
            walk(entry["path"], target)
            continue

        if entry["type"] != "file":
            continue

        # The contents API inlines files under 1 MB as base64; anything larger
        # comes back with a null content and has to be fetched from download_url.
        blob = get(entry["path"])
        if blob.get("content"):
            data = base64.b64decode(blob["content"])
        else:
            with urllib.request.urlopen(blob["download_url"]) as response:
                data = response.read()

        with open(target, "wb") as handle:
            handle.write(data)

        files_written += 1
        bytes_written += len(data)
        print(f"  {os.path.relpath(target, DEST).replace(os.sep, '/')}  ({len(data)} bytes)")


for skill in ("supabase", "supabase-postgres-best-practices"):
    print(f"=== {skill} ===")
    walk(f"skills/{skill}", os.path.join(DEST, skill))

print(f"\n{files_written} files, {bytes_written} bytes -> {DEST}")
