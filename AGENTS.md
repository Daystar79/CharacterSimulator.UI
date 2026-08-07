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
* **Solution:** `CharacterSimulator.UI.sln` (.NET 10.0 Blazor Desktop)
* **Projects:**
  * `CharacterSimulator.GUI`: Blazor Desktop UI
  * `CharacterSimulator.Logic`: Core domain logic, DB repositories, LLM integration, and Image Art Engine
  * `CharacterSimulator.Logic.Tests`: xUnit test suite
