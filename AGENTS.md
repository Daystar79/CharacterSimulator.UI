# AGENTS.md

## Repository Instructions & Configuration Notes

### Git Authentication
* **SSH Key Storage:** SSH keys for repository operations and remote pushes are stored in the `./keys` directory.
* **Git Push Command:** When performing automated or CLI git push operations, specify the SSH key from the `./keys` folder:
  ```bash
  GIT_SSH_COMMAND="ssh -i ./keys/id_rsa -o StrictHostKeyChecking=accept-new" git push origin main
  ```
  *(or `keys/id_ed25519` / target key name).*

---

### Project Architecture Quick Reference
* **Solution:** `CharacterSimulator.UI.sln` (.NET 10.0 Blazor Desktop)
* **Projects:**
  * `CharacterSimulator.GUI`: Blazor Desktop UI
  * `CharacterSimulator.Logic`: Core domain logic, DB repositories, LLM integration, and Image Art Engine
  * `CharacterSimulator.Logic.Tests`: xUnit test suite
