# Torrential

## Torrent Manager
### Responsibilities
1. Add/Remove torrents from the task queue
	a. On add (copy the torrent file into the application data directory)
	b. On remove (if optionally selected) delete the file from the application directory
2. Start/Stop torrents
3. Store torrent metadata


# Piece Verification Service
1. Verify pieces as they are being downloaded
	a. If verified, add to the persistence queue
	b. If not verified, disconnect from the peer


## File Manager
### Responsibilities
1. Write a chunk to a file
2. Read a chunk from a file
3. Stream concurrent chunks from a file (via PipeReader)



## Things to consider

### Piece Selection
Selecting which piece to download for each peer is not a trivial task. The naive approach here would be to
keep track of the pieces we have (via bitfield) and intersect that with the peer's bitfield to determine which pieces the peer has that we're interested in.

This get's more complex when you get into piece rarity. If the peer is holding rare pieces (pieces that few peers in the swarm have, then we should prioritize those).

Another curve ball, if we're using fast extensions then a peer may advertise pieces for "superseeding". This is a piece that the peer is uploading to another user and since it's already performed the I/O to read the data it's now advertising that to it's connected peers.


### Rate Limiting
1. Support global rate limiting
2. Support per-torrent rate limiting
3. Support per-peer rate limiting

Rate limiting strategy should be determined at runtime, however the strategies can change at any time.
It's best to ask a service to rate limit and abstract away all of the details of the cascading rate limiting implementation
Peer->Torrent->Global

This check needs to happen before the selection of a piece?
Yes? - Then we simpy select a piece and fire away
No? Then way may "block" a piece while we're waiting (if we use a ticket style system to reserve the right to request a piece)


a) if rate limited at the peer, then wait until bandwidth is replenished
b) if rate limited at the torrent, then wait until bandwidth is replenished
c) if rate limited at the global level, then wait until bandwidth is replenished
