# Bartz Packet Investigation

The proxy now records structured Bartz packet evidence as a `kamael-like` investigation session instead of relying on ad-hoc hex snippets.

## What Gets Captured

- `S2C` observations from `PacketDispatcher`
- `C2S` packets relayed from the live client
- `C2S` packets injected by the bot
- A grouped Markdown matrix with wire opcode, resolved opcode, payload length, handler/classification, and source
- Raw `.hex` payload captures with metadata headers

## Where To Find It

After a Bartz session disconnects, the app writes a report under:

`captures/bartz-investigation/<utc-stamp>/`

Important files:

- `bartz-packet-investigation.md`
- `captures/*.hex`

## Intended Workflow

1. Start the proxy on `bartz`
2. Perform a controlled session:
   login, enter world, open skills, target NPCs, take damage, spend mana, loot, sit/stand
3. Disconnect
4. Inspect the generated matrix and raw captures
5. Use repeated `unknown` rows and suspicious length patterns to drive the next parser fix pass

## Current Assumption

`Bartz` should be treated as `kamael-like` until packet evidence proves otherwise.
