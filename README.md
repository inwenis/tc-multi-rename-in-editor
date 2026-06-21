# TC Batch Rename

`TC Batch Rename` is a small helper executable for Total Commander. It opens the selected file names in a text editor, waits for the editor to close, validates the edited names, shows a confirmation preview, and then renames the files safely.

## Architecture

This is an external helper, not a native Total Commander plugin.

- Total Commander already has the right launch mechanism for this job: `%UL` creates a UTF-8 list file containing the selected items with full paths.
- An external helper is simpler to bind to a button, menu item, or hotkey than a custom plugin, and it avoids plugin SDK complexity.
- The helper can wait for the configured editor process to exit, then validate everything before renaming.

Internally the helper uses a two-phase rename:

1. Every changed file is renamed to a unique temporary name in its original directory.
2. After all temporary renames succeed, every file is renamed from its temporary name to the final target name.

That makes swaps and case-only renames safe and avoids most preventable partial-rename conflicts.

If some files cannot be renamed because another process holds them open, the tool does not abort everything. It renames every file it can, leaves the locked files at their original names, and shows a results window that marks each locked file. Close the application holding those files and click **Retry locked** to re-attempt only the failed ones. Any other, unexpected failure still rolls back and reports a hard error.

## Behavior

The helper does the following:

1. Reads the selected files from a Total Commander list file or direct file arguments.
2. Writes the current file names, one per line, to a temporary UTF-8 text file.
3. Launches the configured editor and waits for it to exit.
4. Re-reads the edited names and validates them.
5. If nothing changed, it exits without renaming anything.
6. If validation passes, it shows a rename preview and asks for confirmation.
7. If confirmed, it renames the files.
8. If any file is locked by another process, it shows a results window marking the locked files and offers a retry for just those.

Validation rules:

- The edited file must contain exactly the same number of lines as the number of selected files.
- Any empty line aborts the entire operation.
- Duplicate target names in the same directory abort the entire operation.
- Invalid Windows file names abort the entire operation.
- Existing target-file conflicts abort the entire operation.
- Only the leaf file name may change. The original directory is always preserved.

## Build and deploy

### Quick build

```powershell
.\build.ps1
```

This builds a single-file, framework-dependent executable at `artifacts\publish\win-x64\TcBatchRename.exe`. It requires a matching .NET 10 desktop runtime on the target machine. `build.ps1` is a thin wrapper over `publish.ps1 -Runtime win-x64 -SingleFile`.

### Deploy

```powershell
.\deploy.ps1
```

This rebuilds, then deploys to `C:\programki`:

- Creates the target directory if it does not exist.
- Copies `TcBatchRename.exe` (overwrites).
- Copies `tc-batch-rename.json` only if it is missing at the target, so a tuned deployed config survives redeploys.

Deploy to a different location with `-Target`:

```powershell
.\deploy.ps1 -Target "C:\Tools\TcBatchRename"
```

## Build (publish options)

### Recommended baseline: portable publish

```powershell
.\publish.ps1
```

This publishes the app to `artifacts\publish\portable`. It is the least fragile publish option because it does not require downloading extra runtime packs at publish time.

### Optional: single-file framework-dependent publish

```powershell
.\publish.ps1 -Runtime win-x64 -SingleFile
```

This produces a single-file executable in `artifacts\publish\win-x64`, but it requires a matching .NET 10 desktop runtime on the target machine.

### Optional: single-file self-contained publish

```powershell
.\publish.ps1 -Runtime win-x64 -SingleFile -SelfContained
```

Use this if you want the easiest deployment on a machine that may not already have the matching .NET runtime. The first self-contained publish may need to download runtime packs from NuGet.

## Configuration

The publish output includes a starter `tc-batch-rename.json` next to the executable. If that file is missing, the helper creates one with a safe Notepad-based default.

Minimal config:

```json
{
  "editorPath": "notepad.exe",
  "editorArguments": ["{file}"],
  "editorWorkingDirectory": "",
  "confirmBeforeRename": true
}
```

Example for Notepad++:

```json
{
  "editorPath": "C:\\Program Files\\Notepad++\\notepad++.exe",
  "editorArguments": ["-multiInst", "-nosession", "{file}"],
  "editorWorkingDirectory": "",
  "confirmBeforeRename": true
}
```

Example for VS Code:

```json
{
  "editorPath": "C:\\Users\\YourUser\\AppData\\Local\\Programs\\Microsoft VS Code\\Code.exe",
  "editorArguments": ["--wait", "{file}"],
  "editorWorkingDirectory": "",
  "confirmBeforeRename": true
}
```

For editors like VS Code that usually return immediately, you must include the editor's own wait flag such as `--wait`.

## Total Commander Setup

Run `.\deploy.ps1` to place the executable at the stable default location:

```text
C:\programki\TcBatchRename.exe
```

Then create a button, menu item, or user command in Total Commander with:

- Command: `C:\programki\TcBatchRename.exe`
- Parameters: `--list "%UL"`
- Start path: `%P`

Why `%UL`:

- `%L` creates a temporary list file with the selected file paths.
- `%UL` is the UTF-8 version with BOM, which is the safest choice for Unicode file names.
- Passing a list file avoids command-line length issues when many files are selected.

## Example Invocation

These two examples are equivalent:

### Total Commander button or user command

- Command: `C:\programki\TcBatchRename.exe`
- Parameters: `--list "%UL"`

### Manual command line test

```powershell
.\artifacts\publish\win-x64\TcBatchRename.exe "C:\Work\File A.txt" "C:\Work\File B.txt"
```

## Limitations

- The helper is designed for files, not directories. If a directory is included in the selection, the tool aborts.
- The helper waits for the editor process it launches. Some editors spawn another process and exit immediately unless you pass a dedicated wait flag. VS Code is the common example.
- Total Commander passes selected files to external tools in panel order, not true click-selection order. If exact click order matters, you would need a more complex two-step workflow that tracks selection history outside the standard `%UL` mechanism.

## References

- Total Commander parameter substitution overview: https://ghisler.ch/board/viewtopic.php?p=287965
- Total Commander wiki, unified parameter system: https://www.ghisler.ch/wiki/index.php/Unified_Parameters_System
- Official forum discussion showing that selected files are passed in panel order rather than true selection order: https://ghisler.ch/board/viewtopic.php?t=39555
