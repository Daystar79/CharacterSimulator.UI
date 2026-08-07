# AGENTS.md

## Repository Instructions & Configuration Notes

### Git Authentication
* **SSH Key Storage:** SSH keys for repository operations and remote pushes are stored in `/mnt/Books/Keys/id_ed25519_github` (or `./keys` directory).
* **Git Push Command:** When performing automated or CLI git push operations, specify the SSH key:
  ```bash
  GIT_SSH_COMMAND="ssh -i /mnt/Books/Keys/id_ed25519_github -o StrictHostKeyChecking=accept-new" git push origin main
  ```

---

### Project Architecture Quick Reference
* **Product name:** **Simulacra** (user-facing). Code projects remain `CharacterSimulator.*`.
* **What this is:** Desktop **roleplay host** for CharacterSimulator cards + cognitive pipeline (not the mind engine itself). Prefer “Simulacra” in UI copy; “host” in architecture notes.
* **Solution:** `CharacterSimulator.UI.sln` (.NET 10.0 Photino + Blazor)
* **Projects:**
  * `CharacterSimulator.GUI`: Desktop shell / **Simulacra** (scene · dialogue · character workspace)
  * `CharacterSimulator.Logic`: Host services — cards, catalog, safety, prompts, images, SQLite
  * `CharacterSimulator.Logic.Tests`: xUnit test suite
* **No TUI:** Spectre console host removed; do not reintroduce without a clear need.
* **Themes:** CSS design tokens in `GUI/wwwroot/css/app.css`; boot/apply via `wwwroot/js/theme.js` (`csTheme.apply`). Catalog: `ThemeCatalog`. Do not hardcode Midnight slate hexes in new chrome.
* **Cards:** Keep `personality`, `behavior`, `physical`, `character_style` separate — see root `README.md` and `Characters/HOW_TO_CARD.md`.
