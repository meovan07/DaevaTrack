# Economy packet notes (Milestone 1)

Findings from recordings made with `aionmeter-cli live <seconds> --record`. The recordings themselves stay in
`captures/` (git-ignored); only field layouts go here. Byte offsets are into the packet body after the opcode.

## Test 1 — buy from a shop NPC with bound Kinah (2026-10-16)

Bound Kinah 586,000 → 336,000 (item cost 250,000). Unbound Kinah unchanged (12,974,304).

`findnum` for 586000, 336000, 250000, 12974304: **one hit**, the new bound balance.

| Opcode | When | What we know |
|---|---|---|
| `1b 56` (len 80) | at the purchase | **New balance, i64 LE at offset 15** (336000). Bytes 11–14 (`21 33 70 37`) are a candidate currency/item id for bound Kinah. Neither the old balance nor the price is in it, so the server sends the absolute balance, not a delta. |
| `8c 56` (len 10), `8e 56` (len 19), `8f 56` (len 16) | same moment | Likely the bought item arriving in the inventory: all three share `04 87 01 00` (candidate item/slot id) followed by `0a 00 00 00` = **quantity 10** (confirmed: bought 10 resurrection items). `8e`/`8f` end with an i64 ms timestamp. |

Not seen: the unbound balance. The server seems to send only the currency that changed.

## Test 2 — claim an achievement reward of 12,000 bound Kinah (2026-10-16)

Bound Kinah 336,000 → 348,000. `findnum`: one hit, `1b 56` again, but the balance is now at offset 55.

Dumping both `1b 56` packets shows the real layout: **a list of item stacks, and Kinah is just an item.**

```
u8   count                       (02 in both packets)
per entry (~40 bytes):
  01 00
  u32  serial / slot             (differs per entry)
  u32  ?                         (00 72 00 00, 00 85 00 00, ...)
  u32  item id                   37703321 = bound Kinah
  i64  new quantity              absolute, not a delta
  ...  flags / padding           (01 01 00.. , 01 09 00..)
```

- Test 1 entries: bound Kinah = 336,000, and item `1df78244` = **13** (likely the resurrection items: 3 owned + 10 bought).
- Test 2 entries: item `37703337` = **333,946** (unknown, maybe another currency from the achievement), bound Kinah = 348,000.
- `8a 56` (len 20) arrives with the achievement claim; contents not yet understood.

## Test 3 — sell items for unbound Kinah (2026-10-16)

Unbound Kinah 12,974,304 → 12,974,416 → 12,974,478 (two sales). Each sale: a one-entry `1b 56` with item id
**`3770333F` = unbound Kinah** and the new total as i64.

## Currency item ids

| Id | Currency |
|---|---|
| `3770333F` | Kinah (unbound) |
| `37703321` | Bound Kinah |
| `37703337` | unknown, 333,946 after an achievement claim |

## Parser

`src/AionMeter.Core/Economy/KinahParser.cs` reads both Kinah balances from `1b 56`; `aionmeter-cli kinah <pcap>`
prints them over a recording. Checked against all three recordings above: every balance matches what was seen in game.

## Open questions

- Is the starting balance sent on login or character switch (so the tracker knows the total without waiting for a change)?
- What is item `37703337` (333,946)?
- Is `04 87 01 00` in `8c/8e/8f 56` the item id or an inventory slot? → buy a different item and compare.
