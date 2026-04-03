

## 3) C2S / INJ: справочник пакетов (как в коде)

Источник структур: [`Protocol/PacketBuilder.cs`](../Protocol/PacketBuilder.cs).  
В логах plaintext пакета: `[opcode][payload...]` (длина в байтах = `1 + payload`).

| Opcode | Имя (условное) | Plain len | Payload layout (LE int32, если не указано) | Где используется |
|--------|----------------|-----------|---------------------------------------------|------------------|
| `0x01` | MoveToLocation | 29 | destXYZ, origXYZ, moveMode | `BuildMoveToLocation` — подход к цели/луту |
| `0x04` | Action | 18 | objectId, x, y, z, actionId (1B) | `BuildAction` — Teon таргет; self-target под баф |
| `0x0A` | AttackRequest | 18 | objectId, x, y, z, shift (1B) | `BuildAttackRequest` — `LegacyAttackRequest` |
| `0x1F` | Target enter | 18 | objectId, x, y, z, tail (1B) | `BuildTargetEnter` — **Bartz** таргет/лут |
| `0x2F` | ShortcutSkillUse | 10 | skillOrActionId, ctrl, shift (1B) | `BuildShortcutSkillUse`, `BuildForceAttack` (id 16) — **Teon** |
| `0x37` | Target cancel | 3 или 5 | short или dword 0 | `BuildTargetCancel` |
| `0x39` | MagicSkillUse | 7 / 10 / 13 | `dcc` / `dcb` / `ddd` | `BuildMagicSkillUse` — **Bartz** скиллы (`dcb` типично) |
| `0x45` | ActionUse | 10 | actionId, ctrl, shift (1B) | `BuildActionUse` — sit/stand и др. |
| `0x48` | GetItem | 21 | xyz, objectId, 0 | `BuildGetItem` — лут (не-Bartz пути) |
| `0x59` | Attack use | 21 | xyz, attackParam, 0 | `BuildAttackUse59` — **не** основной inject для Bartz engage в `CombatService` |

### 3.1 Варианты `0x39` (сервер-зависимо)


| Style | Размер payload | Описание |
|-------|----------------|----------|
| `dcb` | 9 | skillId + ctrl + shift(byte) — часто L2J / Bartz |

 `UserInfo` (base `0x04`)
  - для `Me`: `CurHp/MaxHp`, `CurMp/MaxMp`, `CurCp/MaxCp`, позиция и др.
- `StatusUpdate`/`StatusUpdate2` (base `0x6D` и `0x0E`)
  - ключевые attrId: `0x09` (cur HP), `0x0A` (max HP), `0x0B` (cur MP), `0x0C` (max MP), `0x21` (cur CP), `0x22` (max CP).
