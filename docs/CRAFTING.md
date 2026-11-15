# Workstation crafting: evidence and limits

Beta.10 adds experimental workstation activity detection. The implementation was
replayed against a private Global capture: 13 potion attempts and 6 cooking attempts.
All 19 private detail frames were immediately followed by a matching visible start.
There were 18 results: potion cancellation and a naturally completed three-item
potion batch were included. The cooking recording stopped during its sixth attempt,
so cooking cancellation/natural final completion were not captured separately.

## Decoder

- 6F 8D carries private attempt/minigame details and a recipe varint.
- The immediately following 72 8D must contain the same recipe and a well-formed
  actor/recipe/profession tuple. Only this pair activates Crafting.
- A matching private 71 8D or actor/recipe/profession-specific 74 8D result shortens
  the activity lease to 2.5 seconds. This bridges the observed ~1.5-second repeat
  delay and clears after cancellation or the last attempt without guessing
  whether result codes mean success, failure or manual cancellation.
- Public starts/results by themselves, recipe windows and 8B 8D Substance Morph
  do not activate the status.
- No result: a 60-second safety timeout clears stale state. Map changes, server
  handoff and a confirmed gathering start also clear it. Boss combat takes priority.

Recipes are parsed as varints; no per-item list or recipe-specific timer is used.
Profession values 1–5 are accepted, but only alchemy/cooking (4/5) were in this
capture. Other professions, unusually slow attempts exceeding 60 seconds and
crowded-workstation attribution still need live validation. This is not a claim
that every recipe and profession was tested.

## Replay without live collection

The engine can analyze an existing local diagnostic:

    aion-presence-engine.exe --replay-crafting PATH/TO/craft-trace.jsonl

It emits frame counts and Crafting transitions only, without echoing payloads,
character names or chat. The raw private capture is not committed or published.
Synthetic regression fixtures cover variable recipe/actor IDs, result gaps,
malformed packets, public-only events, timeout, combat and map changes.

## Diagnostic interruption fixed

The first full capture stopped at roughly 314 seconds with Windows error 5 while
replacing craft-report.json. Its complete .tmp summary and intact JSONL trace
show the summary had been written before replacement failed, below both limits.
The helper previously read the report without delete sharing, which can block
atomic replacement in Windows. Beta.10 opens summaries with ReadWrite/Delete
sharing, and the engine retries transient access/sharing failures. A Windows
regression test holds a reader without delete sharing, reproduces the failed
rename, releases the handle and verifies the next checkpoint succeeds.

