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

## Party / Follow / Assist

Confirmed from the latest Bartz capture set dated 2026-04-03:

- `PartySmallWindowAll` (`wire 0x54 -> resolved 0x4E`) has a compact Bartz layout and should not be parsed with the old Teon string-based structure.
- `PartySmallWindowUpdate` (`wire 0x48 -> resolved 0x52`, 40-byte payload) is a confirmed source of party member position updates. In the current implementation the tail `x/y/z/heading` fields are used conservatively.
- `PartySmallWindowDelete` (`wire 0x4A -> resolved 0x50`) is still noisy in Bartz captures, so the handler now ignores unexpected payload shapes instead of guessing.
- Party member target tracking for assist mode is confirmed through live combat packets: `Attack` and `MagicSkillLaunched` now update `PartyMember.TargetId` when the actor is a known party member.
- Party member movement is also refreshed from `MoveToPoint` and `ValidatePosition` when the moving object already exists in `world.Party`.
- Party buffs are still not confirmed from packet evidence. This phase only stores room for party buff state on `PartyMember`; it does not ship automatic party buff casting based on guessed packets.

Implementation status for v1:

- `Follow` only acts on fresh leader coordinates and does not attack.
- `Assist` follows the assist actor and attacks only the assist target; if the assist actor has no target, combat stays idle.
- Leader and assist resolution remains name-based for now, matching the current profile/UI contract.
