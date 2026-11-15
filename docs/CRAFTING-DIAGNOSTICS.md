# Optional crafting diagnostic v3

Download Aion2Presence-Crafting-Diagnostic.zip **inside ShadowPC**, extract it, and run
Start-Diagnostic.cmd there. Keep AION 2 running and exit normal Presence from its tray menu.
No installation or transfer from the local PC is required.

The helper first checks that game frames arrive. It then guides:
1. An idle baseline, followed by opening the alchemy recipe window.
2. ONE automatic potion batch of at least 20 items, observed for 60 seconds, then canceled.
3. A separate batch of exactly 3 potions, allowed to finish naturally.
4. The same window/batch/cancel/natural-finish sequence for cooking.

Use recipes you already intend to craft. The 20-item batch is canceled early;
there is no need to finish 200 items. Enter recipe names when prompted if convenient.
Return to the helper at each requested step; do not start each item manually.
If a batch ends before cancellation, mention that in the chat instead of repeating it.
The phase markers describe user action windows, not exact inferred game event times.

## What is recorded

This deliberately replaces the insufficient metadata-only diagnostic.
All incoming game stream chunks admitted by the existing capture pipeline are stored
after TCP ordering, before stream assembly, with anonymous connection indices.
All decoded frames are also stored without an opcode allowlist or payload truncation.
Each record has an order number, millisecond elapsed time, phase and complete hexadecimal
bytes. Stream chunks retain the capture timestamp. The report includes numeric local
player ID when known, capture/port status, per-phase frame counts and stop/error status.
Engine warnings/errors are saved alongside the trace, including reported TCP gaps or
capture queue drops. It is not a general PC network recording and does not capture
client-to-server commands. It does not guarantee Npcap itself received every game byte.

The trace can contain character information, chat and other private game data.
Do not post it in a public issue or commit it to the repository.
There is no automatic upload. Normal Presence does not enable this recording.

## Result and limits

Attach the **craft-diagnostic-....zip** printed by the helper in the support chat.
It includes craft-report.json, craft-trace.jsonl, test-context.json, action markers and
engine logs, so send the whole ZIP rather than only the small report.
Trace data is appended and flushed every second; the summary is atomically replaced.
The helper preserves and packages partial data on an ordinary failure.
Hard-closing the console may require manually zipping the generated diagnostic folder.

A session stops after 20 minutes or 128 MiB of trace data, or on a recording error.
Limits and errors are explicit in the report; the helper checks liveness during timed
steps and before/after input prompts. This tool is passive and does not click in the game.

This is a diagnostic package, separate from the experimental crafting detector
introduced in beta.10. Complete bytes let us revisit unknown fields and replay parsing
without collecting the same missing payload again. Potion and food traces share a
lifecycle; no per-item configuration is required. They cannot prove every profession
behaves identically. See [decoder evidence and limits](CRAFTING.md).
Beta.10 also fixes Windows report-reader sharing and retries transient replacement errors.
Source and dependency licenses are supplied with the release.

