# Repository-wide Copilot Instructions

- Never update the 1.5 definitions. The files and definitions under the `1.5/` directory are in maintenance mode only.
- You may reference the decompiled RimWorld core code at: https://github.com/IlyaChichkov/RimworldDecompiled

## Building
- To compile the mod, use `run_in_terminal` with the command `.vscode/build.bat` instead of `run_task`. This executes without leaving the terminal in an interactive state.

## Code Style
- Avoid deep if statement nesting. When an if statement has no else clause, invert the condition and use an early return instead. This improves readability and keeps code flat.
