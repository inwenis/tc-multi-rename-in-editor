# Partial Rename + Retry-Locked Dialog

Date: 2026-06-21
Branch: `rename-retry-locked`

## Problem

`RenameExecutor.Execute` today is all-or-nothing. If any file fails to rename
(for example because another process holds it open), the executor rolls every
file back to its original name, throws, and the app shows a single error box.

The user wants: when one or more files are locked by another process, rename the
files that can be renamed, keep the locked ones at their original names, and
return to a dialog that marks which files could not be renamed — so the user can
close the locking application and retry just those files.

## Goals

1. Rename all unlocked files; commit them immediately.
2. Leave locked files at their original names, clearly marked.
3. Show a results dialog listing every changed file with `OK` / `LOCKED`
   status.
4. Offer a `Retry locked` action that re-attempts only the still-locked files.
5. Keep real, unexpected errors loud (still shown as a hard error).

## Non-Goals

- No re-launch of the external editor; names are not re-editable in the retry
  flow. The retry path only re-attempts the rename of already-validated names.
- No change to validation, editor launching, or the confirmation preview flow.
- No automatic detection of which process holds a lock.

## Current Flow (unchanged parts)

`Program.Main`:

1. Load selected files.
2. Launch external editor, wait, read edited names.
3. `RenamePlanner.Build` validates and builds the plan.
4. Optional confirmation preview (`Ui.ConfirmRename`).
5. `RenameExecutor.Execute(plan)`.

Steps 1–4 are unchanged. Step 5 and the failure handling around it change.

## Design

### 1. Partial rename semantics

`RenameExecutor.Execute` stops being all-or-nothing for lock-type failures.

- Unlocked files are renamed and committed (left at their final target names).
- Locked files are left at their original names.
- The method returns a result describing successes and failures instead of
  throwing when the only problem is locked files.

### 2. Lock-tolerant two-phase rename

The existing two-phase approach (temp move, then final move) is kept because it
is what makes swaps and case-only renames safe. It is made tolerant of locked
files:

- **Phase 1 (temp move):** for each changed item, attempt
  `File.Move(source, temp)`. If it fails with a lock-type error, record the item
  as failed and skip it. Other items continue.
- **Phase 2 (final move):** for each item that was successfully temp-moved,
  attempt `File.Move(temp, target)`. If the target is still occupied by a failed
  (locked) item — for example the other half of a swap — this move fails; record
  the item as failed and roll its temp file back to the original name so it ends
  up at its original name too.
- Items that complete phase 2 are committed (succeeded).

Result of `Execute`: a `RenameResult` containing:

- `Succeeded`: items now at their target names.
- `Failed`: items still at their original names, each with a reason string.

### 3. Failure classification

During a move:

- `IOException` (sharing/lock violation) → treated as `LOCKED`, retryable.
- `UnauthorizedAccessException` → treated as `LOCKED`, retryable (a file open
  for writing elsewhere commonly surfaces this on Windows).
- Any other exception → unexpected. The executor rolls back what it can and
  throws, preserving today's hard-error behavior so real bugs stay visible.

### 4. Retry dialog (`Ui.ShowRenameResults`)

Shown only when `RenameResult.Failed` is non-empty.

Contents:

- One line per changed file: `source name -> new name   OK | LOCKED (open elsewhere)`.
- A `Retry locked` button.
- A `Close` button.

Behavior:

- `Retry locked` → build a sub-plan from the failed items (still at their
  original names) and call `Execute` again. Update the dialog with the new
  result. Loop until `Failed` is empty or the user closes.
- `Close` → exit. Remaining locked files simply stay un-renamed; no error box.

Layout follows the existing `Ui.ConfirmRename` monospace preview style.

### 5. Orchestration (`Program.Main`)

Replace the single `RenameExecutor.Execute(plan)` call with:

1. `result = Execute(plan)`.
2. If `result.Failed` is empty → return `0` (silent success, as today).
3. Otherwise show `Ui.ShowRenameResults`, which owns the retry loop.
4. After the dialog closes, return `0` if nothing is still failed, else `1`.

### 6. Exit codes

- All files renamed (including after one or more retries) → `0`.
- User closes the dialog with files still locked → `1`, so Total Commander sees
  a non-success result.
- Unexpected (non-lock) failure → `1` via the existing error path.

## Data Shapes

- `RenamePlanItem` already carries `Source`, `NewName`, `TargetPath`,
  `TemporaryPath`. Add a transient `FailureReason` (nullable string) or carry it
  in the result; the result object is preferred to keep the plan item a value of
  the plan.
- New `RenameResult` (or similar): `IReadOnlyList<RenamePlanItem> Succeeded`,
  `IReadOnlyList<RenameFailure> Failed` where `RenameFailure` pairs an item with
  a reason string.

## Testing

No test project exists yet. Add a small test project (xUnit) covering the
executor and planner, using real temp directories:

1. **Partial success:** three files, one held open with a deny-rename share mode
   → two succeed, one reported failed; the failed file keeps its original name.
2. **All unlocked:** all files rename; result has no failures (regression guard
   for the happy path).
3. **Swap with one side locked:** `a <-> b` with `a` locked → both reported
   failed and both end at their original names (no half-applied swap).
4. **Retry then succeed:** simulate a file that fails the first attempt and
   succeeds the second → executor on the failed sub-plan completes it.
5. **Unexpected error still throws:** a non-lock failure path still produces the
   hard-error behavior.

Locking in tests: open the target file with a `FileShare` mode that denies the
move, perform the rename, then release.

## Risks / Notes

- Distinguishing "locked" from other `IOException`s relies on HRESULT codes
  (`ERROR_SHARING_VIOLATION` 0x20, `ERROR_LOCK_VIOLATION` 0x21). Treating all
  `IOException` during a move as retryable is acceptable here: the file is still
  at a known name, and retry is harmless. The implementation may classify by
  HRESULT for a more precise message but should not hard-fail an otherwise
  recoverable rename.
- `UnauthorizedAccessException` is treated as retryable by decision; a
  genuinely permission-denied file will simply keep failing on retry, which the
  user resolves or closes out of.
