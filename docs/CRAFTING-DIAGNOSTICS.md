# Optional crafting discovery probe

Download the diagnostic ZIP in the ShadowPC browser, extract it, and run
`Start-Diagnostic.cmd` inside ShadowPC. It starts the bundled PowerShell script;
the execution-policy override applies only to this helper process. Exit normal Presence
first; keep AION 2 running. The script guides an idle baseline, one potion
batch, and cancellation. No per-recipe configuration is planned.

Attach the resulting `craft-report.json` in this chat. It is local only and
contains per-second counts, two-byte message types and body sizes. It contains
no packet payloads, player identity, chat, network addresses or port numbers.
This opt-in mode has a 180-second limit and a 50,000-row bound. Normal Presence
does not write this report. No files are uploaded automatically.

This is a discovery tool, not working workstation-crafting support. Metadata
can identify candidate messages and cadence, but cannot verify unknown payload
fields or local-player attribution. Another targeted test may be needed.
The eventual decoder must recognize workstation start/result/end/cancel,
exclude morphs and other players, and expire stale state. Do not implement it
from message frequency alone. Test representative professions if necessary,
not every item. Source and dependency licenses are supplied with the release.
