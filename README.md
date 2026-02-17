# Torrential

A high-performance Torrent client built with .NET.

BEPs:

- [ ] BEP03 - Bit Torrent Protocol
  - [x] Decode metadata files
  - [x] HTTP Trackers
  - [x] TCP Listener
  - [ ] Peer Messages
    - [x] Choke
      - [x] Send
      - [x] Receive
    - [x] Unchoke
      - [x] Send
      - [x] Receive
    - [x] Interested
      - [x] Send
      - [x] Receive
    - [x] Not Interested
      - [x] Send
      - [x] Receive
    - [x] Have
      - [x] Send
      - [x] Receive
    - [x] Bitfield
      - [x] Send
      - [x] Receive
    - [x] Request
      - [x] Send
      - [x] Receive
    - [x] Piece
      - [x] Send
      - [x] Receive
    - [ ] Cancel
      - [ ] Send
      - [ ] Receive
- [ ] BEP06 - Fast Extensions
- [x] BEP15 - UDP Tracker Protocol

## Docker

The Docker image is published to GitHub Container Registry on every push to `master` and on version tags.

**Image:** `ghcr.io/labanar/torrential`

### Tags

| Tag | Description |
|---|---|
| `latest` | Latest build from the `master` branch |
| `sha-<short>` | Immutable tag tied to a specific commit SHA |
| `v*` | Immutable tag matching a Git version tag (e.g. `v1.0.0`) |

### Quick start

```bash
docker pull ghcr.io/labanar/torrential:latest
docker run -p 8080:8080 -v torrential-data:/app/data ghcr.io/labanar/torrential:latest
```
