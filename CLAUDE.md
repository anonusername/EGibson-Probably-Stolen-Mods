# CLAUDE.md

This repo's agent instructions live in the private `.agent-files` submodule
(`git@github.com:anonusername/Internal_Agent_Instructions.git`), in `.agent-files/EGibson-Probably-Stolen-Mods/`.
Use only that folder; the other folders under `.agent-files/` belong to other projects. Launch Claude Code from this
repo root.

## If `.agent-files/` is empty or missing

On a fresh clone the submodule isn't fetched yet, so the import below loads nothing. Before any other work:

1. Run `git submodule update --init` from the repo root. It needs SSH access to the private repo; if that fails,
   stop and tell the user.
2. Follow "Local setup on a fresh clone" in `.agent-files/EGibson-Probably-Stolen-Mods/AGENTS.md`. It covers the
   `.claude/` agent and settings, the SessionStart hook, `.claude/upstream-docs-sha` and the mod-folder pointer.
3. Start a new session so the imported instructions load.

To pull newer instructions later: `git submodule update --remote .agent-files`, then commit the `.agent-files` pointer.

@.agent-files/EGibson-Probably-Stolen-Mods/AGENTS.md
