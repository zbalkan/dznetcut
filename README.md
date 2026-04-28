# dznetcut
A zero-footprint ARP management tool

Can be used to "cut-off" other LAN devices by flooding the gateway device with targeted unicast spoofed ARP requests.
On Windows, install **Npcap** for SharpPcap support (WinPcap is deprecated).

### ⚖️ Licensing & Provenance

This project is a **Hard Fork** of `csarp-netcut`, modernized and expanded with components from the **dzmac** project.

#### Technical Lineage
* **Upstream Base:** [globalpolicy/csarp-netcut](https://github.com/globalpolicy/csarp-netcut) (Forked at `6952d98`)
* **Core Components:** Derived from [dzmac](https://github.com/DeltaZulu-OU/dzmac) (GPLv3)
* **Current Version:** `dznetcut` starting at commit `cbaba0b`

#### License Structure
* **Primary License:** [GPLv3](LICENSE) - Governs all code from commit `cbaba0b` onwards.
* **Original Notice:** [MIT License](LICENSE-MIT) - Applies to original portions from the 2017 upstream base.

