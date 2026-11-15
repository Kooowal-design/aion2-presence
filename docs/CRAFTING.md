# Crafting: evidence and limits

Beta.10 adds experimental crafting activity detection. The implementation was
replayed against a private Global capture: 13 potion attempts and 6 cooking attempts.
All 19 private detail frames were immediately followed by a matching visible start.
There were 18 results: potion cancellation and a naturally completed three-item
potion batch were included. The cooking recording stopped during its sixth attempt,
so cooking cancellation/natural final completion were not captured separately.

After installing beta.10, live use on 2026-11-15 confirmed Crafting while making both food and a bracelet. The same decoder recognized both; it is not restricted to consumables.

## Decoder

- 6F 8D carries private attempt/minigame details and a recipe varint.
- Since beta.14, a well-formed 72 8D start can activate Crafting on its own when
  its actor matches the local identity confirmed by the game's self record.
  Loot-derived or unknown identity is not sufficient for this path.
- If confirmed identity is unavailable, the immediately following 72 8D must
  match the recipe from private 6F 8D details. The unnecessary 24-byte minimum
  for those variable-length details was removed; the recipe must still be complete.
- A matching private 71 8D or actor/recipe/profession-specific 74 8D result shortens
  the activity lease to 2.5 seconds. This bridges the observed ~1.5-second repeat
  delay and clears after cancellation or the last attempt without guessing
  whether result codes mean success, failure or manual cancellation.
- Public starts for another/unknown actor, results without a confirmed start,
  recipe windows and 8B 8D Substance Morph do not activate the status.
- No result: a 60-second safety timeout clears stale state. Map changes, server
  handoff and a confirmed gathering start also clear it. Boss combat takes priority.

Recipes are parsed as varints; no per-item list or recipe-specific timer is used.
Profession values 1–5 are accepted without a recipe whitelist. The capture used to develop the decoder covered values 4/5; subsequent live bracelet crafting also worked. Remaining edge cases include unusually slow attempts exceeding 60 seconds and attribution among nearby crafters.

## ALT+G path reported missing

Initial live reports identified missing Crafting for Soulstone, Manastone,
crafting scrolls, Magic Crystal in Handicrafting, and a bow. Follow-up testing
clarified the distinction: those recipes were recognized at crafting stations,
while crafting through ALT+G was not recognized, including potions. This points
to the production path rather than a per-recipe coverage issue. The exact wire
difference has not yet been established; a separate protocol is not assumed.

Beta.14 removes an unsupported minimum detail length and adds the
confirmed-local-actor start path. It is not a confirmed fix for ALT+G.
Detection does not inspect windows, keyboard shortcuts or station interaction;
it consumes incoming game frames indicating actual production.

A short, opt-in [ALT+G diagnostic](diagnostics/Aion2Presence-AltG-Diagnostic.zip)
guides one automatic batch of three items, with no station comparison required.
Extract it on the game PC and run Start-AltG.cmd. It downloads the existing
beta.14 diagnostic engine and verifies a pinned SHA-256 before starting it.
Menu-opening markers only help analyze the recording; they are not a proposed
detection mechanism. The resulting ZIP can contain private game data and must
be shared privately, never in a public issue. Nothing uploads automatically.
See the [helper instructions and source](diagnostics/altg/README.txt).

## Replay without live collection

The engine can analyze an existing local diagnostic:

    aion-presence-engine.exe --replay-crafting PATH/TO/craft-trace.jsonl

It emits frame counts and Crafting transitions only, without echoing payloads,
character names or chat. The raw private capture is not committed or published.
Synthetic regression fixtures cover variable recipe/actor IDs, result gaps,
malformed packets, public-only events, timeout, combat and map changes.
Beta.14 also covers short detail payloads, confirmed-local starts without private
details, rejection of bystander starts, identity changes and all five profession
values. Replaying all 6,545 frames of the existing private capture produces the
same activity transitions as before the change. No new raw capture was collected
or published for these checks.

## Diagnostic interruption fixed

The first full capture stopped at roughly 314 seconds with Windows error 5 while
replacing craft-report.json. Its complete .tmp summary and intact JSONL trace
show the summary had been written before replacement failed, below both limits.
The helper previously read the report without delete sharing, which can block
atomic replacement in Windows. Beta.10 opens summaries with ReadWrite/Delete
sharing, and the engine retries transient access/sharing failures. A Windows
regression test holds a reader without delete sharing, reproduces the failed
rename, releases the handle and verifies the next checkpoint succeeds.

